using SimpleDeploy.Cmdlet.Services;
using System.Management.Automation;
using System.Runtime.InteropServices;

namespace SimpleDeploy.Cmdlet
{
    [Cmdlet(VerbsCommon.Add, "Artifact")]
    public class AddArtifactCmdlet : System.Management.Automation.PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the filename of an artifact")]
        [Alias("f")]
        public string File { get; set; } = string.Empty;

        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the website")]
        [Alias("w", "domain", "d")]
        public string Website { get; set; } = string.Empty;

        protected override void ProcessRecord()
        {
            var artifactService = new ArtifactService();
            var path = SessionState.Path.CurrentLocation.Path;
            if (string.IsNullOrWhiteSpace(File))
            {
                WriteWarning($"File must be specified.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Website))
            {
                WriteWarning($"Website must be specified.");
                return;
            }
            var filePath = Path.GetFullPath(Path.Combine(path, File));
            if (!System.IO.File.Exists(filePath))
            {
                WriteWarning($"File '{filePath}' does not exist!");
                return;
            }
            var store = artifactService.CreateOrLoadStore(path, Website);
            WriteVerbose($"Artifacts file: {store}");

            // add to store
            if (artifactService.AddArtifact(filePath))
            {
                // success
                WriteVerbose($"Artifact '{filePath}' has been added.");
                // overwrite the temporary store
                artifactService.WriteArtifactDatabase(store);
            }

            WriteWarning($"File '{filePath}' already added to artifacts.");
            return;
        }
    }

    [Cmdlet(VerbsCommon.Remove, "Artifact")]
    public class RemoveArtifactCmdlet : System.Management.Automation.PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the filename of an artifact")]
        [Alias("f")]
        public string File { get; set; } = string.Empty;

        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "Specify the website")]
        [Alias("w", "domain", "d")]
        public string Website { get; set; } = string.Empty;

        protected override void ProcessRecord()
        {
            var artifactService = new ArtifactService();
            var path = SessionState.Path.CurrentLocation.Path;
            if (string.IsNullOrWhiteSpace(File))
            {
                WriteWarning($"File must be specified.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Website))
            {
                WriteWarning($"Website must be specified.");
                return;
            }
            var filePath = Path.GetFullPath(Path.Combine(path, File));
            if (!System.IO.File.Exists(filePath))
            {
                WriteWarning($"File '{filePath}' does not exist!");
                return;
            }
            var store = artifactService.CreateOrLoadStore(path, Website);
            if (artifactService.ArtifactStore.Count == 0)
            {
                WriteWarning($"No artifacts have been added! Syntax: Add-Artifact -File ./examplefile.zip -Website example.com");
                return;
            }
            if (artifactService.RemoveArtifact(filePath))
            {
                // success
                WriteVerbose($"Artifact '{filePath}' has been removed.");
                // overwrite the temporary store
                artifactService.WriteArtifactDatabase(store);
            }

            WriteWarning($"Artifact '{filePath}' has not been added, cannot remove.");
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
            WriteVerbose($"Artifacts file: {store}");
            WriteVerbose($"Artifacts count: {artifactService.ArtifactStore.Count}");
            if (artifactService.ArtifactStore.Count == 0)
            {
                WriteWarning($"No artifacts have been added! Syntax: Add-Artifact -File ./examplefile.zip -Website example.com");
                return;
            }
            // upload artifacts
            try
            {
                Action<string> onVerbose = str => WriteVerbose(str);
                Action<string> onWarning = str => WriteWarning(str);
                deployService.Deploy(artifactService, Website, DeploymentScript, Host, Username, Password, Token, Port, Timeout, RequestTimeout, AutoCopy.ToBool(), AutoExtract.ToBool(), IgnoreCert.ToBool(), onVerbose, onWarning);
            }
            catch (Exception ex)
            {
                var err = new ErrorRecord(ex, "Exception", ErrorCategory.FromStdErr, null);
                WriteError(err);
            }
        }
    }
}
