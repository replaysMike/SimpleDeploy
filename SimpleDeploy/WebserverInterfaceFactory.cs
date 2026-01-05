namespace SimpleDeploy
{
    public class WebserverInterfaceFactory
    {
        private readonly ILogger<IWebserverInterface> _logger;
        public WebserverInterfaceFactory(ILogger<IWebserverInterface> logger)
        {
            _logger = logger;
        }

        public IWebserverInterface Create(Webservers webserver)
        {
            switch (webserver)
            {
                case Webservers.IIS:
                    return new IISWebserverInterface(_logger);
                //case Webservers.Apache:
                //    return new ApacheWebserverInterface();
                //case Webservers.Nginx:
                //    return new NginxWebserverInterface();
                default:
                    throw new NotSupportedException($"Webserver {webserver} is not supported.");
            }
        }
    }
}
