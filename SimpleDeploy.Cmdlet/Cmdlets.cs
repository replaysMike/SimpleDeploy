using SimpleDeploy.Cmdlet.Services;
using System.Management.Automation;

namespace SimpleDeploy.Cmdlet
{
    [Cmdlet(VerbsCommon.Add, "Artifact")]
    public class AddArtifactCmdlet : System.Management.Automation.PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the filename of an artifact")]
        [Alias("f")]
        public IEnumerable<string> File { get; set; } = Enumerable.Empty<string>();

        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the website")]
        [Alias("w", "domain", "d")]
        public string Website { get; set; } = string.Empty;

        protected override void ProcessRecord()
        {
            var artifactService = new ArtifactService();
            var path = SessionState.Path.CurrentLocation.Path;
            if (File == null || !File.Any())
            {
                WriteWarning($"File must be specified.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Website))
            {
                WriteWarning($"Website must be specified.");
                return;
            }

            // check if files exist
            foreach (var file in File)
            {
                var filePath = Path.GetFullPath(Path.Combine(path, file));
                if (!System.IO.File.Exists(filePath))
                {
                    WriteWarning($"File '{filePath}' does not exist!");
                    return;
                }
            }

            var store = artifactService.CreateOrLoadStore(path, Website);
            WriteVerbose($"Artifacts file: {store}");

            // add to store
            foreach (var file in File)
            {
                var filePath = Path.GetFullPath(Path.Combine(path, file));
                if (!artifactService.AddArtifact(filePath))
                {
                    WriteWarning($"File '{filePath}' already added to artifacts.");
                }
                else
                {
                    WriteVerbose($"Artifact '{filePath}' has been added.");

                }
            }

            // overwrite the temporary store
            artifactService.WriteArtifactDatabase(store);
            return;
        }
    }

    [Cmdlet(VerbsCommon.Remove, "Artifact")]
    public class RemoveArtifactCmdlet : System.Management.Automation.PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the filename of an artifact")]
        [Alias("f")]
        public IEnumerable<string> File { get; set; } = Enumerable.Empty<string>();

        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the website")]
        [Alias("w", "domain", "d")]
        public string Website { get; set; } = string.Empty;

        protected override void ProcessRecord()
        {
            var artifactService = new ArtifactService();
            var path = SessionState.Path.CurrentLocation.Path;
            if (File == null || !File.Any())
            {
                WriteWarning($"File must be specified.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Website))
            {
                WriteWarning($"Website must be specified.");
                return;
            }
            
            var store = artifactService.CreateOrLoadStore(path, Website);
            if (artifactService.ArtifactStore.Count == 0)
            {
                WriteWarning($"No artifacts have been added! Syntax: Add-Artifact -File ./examplefile.zip -Website example.com");
                return;
            }
            foreach (var file in File)
            {
                var filePath = Path.GetFullPath(Path.Combine(path, file));
                if (!artifactService.RemoveArtifact(filePath))
                {
                    WriteWarning($"Artifact '{filePath}' has not been added, cannot remove.");
                }
                else
                {
                    WriteVerbose($"Artifact '{filePath}' has been removed.");
                }
            }

            // success
            // overwrite the temporary store
            artifactService.WriteArtifactDatabase(store);
            return;
        }
    }

    [Cmdlet(VerbsCommon.Get, "Artifacts")]
    [OutputType(typeof(Artifact))]
    public class GetArtifactsCmdlet : System.Management.Automation.PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the website")]
        [Alias("w", "domain", "d")]
        public string Website { get; set; } = string.Empty;

        protected override void ProcessRecord()
        {
            var artifactService = new ArtifactService();
            var path = SessionState.Path.CurrentLocation.Path;
            if (string.IsNullOrWhiteSpace(Website))
            {
                WriteWarning("Error: Website must be specified.");
                return;
            }
            var store = artifactService.CreateOrLoadStore(path, Website);
            if (artifactService.ArtifactStore.Count == 0)
            {
                WriteWarning($"No artifacts have been added! Syntax: Add-Artifact -File ./examplefile.zip -Website example.com");
                return;
            }

            foreach (var artifact in artifactService.ArtifactStore)
            {
                if (string.IsNullOrEmpty(artifact))
                    continue;
                var artifactPath = Path.GetFullPath(artifact);
                WriteVerbose($" - Artifact: {artifactPath} ({(File.Exists(artifactPath) ? "exists" : "does not exist")})");
                WriteObject(new Artifact
                {
                    File = artifactPath
                });
            }
            return;
        }
    }

    [Cmdlet("Deploy", "Website")]
    public class DeployWebsiteCmdlet : System.Management.Automation.PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the website to deploy")]
        [Alias("w", "domain", "d")]
        public string Website { get; set; } = string.Empty;

        [Parameter(Mandatory = false, Position = 1, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the deployment script, filename or content")]
        [Alias("deployment-script", "script", "s")]
        public string? DeploymentScript { get; set; }

        [Parameter(Mandatory = true, Position = 2, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the host to deploy to (ip or hostname)")]
        [Alias("h", "ip")]
        public new string Host { get; set; } = string.Empty;

        [Parameter(Mandatory = false, Position = 3, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the username for deployment")]
        [Alias("u", "user")]
        public string Username { get; set; } = string.Empty;

        [Parameter(Mandatory = false, Position = 4, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the password for deployment")]
        [Alias("p", "pass")]
        public string Password { get; set; } = string.Empty;

        [Parameter(Mandatory = false, Position = 5, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the authentication token for deployment")]
        [Alias("t", "token")]
        public string Token { get; set; } = string.Empty;

        [Parameter(Mandatory = false, Position = 6, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the port number of the deployment host (default: 5001)")]
        public int Port { get; set; } = 5001;

        [Parameter(Mandatory = false, Position = 7, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the timeout (default: 5 seconds)")]
        public int Timeout { get; set; } = 5;

        [Parameter(Mandatory = false, Position = 8, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the request timeout (default: 300 seconds)")]
        [Alias("r")]
        public int RequestTimeout { get; set; } = 300;

        [Parameter(Mandatory = false, Position = 9, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Automatically copy files to destination after running deployment (default: true)")]
        public SwitchParameter AutoCopy { get; set; } = true;

        [Parameter(Mandatory = false, Position = 10, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Automatically extract compressed files before running deployment (default: false)")]
        public SwitchParameter AutoExtract { get; set; }

        [Parameter(Mandatory = false, Position = 11, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Ignore any SSL certificate errors")]
        [Alias("r")]
        public SwitchParameter IgnoreCert { get; set; }

        [Parameter(Mandatory = false, Position = 12, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Run deployment in interactive mode to view the output")]
        public SwitchParameter Interactive { get; set; }

        [Parameter(Mandatory = true, Position = 13, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the filename of an artifact")]
        [Alias("f")]
        public IEnumerable<string> File { get; set; } = Enumerable.Empty<string>();

        protected override void ProcessRecord()
        {
            var artifactService = new ArtifactService();
            var deployService = new DeployService();
            var path = SessionState.Path.CurrentLocation.Path;

            // check for artifacts for the website in temp store
            // upload
            // delete from temp store
            if (string.IsNullOrWhiteSpace(Website))
            {
                WriteWarning($"Website must be specified.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Host))
            {
                WriteWarning($"Host must be specified.");
                return;
            }
            var store = artifactService.CreateOrLoadStore(path, Website);
            // to support in-line specified files, add them to the artifact store temporarily
            if (File != null && File.Any())
            {
                foreach (var file in File)
                {
                    if (string.IsNullOrWhiteSpace(file)) continue;
                    if (file.Contains("*") || file.Contains("?"))
                    {
                        var filesResolved = Directory.GetFiles(Environment.CurrentDirectory, file, SearchOption.TopDirectoryOnly);
                        foreach (var resolvedFile in filesResolved)
                            artifactService.AddArtifact(Path.GetFullPath(resolvedFile));
                    }
                    else
                    {
                        var fullPath = Path.GetFullPath(file);
                        if (System.IO.File.Exists(fullPath))
                            artifactService.AddArtifact(fullPath);
                    }
                }
            }
            if (artifactService.ArtifactStore.Count == 0)
            {
                WriteWarning($"No artifacts have been added! Syntax: Add-Artifact -File ./examplefile.zip -Website example.com");
                return;
            }

            WriteVerbose($"Artifacts file: {store}");
            WriteVerbose($"Artifacts count: {artifactService.ArtifactStore.Count}");

            // upload artifacts
            try
            {
                Action<string> onVerbose = str => WriteVerbose(str);
                Action<string> onWarning = str => WriteWarning(str);
                Action<string> onInteractive = str =>
                {
                    var color = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(str);
                    Console.ForegroundColor = color;
                };
                deployService.Deploy(artifactService, Website, DeploymentScript, Host, Username, Password, Token, Port, Timeout, RequestTimeout, AutoCopy.ToBool(), AutoExtract.ToBool(), IgnoreCert.ToBool(), Interactive.ToBool(), onVerbose, onWarning, onInteractive);
            }
            catch (Exception ex)
            {
                var err = new ErrorRecord(ex, "Exception", ErrorCategory.FromStdErr, null);
                WriteError(err);
            }
        }
    }
}
