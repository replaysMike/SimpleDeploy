namespace SimpleDeploy
{
    public class DeploymentQueueItem
    {
        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// Deployment script or filename from artifacts
        /// </summary>
        public string? DeploymentScript { get; set; }

        /// <summary>
        /// List of artifact files
        /// </summary>
        public List<ArtifactFile> Artifacts { get; set; } = new();
        
        /// <summary>
        /// Website to deploy
        /// </summary>
        public string Website { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; }
    }

    public class ArtifactFile
    {
        /// <summary>
        /// File contents of artifact
        /// </summary>
        public MemoryStream Data { get; set; } = new();

        /// <summary>
        /// Filename of artifact
        /// </summary>
        public string Filename { get; set; } = string.Empty;
        
        public DateTime DateCreated { get; set; }
    }
}
