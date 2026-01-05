using NLog;

namespace SimpleDeploy
{
    public class CustomLogManager
    {
        private const string DefaultPath = "c:/SimpleDeploy";
        private readonly Dictionary<string, LogFactory> _logFactories = new Dictionary<string, LogFactory>();
        private readonly IDictionary<string, NLog.Layouts.Layout> _variables;

        public CustomLogManager(IDictionary<string, NLog.Layouts.Layout> variables)
        {
            _variables = variables;
        }

        public NLog.ILogger GetWebsiteLogger(string website)
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
            return _logFactories[website].GetLogger(website);
        }
    }
}
