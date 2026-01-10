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
        /// Path to the jobs folder (can be relative to WorkingFolder)
        /// </summary>
        public string JobsFolder { get; set; } = "Jobs";

        /// <summary>
        /// Path to the backups folder (can be relative to WorkingFolder)
        /// </summary>
        public string BackupsFolder { get; set; } = "Backups";

        /// <summary>
        /// Maximum size of all artifacts for a single deployment (request size)
        /// </summary>
        public long MaxDeploymentSize { get; set; } = 1000 * 1024 * 1024; // Max 1GB default

        /// <summary>
        /// Minimum free space to allow deployments
        /// </summary>
        public long MinFreeSpace { get; set; } = 100 * 1024 * 1024; // 100MB

        /// <summary>
        /// Maximum number of backup files to keep in history (per deployment name)
        /// </summary>
        public int MaxBackupFiles { get; set; } = 10;

        /// <summary>
        /// Remove all working files after deployment is complete
        /// </summary>
        public bool CleanupAfterDeploy { get; set; } = true;

        /// <summary>
        /// List of websites that can be deployed. Set to "*" to allow all.
        /// </summary>
        public DeploymentNamesConfiguration DeploymentNames { get; set; } = new();
    }

    public class DeploymentNamesConfiguration
    {
        /// <summary>
        /// List of deployment names to allow
        /// </summary>
        public List<string> Allow { get; set; } = new();

        public List<DeploymentNameConfiguration> Configurations { get; set; } = new();
    }

    public class DeploymentNameConfiguration
    {
        /// <summary>
        /// Name of deployment (can be website name)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Website domain name to configure
        /// </summary>
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Physical path of the website on the webserver.
        /// If IIS is enabled, this will be fetched from IIS however you may override it here.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// True if it's an IIS website.
        /// Forces configuration on the agent side.
        /// </summary>
        public bool IIS { get; set; }

        /// <summary>
        /// True to autocopy files after deployment script completes.
        /// Forces configuration on the agent side.
        /// </summary>
        public bool AutoCopy { get; set; }

        /// <summary>
        /// True to autoextract compressed files before deployment script starts.
        /// Forces configuration on the agent side.
        /// </summary>
        public bool AutoExtract { get; set; }

        /// <summary>
        /// True to delete all file contents of the destination path before deploying.
        /// Configuration only available on the agent side.
        /// </summary>
        public bool CleanBeforeDeploy { get; set; }

        /// <summary>
        /// True to stop the website on the webserver before deploying.
        /// Forces configuration on the agent side.
        /// </summary>
        public bool StopBeforeDeploy { get; set; }

        /// <summary>
        /// True to start the website on the webserver after deploying.
        /// Forces configuration on the agent side.
        /// </summary>
        public bool StartAfterDeploy { get; set; }

        /// <summary>
        /// True to enable backup of the current website before deploying
        /// Configuration only available on the agent side.
        /// </summary>
        public bool Backup { get; set; }
    }
}
