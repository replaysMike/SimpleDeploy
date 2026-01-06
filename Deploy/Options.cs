using CommandLine;

namespace Deploy
{
    public class Options
    {
        [Option('a', "add", Required = false, HelpText = "Add an artifact to the deployment")]
        public bool AddArtifact { get; set; }

        [Option('r', "remove", Required = false, HelpText = "Remove an artifact from the deployment")]
        public bool RemoveArtifact { get; set; }

        [Option('g', "get", Required = false, HelpText = "Get the artifacts for the deployment")]
        public bool GetArtifacts { get; set; }

        [Option('d', "deploy", Required = false, HelpText = "Deploy the website")]
        public bool Deploy { get; set; }

        [Option('f', "file", Required = false, HelpText = "Specify the filename(s) of an artifact")]
        public IEnumerable<string>? File { get; set; }

        [Option('w', "website", Required = true, HelpText = "Specify the website to deploy")]
        public string? Website { get; set; }

        [Option('s', "script", Required = false, HelpText = "Specify the deployment script, filename or content")]
        public string? DeploymentScript { get; set; }

        [Option('h', "host", Required = false, HelpText = "Specify the host to deploy to (ip or hostname)")]
        public string? Host { get; set; }

        [Option('u', "username", Required = false, HelpText = "Specify the username for deployment")]
        public string? Username { get; set; }

        [Option('p', "password", Required = false, HelpText = "Specify the password for deployment")]
        public string? Password { get; set; }

        [Option('t', "token", Required = false, HelpText = "Specify the authentication token for deployment")]
        public string? Token { get; set; }

        [Option("port", Required = false, HelpText = "Specify the port number of the deployment host (default: 5001)")]
        public int Port { get; set; } = 5001;

        [Option("timeout", Required = false, HelpText = "Specify the timeout (default: 5 seconds)")]
        public int Timeout { get; set; } = 5;

        [Option("request-timeout", Required = false, HelpText = "Specify the request timeout (default: 300 seconds)")]
        public int RequestTimeout { get; set; } = 300;

        [Option('v', "verbose", Required = false, HelpText = "Specify verbose output")] 
        public bool Verbose { get; set; }

        [Option("autocopy", Required = false, HelpText = "Automatically copy files to destination after running deployment (default: true)")]
        public bool AutoCopy { get; set; } = true;

        [Option("autoextract", Required = false, HelpText = "Automatically extract compressed files before running deployment (default: false)")]
        public bool AutoExtract { get; set; }

        [Option('i', "ignorecert", Required = false, HelpText = "Ignore any SSL certificate errors")]
        public bool IgnoreCert { get; set; }

        [Option("interactive", Required = false, HelpText = "Run deployment in interactive mode to view the output")]
        public bool Interactive { get; set; }
    }
}
