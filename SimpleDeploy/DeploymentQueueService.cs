using System.Diagnostics;
using System.Text;

namespace SimpleDeploy
{
    public class DeploymentQueueService : IDisposable
    {
        private readonly ILogger<DeploymentQueueService> _logger;
        private readonly Configuration _config;
        private readonly Queue<JobTask> _queue;
        private readonly Dictionary<string, JobTask> _jobTasks;
        private readonly IWebserverInterface _webserverInterface;
        private ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private Thread _queueThread;
        private Thread _cleanupThread;
        private CustomLogManager _domainLogManager;

        public DeploymentQueueService(ILogger<DeploymentQueueService> logger, Configuration config, CustomLogManager domainLogManager, IWebserverInterface fetcher)
        {
            _logger = logger;
            _config = config;
            _domainLogManager = domainLogManager;
            _jobTasks = new();
            _queue = new Queue<JobTask>();
            _queueThread = new Thread(new ThreadStart(QueueThread));
            _queueThread.Name = "Queue management thread";
            _queueThread.Start();
            _cleanupThread = new Thread(new ThreadStart(CleanupThread));
            _cleanupThread.Name = "Cleanup thread";
            _cleanupThread.Start();
            _webserverInterface = fetcher;
        }

        public void Dispose()
        {
            _resetEvent.Set();
        }

        public async Task<string?> EnqueueDeploymentAsync(DeploymentQueueItem request)
        {
            if (_queue.Any(x => x.Request.JobId == request.JobId))
                return null;

            var sb = new StringBuilder();
            var task = new Task(() =>
            {
                WorkerJob(request, sb);
            });

            // keep track of the job task for output retrieval
            var jobTask = new JobTask(request, task, sb);
            _jobTasks.Add(request.JobId, jobTask);

            _queue.Enqueue(jobTask);
            return request.JobId;
        }

        public async Task<StringBuilder?> WaitForCompletionAsync(string jobId, TimeSpan timeout)
        {
            if (_jobTasks.ContainsKey(jobId))
            {
                var jobTask = _jobTasks[jobId];
                try
                {
                    await jobTask.Task.WaitAsync(timeout);
                }
                catch (TimeoutException)
                {
                    // timeout
                    jobTask.Output.AppendLine($"Job '{jobId}' timeout waiting for completion response {timeout}.");
                    return null;
                }
                //_logger.LogInformation($"Job '{jobId}' completed with log length {jobTask.Output.Length} bytes");
                return jobTask.Output;
            }
            return null;
        }

        private void CleanupThread()
        {
            // once per minute , clean up any completed jobs older than 5 minutes
            while (!_resetEvent.WaitOne(60 * 1000))
            {
                try
                {
                    var completedJobs = _jobTasks.Values.Where(x => x.Completed.HasValue && (DateTime.UtcNow - x.Completed.Value).TotalMinutes > 5).ToList();
                    foreach (var job in completedJobs)
                    {
                        job.Output.Clear();
                        job.Task.Dispose();
                        _jobTasks.Remove(job.Request.JobId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during cleanup of completed jobs.");
                }
            }
            _logger.LogDebug($"{_cleanupThread.Name} shutdown.");
        }

        private void QueueThread()
        {
            while (!_resetEvent.WaitOne(250))
            {
                if (_queue.Count > 0)
                {
                    var jobTask = _queue.Dequeue();
                    // execute the work
                    jobTask.Started = DateTime.UtcNow;
                    try
                    {
                        jobTask.Task.RunSynchronously();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error executing job '{jobTask.Request.JobId}'");
                    }
                    finally
                    {
                        jobTask.Completed = DateTime.UtcNow;
                    }
                }
            }
            _logger.LogDebug($"{_queueThread.Name} shutdown.");
        }

        private void WorkerJob(DeploymentQueueItem job, StringBuilder logOutput)
        {
            // create a logger for the website
            var domainLogger = _domainLogManager.GetWebsiteLogger(job.Website, logOutput);
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
                domainLogger.Info($"{job.JobId}| Artifacts ({job.Artifacts.Count}): {string.Join(", ", job.Artifacts.Select(x => Path.GetFileName(x.Filename)))}");
                foreach (var artifact in job.Artifacts)
                {
                    domainLogger.Info($"{job.JobId}| - Saving artifact {artifact.Filename} ({artifact.Data.Length} bytes)");
                    File.WriteAllBytes(Path.Combine(jobFolder, artifact.Filename), artifact.Data.ToArray());
                    artifact.Data.Dispose();
                }

                domainLogger.Info($"{job.JobId}| Fetching information from webserver...");
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
                    domainLogger.Info($"{job.JobId}| Found {zipFiles.Length} zip files to extract...");
                    foreach (var zipFile in zipFiles)
                    {
                        try
                        {
                            var extractPath = Path.GetDirectoryName(zipFile) ?? jobFolder;
                            domainLogger.Info($"{job.JobId}| - Extracting zip file '{zipFile}' to '{extractPath}'...");
                            var extractStartTime = DateTime.UtcNow;
                            System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, extractPath ?? jobFolder, true);
                            var extractElapsed = DateTime.UtcNow - extractStartTime;
                            domainLogger.Info($"{job.JobId}| - Finished extracting zip file '{zipFile}' in {extractElapsed}!");
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
                if (string.IsNullOrEmpty(job.DeploymentScript))
                {
                    // no deployment script specified, try to determine the name automatically if it was included in the artifacts
                    var defaultScripts = new List<string> { "deploy.ps1", "deploy.psm1", "deploy.psc1", "deploy.cmd", "deploy.bat" };
                    if (job.Artifacts.Any(x => defaultScripts.Contains(x.Filename, StringComparer.InvariantCultureIgnoreCase)))
                    {
                        job.DeploymentScript = job.Artifacts.First(x => defaultScripts.Contains(x.Filename, StringComparer.InvariantCultureIgnoreCase)).Filename;
                        domainLogger.Info($"{job.JobId}| No deployment script specified, using default from artifacts: '{job.DeploymentScript}'");
                    }
                }
                var deploymentScriptFilename = string.Empty;
                var pathToDeployScript = jobFolder;
                if (job.Artifacts.Any(x => x.Filename.Equals(job.DeploymentScript, StringComparison.InvariantCultureIgnoreCase)))
                {
                    // deployment script is an artifact file
                    deploymentScriptFilename = job.DeploymentScript;
                    pathToDeployScript = Path.Combine(jobFolder, job.DeploymentScript);
                }
                else if (job.DeploymentScript?.Length > 0)
                {
                    // deployment script is provided directly as a string
                    // save it to a file
                    domainLogger.Info($"{job.JobId}| Saving deployment script {job.DeploymentScript} ({job.DeploymentScript?.Length} bytes)");
                    deploymentScriptFilename = "deploy.ps1";
                    pathToDeployScript = Path.Combine(jobFolder, deploymentScriptFilename);
                    File.WriteAllText(pathToDeployScript, job.DeploymentScript ?? string.Empty);
                }
                else
                {
                    domainLogger.Error($"{job.JobId}| Could not determine deployment script - artifacts specified a deployment script named '{job.DeploymentScript}' but was not found and no direct script was provided.");
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
                    Cleanup(job, domainLogger, jobFolder);

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

        private void Cleanup(DeploymentQueueItem job, LogWrapper domainLogger, string jobFolder)
        {
            try
            {
                domainLogger.Info($"{job.JobId}|Cleaning up job folder '{jobFolder}'...");
                // sanity check, todo: improve this
                if (jobFolder != "C:\\" && jobFolder != "C:/" && jobFolder != "\\" && jobFolder != "/")
                    Directory.Delete(jobFolder, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{job.JobId}] Failed to cleanup folder '{jobFolder}'");
                domainLogger.Error(ex, $"{job.JobId}| Failed to cleanup folder '{jobFolder}'");
            }
        }

        private void RunScriptFromFile(LogWrapper domainLogger, string jobId, string? scriptFile)
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
