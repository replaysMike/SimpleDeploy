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
        /// True to copy the files automatically after running the deployment script
        /// </summary>
        public bool AutoCopy { get; set; } = true;

        /// <summary>
        /// True to autoextract zip files before running deployment script
        /// </summary>
        public bool AutoExtract { get; set; }

        /// <summary>
        /// List of artifact files
        /// </summary>
        public List<ArtifactFile> Artifacts { get; set; } = new();
        
        /// <summary>
        /// Website to deploy
        /// </summary>
        public string? Domain { get; set; }

        public string DeploymentName { get; set; } = string.Empty;

        public bool Interactive { get; set; }

        public bool IIS { get; set; }

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
