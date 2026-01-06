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
            try
            {
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

                if (job.Artifacts.Count == 0)
                {
                    _logger.LogWarning($"[{job.JobId}] No artifacts provided for deployment!");
                    domainLogger.Warn($"{job.JobId}| No artifacts provided for deployment!");
                    return;
                }

                // save each file to the job folder
                domainLogger.Info($"Artifacts ({job.Artifacts.Count}): {string.Join(", ", job.Artifacts.Select(x => Path.GetFileName(x.Filename)))}");
                foreach (var artifact in job.Artifacts)
                {
                    domainLogger.Info($"Saving artifact {artifact.Filename} ({artifact.Data.Length} bytes)");
                    File.WriteAllBytes(Path.Combine(jobFolder, artifact.Filename), artifact.Data.ToArray());
                    artifact.Data.Dispose();
                }

                domainLogger.Info($"Fetching information from webserver...");
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
                    // stop the website in the webserver
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

                if (job.AutoExtract)
                {
                    // extract any zip files (todo: add other archive types?)
                    var zipFiles = Directory.GetFiles(jobFolder, "*.zip", SearchOption.AllDirectories);
                    domainLogger.Info($"Found {zipFiles.Length} zip files to extract...");
                    foreach (var zipFile in zipFiles)
                    {
                        try
                        {
                            var extractPath = Path.GetDirectoryName(zipFile) ?? jobFolder;
                            domainLogger.Info($"Extracting zip file '{zipFile}' to '{extractPath}'...");
                            var extractStartTime = DateTime.UtcNow;
                            System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, extractPath ?? jobFolder, true);
                            var extractElapsed = DateTime.UtcNow - extractStartTime;
                            domainLogger.Info($"Finished extracting zip file '{zipFile}' in {extractElapsed}!");
                            try
                            {
                                // make sure to remove the zip file after extraction
                                File.Delete(zipFile);
                            }
                            catch (Exception innerEx)
                            {
                                // not fatal
                                _logger.LogWarning(innerEx, $"[{job.JobId}] Error deleting zip file '{zipFile}' after extraction");
                                domainLogger.Warn(innerEx, $"{job.JobId}| Error deleting zip file '{zipFile}' after extraction");
                            }
                        }
                        catch (Exception ex)
                        {
                            // fatal
                            _logger.LogError(ex, $"[{job.JobId}] Error extracting zip file '{zipFile}'");
                            domainLogger.Error(ex, $"{job.JobId}| Error extracting zip file '{zipFile}'");
                            return;
                        }
                    }
                }
                else
                {
                    domainLogger.Info($"Auto-extract of zip files skipped due to job setting.");
                }

                // determine deployment scripts
                // does it refer to a file in artifacts or a script directly?
                var deploymentScriptFilename = string.Empty;
                var pathToDeployScript = jobFolder;
                if (job.Artifacts.Any(x => x.Filename.Equals(job.DeploymentScript, StringComparison.InvariantCultureIgnoreCase)))
                {
                    // deployment script is an artifact
                    deploymentScriptFilename = job.DeploymentScript;
                    pathToDeployScript = Path.Combine(jobFolder, job.DeploymentScript);
                }
                else if (job.DeploymentScript?.Length > 0)
                {
                    // deployment script is provided directly
                    // save it to a file
                    domainLogger.Info($"Saving deployment script {job.DeploymentScript} ({job.DeploymentScript?.Length} bytes)");
                    deploymentScriptFilename = "deploy.ps1";
                    pathToDeployScript = Path.Combine(jobFolder, deploymentScriptFilename);
                    File.WriteAllText(pathToDeployScript, job.DeploymentScript ?? string.Empty);
                }
                else
                {
                    domainLogger.Error($"Could not determine deployment script - artifacts specified a deployment script named '{job.DeploymentScript}' but was not found and no direct script was provided.");
                    return;
                }

                // run deployment script
                RunScriptFromFile(domainLogger, job.JobId, pathToDeployScript);

                // copy files to output destination
                if (job.AutoCopy)
                {
                    var files = Directory.GetFiles(jobFolder, "*", SearchOption.AllDirectories);
                    _logger.LogInformation($"[{job.JobId}] Copying {files.Length} deployment files to '{iisWebsite?.PhysicalPath}'...");
                    domainLogger.Info($"{job.JobId}| Copying {files.Length} deployment files to '{iisWebsite?.PhysicalPath}'...");
                    foreach (var file in files)
                    {
                        // don't copy the deployment script to the deployment destination
                        if (Path.GetFileName(file).Equals(deploymentScriptFilename)) continue;

                        var relativePath = Path.GetRelativePath(jobFolder, file);
                        var destinationPath = Path.Combine(websiteConfig?.Path ?? iisWebsite?.PhysicalPath ?? string.Empty, relativePath);
                        var destinationDir = Path.GetDirectoryName(destinationPath);
                        if (!Directory.Exists(destinationDir))
                            Directory.CreateDirectory(destinationDir ?? string.Empty);
                        File.Copy(file, destinationPath, true);
                    }
                    domainLogger.Info($"{job.JobId}| Finished copying deployment files to '{iisWebsite?.PhysicalPath}'");
                }
                else
                {
                    domainLogger.Info($"{job.JobId}| Auto-copy of deployment files skipped due to job setting.");
                }

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
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{job.JobId}] Error processing job!");
                domainLogger.Error(ex, $"{job.JobId}| Error processing job!");
            }
        }

        private void Cleanup(NLog.ILogger domainLogger, string jobFolder)
        {
            try
            {
                domainLogger.Info($"Cleaning up job folder '{jobFolder}'...");
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
