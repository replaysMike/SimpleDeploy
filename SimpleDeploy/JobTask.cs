using System.Text;

namespace SimpleDeploy
{
    public class JobTask
    {
        public DateTime Queued { get; set; } = DateTime.UtcNow;
        public DateTime? Started { get; set; }
        public DateTime? Completed { get; set; }

        public DeploymentQueueItem Request { get; }
        public Task Task { get; }
        public StringBuilder Output { get; }

        public JobTask(DeploymentQueueItem request, Task task, StringBuilder output)
        {
            Request = request;
            Task = task;
            Output = output;
        }
    }
}
