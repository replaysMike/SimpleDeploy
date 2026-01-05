using System.Diagnostics;
using System.Text;

namespace SimpleDeploy
{
    public class DeploymentQueueService : IDisposable
    {
        private readonly ILogger<DeploymentQueueService> _logger;
        private readonly Configuration _config;
        private readonly Queue<DeploymentQueueItem> _queue;
        private readonly IWebserverInterface _webserverInterface;
        private ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private Thread _queueThread;
        private CustomLogManager _domainLogManager;

        public DeploymentQueueService(ILogger<DeploymentQueueService> logger, Configuration config, CustomLogManager domainLogManager, IWebserverInterface fetcher)
        {
            _logger = logger;
            _config = config;
            _domainLogManager = domainLogManager;
            _queue = new Queue<DeploymentQueueItem>();
            _queueThread = new Thread(new ThreadStart(QueueThread));
            _queueThread.Start();
            _webserverInterface = fetcher;
        }

        public void Dispose()
        {
            _resetEvent.Set();
        }

        public async Task<bool> EnqueueDeploymentAsync(DeploymentQueueItem request)
        {
            // Placeholder implementation
            if (_queue.Any(x => x.JobId == request.JobId))
                return false;

            _queue.Enqueue(request);
            return true;
        }

        private void QueueThread()
        {
            while (!_resetEvent.WaitOne(250))
            {
                if (_queue.Count > 0)
                {
                    var item = _queue.Dequeue();
                    var task = Task.Run(() =>
                    {
                        QueueWorker(item);
                    });
                }
            }
        }

        private void QueueWorker(DeploymentQueueItem job)
        {
            // create a logger for the website
            var domainLogger = _domainLogManager.GetWebsiteLogger(job.Website);
            var websiteConfig = _config.Websites.Configurations.FirstOrDefault(x => x.Domain.Equals(job.Website, StringComparison.InvariantCultureIgnoreCase));

            _logger.LogInformation($"Processing job '{job.JobId}'");
            domainLogger.Info($"{job.JobId}| ==Deployment started==");

            var startTime = DateTime.UtcNow;
            var workingFolderRoot = _config.WorkingFolder;
            var workingWebsiteFolder = Path.Combine(workingFolderRoot, job.Website);
            var jobFolder = Path.Combine(workingWebsiteFolder, job.JobId);
            if (!Directory.Exists(workingFolderRoot))
                Directory.CreateDirectory(workingFolderRoot);
            if (!Directory.Exists(workingWebsiteFolder))
                Directory.CreateDirectory(workingWebsiteFolder);
            if (!Directory.Exists(jobFolder))
                Directory.CreateDirectory(jobFolder);

            // save each file to the job folder
            foreach (var artifact in job.Artifacts)
            {
                File.WriteAllBytes(Path.Combine(jobFolder, artifact.Filename), artifact.Data.ToArray());
            }

            var iisWebsite = _webserverInterface.GetWebsite(job.Website);
            if (iisWebsite == null)
                _logger.LogWarning($"'{job.Website}' website could not be found in web server.");
            else
            {
                var hostedWebsitePath = websiteConfig?.Path ?? iisWebsite?.PhysicalPath ?? string.Empty;
                _logger.LogInformation($"'{job.Website}' website found in web server [id '{iisWebsite.Id}', state '{iisWebsite.State}', path '{hostedWebsitePath}']");
            }

            if (websiteConfig == null || websiteConfig.StopBeforeDeploy)
            {
                // good, we know the destination physical path
                _logger.LogInformation($"[{job.JobId}] Stopping website '{job.Website}'...");
                domainLogger.Info($"{job.JobId}| Stopping website '{job.Website}'...");
                if (_webserverInterface.Stop(job.Website))
                {
                    _logger.LogInformation($"[{job.JobId}] '{job.Website}' stopped!");
                    domainLogger.Info($"{job.JobId}| '{job.Website}' stopped!");
                }
                else
                {
                    _logger.LogWarning($"[{job.JobId}] Failed to stop website '{job.Website}'!");
                    domainLogger.Warn($"{job.JobId}| Failed to stop website '{job.Website}'!");
                }
            }

            // determine deployment scripts
            // does it refer to a file in artifacts or a script directly?
            var deploymentScriptFilename = "deploy.ps1";
            var pathToDeployScript = Path.Combine(jobFolder, deploymentScriptFilename);
            if (job.Artifacts.Any(x => x.Filename.Equals(job.DeploymentScript)))
            {
                // deployment script is an artifact
                deploymentScriptFilename = job.DeploymentScript;
            }
            else
            {
                // deployment script is provided directly
                // save it to a file
                File.WriteAllText(pathToDeployScript, job.DeploymentScript ?? string.Empty);
            }

            // run deployment script
            RunScriptFromFile(domainLogger, job.JobId, pathToDeployScript);

            // copy files to output destination
            var files = Directory.GetFiles(jobFolder, "*", SearchOption.AllDirectories);
            _logger.LogInformation($"[{job.JobId}] Copying {files.Length} deployment files to '{iisWebsite?.PhysicalPath}'...");
            domainLogger.Info($"{job.JobId}| Copying {files.Length} deployment files to '{iisWebsite?.PhysicalPath}'...");
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(jobFolder, file);
                var destinationPath = Path.Combine(websiteConfig?.Path ?? iisWebsite?.PhysicalPath ?? string.Empty, relativePath);
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(destinationDir))
                    Directory.CreateDirectory(destinationDir ?? string.Empty);
                File.Copy(file, destinationPath, true);
            }
            _logger.LogInformation($"[{job.JobId}] Finished copying deployment files to '{iisWebsite?.PhysicalPath}'");
            domainLogger.Info($"{job.JobId}| Finished copying deployment files to '{iisWebsite?.PhysicalPath}'");

            // delete job files if requested
            if (_config.CleanupAfterDeploy)
                Cleanup(domainLogger, jobFolder);

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation($"Processing of job '{job.JobId}' complete in {elapsed}.");
            domainLogger.Info($"{job.JobId}| ==Deployment complete in {elapsed}==");

            if (websiteConfig == null || websiteConfig.StartAfterDeploy)
            {
                // restart the website in IIS
                if (_webserverInterface.Start(job.Website))
                {
                    _logger.LogInformation($"[{job.JobId}] '{job.Website}' started!");
                    domainLogger.Info($"{job.JobId}| '{job.Website}' started!");
                }
                else
                {
                    _logger.LogError($"[{job.JobId}] Failed to start website '{job.Website}'!");
                    domainLogger.Error($"{job.JobId}| Failed to start website '{job.Website}'!");
                }
            }

        }

        private void Cleanup(NLog.ILogger domainLogger, string jobFolder)
        {
            try
            {
                // sanity check, todo: improve this
                if (jobFolder != "C:\\" && jobFolder != "C:/" && jobFolder != "\\" && jobFolder != "/")
                    Directory.Delete(jobFolder, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to cleanup folder '{jobFolder}'");
                domainLogger.Error(ex, $"Failed to cleanup folder '{jobFolder}'");
            }
        }

        private void RunScriptFromFile(NLog.ILogger domainLogger, string jobId, string? scriptFile)
        {
            var psExtensions = new List<string> { ".ps1", ".psm1", ".psd1" };
            if (string.IsNullOrEmpty(scriptFile) || !File.Exists(scriptFile))
                return;

            var startInfo = new ProcessStartInfo();

            // add variables to environment for build script
            var workingDirectory = Path.GetDirectoryName(scriptFile);
            var scriptFilename = Path.GetFileName(scriptFile);
            var scriptFilenameWithTool = scriptFile;
            var scriptArguments = "";

            // execute as powershell if it's a powershell script
            if (psExtensions.Contains(Path.GetExtension(scriptFilename.ToLower())))
            {
                scriptFilenameWithTool = "powershell.exe";
                scriptArguments = $"-NoProfile -File \"{scriptFile}\"";
            }

            startInfo.EnvironmentVariables["Version"] = "1.0.0";
            startInfo.EnvironmentVariables["JobId"] = jobId;
            startInfo.EnvironmentVariables["JobPath"] = workingDirectory;
            startInfo.EnvironmentVariables["ScriptFilename"] = scriptFilename;

            startInfo.FileName = scriptFilenameWithTool;
            startInfo.WorkingDirectory = workingDirectory;
            startInfo.Arguments = scriptArguments;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            try
            {
                _logger.LogInformation($"[{jobId}] Running deployment script '{scriptFile}'...");
                domainLogger.Info($"{jobId}| Running deployment script '{scriptFile}'...");

                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        domainLogger.Info($"{jobId}|SCRIPT| {e.Data}");
                        outputBuilder.Append(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        domainLogger.Error($"{jobId}|SCRIPT| {e.Data}");
                        errorBuilder.Append(e.Data);
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                _logger.LogInformation($"[{jobId}] Deployment script completed with exit code ({process.ExitCode})");
                domainLogger.Info($"{jobId}| Deployment script completed with exit code ({process.ExitCode})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{jobId}] Error running script '{scriptFile}'");
            }
        }
    }
}
