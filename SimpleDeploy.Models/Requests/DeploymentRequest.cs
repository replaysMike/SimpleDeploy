using Microsoft.AspNetCore.Http;

namespace SimpleDeploy.Requests
{
    public class DeploymentRequest
    {
        /// <summary>
        /// The website to deploy
        /// </summary>
        public string Website { get; set; } = string.Empty;

        /// <summary>
        /// Pre-deployment script or filename from artifacts
        /// </summary>
        public string? DeploymentScript { get; set; }

        /// <summary>
        /// True to copy the files automatically after running the deployment script
        /// </summary>
        public bool AutoCopy { get; set; } = true;

        /// <summary>
        /// True to autoextract zip files before running deployment script
        /// </summary>
        public bool AutoExtract { get; set; }

        /// <summary>
        /// List of artifacts to deploy
        /// </summary>
        public List<IFormFile> Artifacts { get; set; } = new();
    }
}
