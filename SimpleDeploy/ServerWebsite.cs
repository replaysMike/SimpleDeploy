namespace SimpleDeploy
{
    public class ServerWebsite
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }
        public string Bindings { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PhysicalPath { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}
