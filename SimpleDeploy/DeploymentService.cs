using NLog;
using NLog.Extensions.Logging;
using SimpleDeploy.Middleware;
using System.Net;
using Topshelf;

namespace SimpleDeploy
{
    public class DeploymentService
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DeploymentService> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Configuration _config;

        public DeploymentService(ILoggerFactory loggerFactory, Configuration config)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<DeploymentService>();
            _config = config;
        }

        public bool Start(HostControl hostControl)
        {
            RunStart();
            return true;
        }

        public bool Stop()
        {
            try
            {
                _cancellationTokenSource.Cancel();
                _logger.LogInformation($"{nameof(SimpleDeploy)} service stopped!");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to stop {nameof(SimpleDeploy)}!");
            }
            finally
            {
                // ensure logmanager stops internal timers/threads
                LogManager.Shutdown();
            }
            return true;
        }

        private void RunStart()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options =>
            {
                if (_config.IpAddress == "*")
                    options.ListenAnyIP(_config.Port, listenOptions =>
                    {
                        if (_config.UseHttps)
                            listenOptions.UseHttps();
                    });
                else
                    options.Listen(IPAddress.Parse(_config.IpAddress), _config.Port, listenOptions =>
                    {
                        if (_config.UseHttps)
                            listenOptions.UseHttps();
                    });
            });

            // Add services to the container.

            builder.Services.AddNLog();
            builder.Services.AddSingleton<Configuration>(_config);
            builder.Services.AddSingleton<DeploymentQueueService>();
            builder.Services.AddScoped<AuthenticatorService>();
            builder.Services.AddSingleton<WebserverInterfaceFactory>();
            builder.Services.AddSingleton<IWebserverInterface>((services) =>
            {
                // currently we only support IIS
                var interfaceFactory = services.GetRequiredService<WebserverInterfaceFactory>();
                return interfaceFactory.Create(Webservers.IIS);
            });
            builder.Services.AddSingleton<CustomLogManager>(new CustomLogManager(LogManager.Configuration.Variables));

            builder.Services.AddControllers();

            var app = builder.Build();

            app.UseMiddleware<IpRestrictionMiddleware>(_config);

            if (_config.UseHttps)
            {
                // force https redirection
                app.UseHttpsRedirection();
            }

            app.UseAuthorization();

            // allow serving a default webpage
            app.UseDefaultFiles();
            app.UseStaticFiles();

            // map api controllers
            app.MapControllers();

            var message = $"{nameof(SimpleDeploy)} service started on port {_config.Port}";
            if (!string.IsNullOrEmpty(_config.Username)) message += $" with user based authentication";
            else if (!string.IsNullOrEmpty(_config.AuthToken)) message += $" with token based authentication";
            _logger.LogInformation(message);
            app.RunAsync(_cancellationTokenSource.Token);
        }
    }
}
