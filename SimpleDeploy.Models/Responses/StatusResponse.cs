namespace SimpleDeploy.Responses
{
    public class StatusResponse
    {
        public Status Status { get; set; } = Status.Stopped;
        public IEnumerable<string> Websites { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<string> Allowed { get; set; } = Enumerable.Empty<string>();
    }

    public enum Status
    {
        Starting,
        Running,
        Stopping,
        Stopped
    }
}
