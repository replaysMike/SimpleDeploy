using NLog;
using System.Text;

namespace SimpleDeploy
{
    public class LogWrapper
    {
        private readonly Microsoft.Extensions.Logging.ILogger? _mslogger;
        private readonly NLog.ILogger? _logger;
        private readonly StringBuilder? _output;
        private readonly DeploymentQueueItem _job;

        public LogWrapper(Microsoft.Extensions.Logging.ILogger logger, DeploymentQueueItem job)
        {
            _mslogger = logger;
            _job = job;
        }

        public LogWrapper(NLog.ILogger logger, StringBuilder output, DeploymentQueueItem job)
        {
            _logger = logger;
            _output = output;
            _job = job;
        }

        public void Trace(string message)
        {
            _logger?.Trace($"{_job.JobId}| {message}");
            _mslogger?.LogTrace($"[{_job.JobId}] {message}");
            _output?.AppendLine($"{message}");
        }

        public void Debug(string message)
        {
            _logger?.Debug($"{_job.JobId}| {message}");
            _mslogger?.LogDebug($"[{_job.JobId}] {message}");
            _output?.AppendLine($"{message}");
        }

        public void Info(string message)
        {
            _logger?.Info($"{_job.JobId}| {message}");
            _mslogger?.LogInformation($"[{_job.JobId}] {message}");
            _output?.AppendLine($"{message}");
        }

        public void Warn(string message)
        {
            _logger?.Warn($"{_job.JobId}| {message}");
            _mslogger?.LogWarning($"[{_job.JobId}] {message}");
            _output?.AppendLine($"{message}");
        }

        public void Warn(Exception ex, string message)
        {
            _logger?.Warn(ex, $"{_job.JobId}| {message}");
            _mslogger?.LogWarning(ex, $"[{_job.JobId}] {message}");
            _output?.AppendLine($"{message}{Environment.NewLine}Exception [{ex.GetType()}]: {ex.GetBaseException().Message}");
        }

        public void Error(string message)
        {
            _logger?.Error($"{_job.JobId}| {message}");
            _mslogger?.LogError($"[{_job.JobId}] {message}");
            _output?.AppendLine($"{message}");
        }

        public void Error(Exception ex, string message)
        {
            _logger?.Error(ex, $"{_job.JobId}| {message}");
            _mslogger?.LogError(ex, $"[{_job.JobId}] {message}");
            _output?.AppendLine($"{message}{Environment.NewLine}Exception [{ex.GetType()}]: {ex.GetBaseException().Message}");
        }
    }

    public class CustomLogManager
    {
        private const string DefaultPath = "c:/SimpleDeploy";
        private const string LogFileName = "deploy.log";
        private readonly Dictionary<string, LogFactory> _logFactories = new Dictionary<string, LogFactory>();
        private readonly IDictionary<string, NLog.Layouts.Layout> _variables;

        public CustomLogManager(IDictionary<string, NLog.Layouts.Layout> variables)
        {
            _variables = variables;
        }

        public LogWrapper GetDeploymentLogger(DeploymentQueueItem job, StringBuilder output, string logPath)
        {
            var deploymentName = job.DeploymentName;
            if (!_logFactories.ContainsKey(deploymentName))
            {
                //var logDir = _variables["var_logdir"];
                var logFactory = new LogFactory();
                var logConfig = new NLog.Config.LoggingConfiguration();
                var logFile = new NLog.Targets.FileTarget()
                {
                    FileName = Path.Combine(logPath.ToString() ?? DefaultPath, LogFileName),
                    Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:inner=${newline}${exception:format=tostring}}",
                };
                logConfig.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logFile);
                logFactory.Configuration = logConfig;
                _logFactories.Add(deploymentName, logFactory);
            }
            return new LogWrapper(_logFactories[deploymentName].GetLogger(deploymentName), output, job);
        }
    }
}
