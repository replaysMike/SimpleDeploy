using NLog;
using NLog.Extensions.Logging;
using SimpleDeploy;
using Topshelf;
using Topshelf.Runtime;

var serviceDescription = "SimpleDeploy provides website deployment services.";
var displayName = "SimpleDeploy";
var serviceName = "SimpleDeploy";


//var logFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddConsole());
//var logger = logFactory.CreateLogger<Program>();


var configPath = AppContext.BaseDirectory;
var configFile = Path.Combine(configPath, "appsettings.json");
var configBuilder = new ConfigurationBuilder()
                .SetBasePath(configPath)
                .AddJsonFile(configFile, optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
var configRoot = configBuilder.Build();
var config = configRoot.GetSection(nameof(Configuration)).Get<Configuration>();
if (config == null) throw new Exception("Unable to load configuration via appsettings.json");

LogManager.Setup()
                .SetupExtensions(s => s.RegisterConfigSettings(configRoot))
                .LoadConfigurationFromSection(configRoot);
var loggerFactory = new NLog.Extensions.Logging.NLogLoggerFactory();
var msLogger = loggerFactory.CreateLogger<DeploymentService>();

var rc = HostFactory.Run(x =>
{
    x.Service<DeploymentService>(s =>
    {
        s.ConstructUsing(name => new DeploymentService(loggerFactory, config));
        s.BeforeStartingService(tc =>
        {
            // check if port is in use before proceeding
            // NOTE: disable this if hosting in IIS
            if (Ports.IsPortInUse(config.Port))
            {
                var message = $"The port '{config.Port}' is currently in use.";
                msLogger.LogError(message);
                Console.WriteLine(message);
                Environment.Exit(-1);
                return;
            }
        });
        s.WhenStarted((tc, hostControl) => tc.Start(hostControl));
        s.WhenStopped((tc, hostControl) => tc.Stop());
    });
    x.RunAsLocalSystem();
    x.SetDescription(serviceDescription);
    x.SetDisplayName(displayName);
    x.SetServiceName(serviceName);
    x.SetStartTimeout(TimeSpan.FromSeconds(15));
    x.SetStopTimeout(TimeSpan.FromSeconds(10));
    x.BeforeInstall(() => Console.WriteLine($"Installing service {serviceName}..."));
    x.BeforeUninstall(() => Console.WriteLine($"Uninstalling service {serviceName}..."));
    x.AfterInstall(() => Console.WriteLine($"{serviceName} service installed."));
    x.AfterUninstall(() => Console.WriteLine($"{serviceName} service uninstalled."));
    x.OnException((ex) =>
    {
        Console.WriteLine($"Error: {serviceName} exception thrown: {ex.Message}{Environment.NewLine}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
    });

    x.UnhandledExceptionPolicy = UnhandledExceptionPolicyCode.LogErrorAndStopService;
});
