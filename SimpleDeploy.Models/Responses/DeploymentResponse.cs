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
    }
}
