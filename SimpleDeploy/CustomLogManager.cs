using NLog;
using System.Text;

namespace SimpleDeploy
{
    public class LogWrapper
    {
        private readonly NLog.ILogger _logger;
        private readonly StringBuilder _output;

        public LogWrapper(NLog.ILogger logger, StringBuilder output)
        {
            _logger = logger;
            _output = output;
        }

        public void Trace(string message)
        {
            _logger.Trace(message);
            _output.AppendLine(message);
        }

        public void Debug(string message)
        {
            _logger.Debug(message);
            _output.AppendLine(message);
        }

        public void Info(string message)
        {
            _logger.Info(message);
            _output.AppendLine(message);
        }

        public void Warn(string message)
        {
            _logger.Warn(message);
            _output.AppendLine(message);
        }

        public void Warn(Exception ex, string message)
        {
            _logger.Warn(ex, message);
            _output.AppendLine($"{message}{Environment.NewLine}Exception [{ex.GetType()}]: {ex.GetBaseException().Message}");
        }

        public void Error(string message)
        {
            _logger.Error(message);
            _output.AppendLine(message);
        }

        public void Error(Exception ex, string message)
        {
            _logger.Error(ex, message);
            _output.AppendLine($"{message}{Environment.NewLine}Exception [{ex.GetType()}]: {ex.GetBaseException().Message}");
        }
    }

    public class CustomLogManager
    {
        private const string DefaultPath = "c:/SimpleDeploy";
        private readonly Dictionary<string, LogFactory> _logFactories = new Dictionary<string, LogFactory>();
        private readonly IDictionary<string, NLog.Layouts.Layout> _variables;

        public CustomLogManager(IDictionary<string, NLog.Layouts.Layout> variables)
        {
            _variables = variables;
        }

        public LogWrapper GetWebsiteLogger(string website, StringBuilder output)
        {
            if (!_logFactories.ContainsKey(website))
            {
                var logDir = _variables["var_logdir"];
                var logFactory = new LogFactory();
                var logConfig = new NLog.Config.LoggingConfiguration();
                var logFile = new NLog.Targets.FileTarget()
                {
                    FileName = Path.Combine(logDir.ToString() ?? DefaultPath, website, $"deploy.log"),
                    Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:inner=${newline}${exception:format=tostring}}",
                };
                logConfig.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logFile);
                logFactory.Configuration = logConfig;
                _logFactories.Add(website, logFactory);
            }
            return new LogWrapper(_logFactories[website].GetLogger(website), output);
        }
    }
}
