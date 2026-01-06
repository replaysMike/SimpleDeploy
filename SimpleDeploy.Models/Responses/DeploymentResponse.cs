namespace SimpleDeploy.Responses
{
    public class DeploymentResponse
    {
        /// <summary>
        /// True if deployment was successful
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// An optional message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Log output from the deployment (only if interactive mode was specified)
        /// </summary>
        public string Log { get; set; } = string.Empty;
    }
}
