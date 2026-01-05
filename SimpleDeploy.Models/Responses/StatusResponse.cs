namespace SimpleDeploy.Responses
{
    public class StatusResponse
    {
        public Status Status { get; set; } = Status.Stopped;
    }

    public enum Status
    {
        Starting,
        Running,
        Stopping,
        Stopped
    }
}
