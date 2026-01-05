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
        /// List of artifacts to deploy
        /// </summary>
        public List<IFormFile> Artifacts { get; set; } = new();
    }
}
