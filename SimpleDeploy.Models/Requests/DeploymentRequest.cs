using Microsoft.AspNetCore.Http;

namespace SimpleDeploy.Requests
{
    public class DeploymentRequest
    {
        /// <summary>
        /// The name of the deployment
        /// </summary>
        public string DeploymentName { get; set; } = string.Empty;

        /// <summary>
        /// The domain name of the website
        /// </summary>
        public string? Domain { get; set; }

        /// <summary>
        /// Pre-deployment script or filename from artifacts
        /// </summary>
        public string? DeploymentScript { get; set; }

        /// <summary>
        /// True to copy the files automatically after running the deployment script
        /// </summary>
        public bool AutoCopy { get; set; }

        /// <summary>
        /// True to autoextract zip files before running deployment script
        /// </summary>
        public bool AutoExtract { get; set; }

        /// <summary>
        /// List of artifacts to deploy
        /// </summary>
        public List<IFormFile> Artifacts { get; set; } = new();

        /// <summary>
        /// True to run deployment in interactive mode to view output
        /// </summary>
        public bool Interactive { get; set; }

        /// <summary>
        /// True to start/stop the website automatically
        /// </summary>
        public bool IIS { get; set; }

        /// <summary>
        /// Timeout for the deployment operation to return a response in interactive mode (in seconds). Default: 300 seconds.
        /// </summary>
        public int InteractiveTimeout { get; set; } = 300;
    }
}
