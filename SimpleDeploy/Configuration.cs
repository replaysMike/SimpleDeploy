namespace SimpleDeploy
{
    public class Configuration
    {
        /// <summary>
        /// IP address to listen on, "*" for all interfaces
        /// </summary>
        public string IpAddress { get; set; } = "*";

        /// <summary>
        /// Port number to listen at
        /// </summary>
        public int Port { get; set; } = 5001;

        /// <summary>
        /// True to force https
        /// </summary>
        public bool UseHttps { get; set; } = true;

        /// <summary>
        /// Username to access the deployment service
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Password to access the deployment service
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Simple token to authenticate against
        /// </summary>
        public string AuthToken { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the list of allowed IP addresses or ranges for access control, specified as a comma-separated string.
        /// Empty will allow requests from all IP addresses.
        /// </summary>
        public List<string> IpWhitelist { get; set; } = new();

        public string WorkingFolder { get; set; } = "C:\\SimpleDeploy";
        
        /// <summary>
        /// Remove all working files after deployment is complete
        /// </summary>
        public bool CleanupAfterDeploy { get; set; } = true;

        /// <summary>
        /// List of websites that can be deployed. Set to "*" to allow all.
        /// </summary>
        public WebsitesConfiguration Websites { get; set; } = new();
    }

    public class WebsitesConfiguration
    {
        public List<string> Allow { get; set; } = new();
        public List<WebsiteConfiguration> Configurations { get; set; } = new();
    }

    public class WebsiteConfiguration
    {
        /// <summary>
        /// Website domain name to configure
        /// </summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Physical path of the website on the webserver
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// True to stop the website on the webserver before deploying
        /// </summary>
        public bool StopBeforeDeploy { get; set; } = true;

        /// <summary>
        /// True to start the website on the webserver after deploying
        /// </summary>
        public bool StartAfterDeploy { get; set; } = true;
    }
}
