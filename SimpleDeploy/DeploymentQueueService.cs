using System;
using System.Diagnostics;
using System.IO.Compression;
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
            var workingFolderRoot = _config.WorkingFolder;
            var workingJobFolderRoot = Path.Combine(workingFolderRoot, _config.JobsFolder, job.DeploymentName);
            var backupFolderRoot = Path.Combine(_config.WorkingFolder, _config.BackupsFolder);
            var systemLogger = new LogWrapper(_logger, job);
            var domainLogger = _domainLogManager.GetDeploymentLogger(job, logOutput, workingJobFolderRoot);
            try
            {
                var websiteConfig = _config.DeploymentNames.Configurations.FirstOrDefault(x => x.Name.Equals(job.DeploymentName, StringComparison.InvariantCultureIgnoreCase));

                systemLogger.Info($"Processing '{job.DeploymentName}' as job '{job.JobId}'");

                // check if there is space on the file system for the deployment
                var freeSpaceBytes = Tools.CheckAvailableSpace(_config.WorkingFolder);
                if (freeSpaceBytes <= _config.MinFreeSpace)
                {
                    systemLogger.Error($"Not enough free space to deploy! {Tools.DisplayFileSize(_config.MinFreeSpace)} required, {Tools.DisplayFileSize(freeSpaceBytes)} available.");
                    return;
                }

                domainLogger.Info($" ==Deployment started==");

                var startTime = DateTime.UtcNow;
                var workingJobFolder = Path.Combine(workingJobFolderRoot, job.JobId);
                // make sure all folders required exist
                EnsureFoldersExist(workingFolderRoot, workingJobFolderRoot, workingJobFolder, backupFolderRoot);

                if (job.Artifacts.Count == 0)
                {
                    _logger.LogWarning($"No artifacts provided for deployment!");
                    domainLogger.Warn($"No artifacts provided for deployment!");
                    return;
                }

                // save artifacts to file
                var domain = SaveArtifacts(job, domainLogger, workingJobFolder);

                // fetch information from the web server
                ServerWebsite? iisWebsite = FetchIISWebsite(job, systemLogger, domainLogger, websiteConfig, domain);

                // auto extract zip files (if requested)
                var flowControl = AutoExtractCompressedFiles(job, systemLogger, domainLogger, websiteConfig, workingJobFolder);
                if (!flowControl) return;

                // determine deployment scripts
                flowControl = DetermineDeploymentScriptName(job, domainLogger, workingJobFolder, out var deploymentScriptFilename, out var pathToDeployScript);
                if (!flowControl) return;

                var destinationPath = !string.IsNullOrEmpty(websiteConfig?.Path) ? websiteConfig.Path : iisWebsite?.PhysicalPath;

                // stop website (if requested)
                StopWebsite(job, systemLogger, domainLogger, websiteConfig, domain);

                // clean destination path (if requested)
                CleanDestinationFolder(domainLogger, websiteConfig, destinationPath);

                // perform backups (if requested)
                flowControl = PerformBackups(job, domainLogger, websiteConfig, backupFolderRoot, destinationPath);
                if (!flowControl) return;

                // run deployment script
                RunScriptFromFile(systemLogger, domainLogger, job.JobId, pathToDeployScript, destinationPath);

                // copy files to output destination (if requested)
                CopyDeploymentToDestinationFolder(job, systemLogger, domainLogger, websiteConfig, workingJobFolder, deploymentScriptFilename, destinationPath);

                // delete job files (if requested)
                Cleanup(job, systemLogger, domainLogger, workingJobFolder);

                // start website (if requested)
                StartWebsite(job, systemLogger, domainLogger, websiteConfig, domain);

                var elapsed = DateTime.UtcNow - startTime;
                systemLogger.Info($"Processing of job '{job.JobId}' complete in {elapsed}.");
                domainLogger.Info($"==Deployment complete in {elapsed}==");
            }
            catch (Exception ex)
            {
                systemLogger.Error(ex, $"Error processing job!");
                domainLogger.Error(ex, $"Error processing job!");
            }
        }

        private static bool DetermineDeploymentScriptName(DeploymentQueueItem job, LogWrapper domainLogger, string jobFolder, out string deploymentScriptFilename, out string pathToDeployScript)
        {
            // does it refer to a file in artifacts or a script directly?
            if (string.IsNullOrEmpty(job.DeploymentScript))
            {
                // no deployment script specified, try to determine the name automatically if it was included in the artifacts
                var defaultScripts = new List<string> { "deploy.ps1", "deploy.psm1", "deploy.psc1", "deploy.cmd", "deploy.bat" };
                if (job.Artifacts.Any(x => defaultScripts.Contains(Path.GetFileName(x.Filename), StringComparer.InvariantCultureIgnoreCase)))
                {
                    job.DeploymentScript = job.Artifacts.First(x => defaultScripts.Contains(Path.GetFileName(x.Filename), StringComparer.InvariantCultureIgnoreCase)).Filename;
                    domainLogger.Info($"No deployment script specified, using default from artifacts: '{job.DeploymentScript}'");
                }
            }
            deploymentScriptFilename = string.Empty;
            pathToDeployScript = jobFolder;
            if (job.Artifacts.Any(x => Path.GetFileName(x.Filename).Equals(Path.GetFileName(job.DeploymentScript), StringComparison.InvariantCultureIgnoreCase)))
            {
                // deployment script is an artifact file
                deploymentScriptFilename = job.DeploymentScript ?? string.Empty;
                pathToDeployScript = Path.Combine(jobFolder, Path.GetFileName(deploymentScriptFilename));
            }
            else if (job.DeploymentScript?.Length > 0)
            {
                // deployment script is provided directly as a string
                // save it to a file
                deploymentScriptFilename = "deploy.ps1";
                pathToDeployScript = Path.Combine(jobFolder, deploymentScriptFilename);
                domainLogger.Info($"Raw deployment script received, saving deployment script as {deploymentScriptFilename} ({job.DeploymentScript?.Length} bytes)");
                File.WriteAllText(pathToDeployScript, deploymentScriptFilename);
            }
            else
            {
                domainLogger.Error($"Could not determine deployment script - artifacts specified a deployment script named '{job.DeploymentScript}' but was not found and no direct script was provided.");
                return false;
            }

            return true;
        }

        private void StartWebsite(DeploymentQueueItem job, LogWrapper systemLogger, LogWrapper domainLogger, DeploymentNameConfiguration? websiteConfig, string domain)
        {
            if ((websiteConfig != null && websiteConfig.IIS && websiteConfig.StartAfterDeploy == true) || job.IIS)
            {
                // restart the website in IIS
                if (_webserverInterface.Start(domain))
                {
                    systemLogger.Info($"'{domain}' website started!");
                    domainLogger.Info($"'{domain}' website started!");
                }
                else
                {
                    systemLogger.Error($"Failed to start website '{domain}'!");
                    domainLogger.Error($"Failed to start website '{domain}'!");
                }
            }
        }

        private void StopWebsite(DeploymentQueueItem job, LogWrapper systemLogger, LogWrapper domainLogger, DeploymentNameConfiguration? websiteConfig, string domain)
        {
            if ((websiteConfig != null && websiteConfig.IIS && websiteConfig.StopBeforeDeploy) || job.IIS)
            {
                // stop the website in the webserver
                systemLogger.Info($"Stopping website '{domain}'...");
                domainLogger.Info($"Stopping website '{domain}'...");
                if (_webserverInterface.Stop(domain))
                {
                    systemLogger.Info($"'{domain}' website stopped!");
                    domainLogger.Info($"'{domain}' website stopped!");
                }
                else
                {
                    systemLogger.Warn($"Failed to stop website '{domain}'!");
                    domainLogger.Warn($"Failed to stop website '{domain}'!");
                }
            }
        }

        private static void CopyDeploymentToDestinationFolder(DeploymentQueueItem job, LogWrapper systemLogger, LogWrapper domainLogger, DeploymentNameConfiguration? websiteConfig, string jobFolder, string deploymentScriptFilename, string destinationPath)
        {
            if (!string.IsNullOrEmpty(destinationPath) && ((websiteConfig != null && websiteConfig.IIS && websiteConfig.AutoCopy) || (job.IIS && job.AutoCopy)))
            {
                var files = Directory.GetFiles(jobFolder, "*", SearchOption.AllDirectories);
                systemLogger.Info($"Copying {files.Length} deployment files to '{destinationPath}'...");
                domainLogger.Info($"Copying {files.Length} deployment files to '{destinationPath}'...");
                foreach (var file in files)
                {
                    // don't copy the deployment script to the deployment destination
                    if (Path.GetFileName(file).Equals(deploymentScriptFilename)) continue;

                    var relativePath = Path.GetRelativePath(jobFolder, file);
                    var outputPath = Path.Combine(destinationPath, relativePath);
                    var destinationDir = Path.GetDirectoryName(outputPath);
                    if (!Directory.Exists(destinationDir))
                        Directory.CreateDirectory(destinationDir ?? string.Empty);
                    File.Copy(file, outputPath, true);
                }
                domainLogger.Info($"Finished copying deployment files to '{destinationPath}'");
            }
            else
            {
                domainLogger.Info($"Auto-copy of deployment files skipped due to job setting.");
            }
        }

        private static void CleanDestinationFolder(LogWrapper domainLogger, DeploymentNameConfiguration? websiteConfig, string? destinationPath)
        {
            if ((websiteConfig != null && websiteConfig.CleanBeforeDeploy) && !string.IsNullOrEmpty(destinationPath))
            {
                // delete the destination path website files before deploying
                var directoryInfo = new DirectoryInfo(destinationPath);
                foreach (var file in directoryInfo.GetFiles())
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception ex)
                    {
                        domainLogger.Warn(ex, $"Failed to delete file '{file.FullName}'");
                    }
                }
                foreach (var folder in directoryInfo.GetDirectories())
                {
                    try
                    {
                        folder.Delete(true);
                    }
                    catch (Exception ex)
                    {
                        domainLogger.Warn(ex, $"Failed to delete folder '{folder.FullName}'");
                    }
                }
            }
        }

        private static bool AutoExtractCompressedFiles(DeploymentQueueItem job, LogWrapper systemLogger, LogWrapper domainLogger, DeploymentNameConfiguration? websiteConfig, string jobFolder)
        {
            if ((websiteConfig != null && websiteConfig.AutoExtract) || job.AutoExtract)
            {
                // extract any zip files (todo: add other archive types?)
                var zipFiles = Directory.GetFiles(jobFolder, "*.zip", SearchOption.AllDirectories);
                domainLogger.Info($"Found {zipFiles.Length} zip files to extract...");
                foreach (var zipFile in zipFiles)
                {
                    try
                    {
                        var extractPath = Path.GetDirectoryName(zipFile) ?? jobFolder;
                        domainLogger.Info($" - Extracting zip file '{zipFile}' to '{extractPath}'...");
                        var extractStartTime = DateTime.UtcNow;
                        System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, extractPath ?? jobFolder, true);
                        var extractElapsed = DateTime.UtcNow - extractStartTime;
                        domainLogger.Info($" - Finished extracting zip file '{zipFile}' in {extractElapsed}!");
                        try
                        {
                            // make sure to remove the zip file after extraction
                            File.Delete(zipFile);
                        }
                        catch (Exception innerEx)
                        {
                            // not fatal
                            systemLogger.Warn(innerEx, $"Error deleting zip file '{zipFile}' after extraction");
                            domainLogger.Warn(innerEx, $"Error deleting zip file '{zipFile}' after extraction");
                        }
                    }
                    catch (Exception ex)
                    {
                        // fatal
                        systemLogger.Error(ex, $"Error extracting zip file '{zipFile}'");
                        domainLogger.Error(ex, $"Error extracting zip file '{zipFile}'");
                        return false;
                    }
                }
            }
            else
            {
                domainLogger.Info($"Auto-extract of zip files skipped due to job setting.");
            }

            return true;
        }

        private ServerWebsite? FetchIISWebsite(DeploymentQueueItem job, LogWrapper systemLogger, LogWrapper domainLogger, DeploymentNameConfiguration? websiteConfig, string domain)
        {
            ServerWebsite? iisWebsite = null;
            if (!string.IsNullOrEmpty(domain) && ((websiteConfig != null && websiteConfig.IIS) || job.IIS))
            {
                domainLogger.Info($"Fetching information from webserver for '{domain}'...");
                iisWebsite = _webserverInterface.GetWebsite(domain);
                if (iisWebsite == null)
                    systemLogger.Warn($"'{domain}' website could not be found in web server.");
                else
                {
                    var hostedWebsitePath = websiteConfig?.Path ?? iisWebsite.PhysicalPath;
                    systemLogger.Info($"'{domain}' website found in web server [id '{iisWebsite.Id}', state '{iisWebsite.State}', path '{hostedWebsitePath}']");
                }
            }

            return iisWebsite;
        }

        private static void EnsureFoldersExist(string workingFolderRoot, string workingWebsiteFolder, string jobFolder, string backupFolderRoot)
        {
            if (!Directory.Exists(workingFolderRoot))
                Directory.CreateDirectory(workingFolderRoot);
            if (!Directory.Exists(workingWebsiteFolder))
                Directory.CreateDirectory(workingWebsiteFolder);
            if (!Directory.Exists(jobFolder))
                Directory.CreateDirectory(jobFolder);
            if (!Directory.Exists(backupFolderRoot))
                Directory.CreateDirectory(backupFolderRoot);
        }

        private static string SaveArtifacts(DeploymentQueueItem job, LogWrapper domainLogger, string jobFolder)
        {
            // use the domain specified, otherwise fallback to the deployment name
            var domain = !string.IsNullOrEmpty(job.Domain) ? job.Domain : job.DeploymentName;
            domainLogger.Info($"Artifacts ({job.Artifacts.Count}): {string.Join(", ", job.Artifacts.Select(x => Path.GetFileName(x.Filename)))}");
            // save each file to the job folder
            foreach (var artifact in job.Artifacts)
            {
                domainLogger.Info($" - Saving artifact {artifact.Filename} ({artifact.Data.Length} bytes)");
                File.WriteAllBytes(Path.Combine(jobFolder, artifact.Filename), artifact.Data.ToArray());
                artifact.Data.Dispose();
            }

            return domain;
        }

        private bool PerformBackups(DeploymentQueueItem job, LogWrapper domainLogger, DeploymentNameConfiguration? websiteConfig, string backupFolderRoot, string? destinationPath)
        {
            if ((websiteConfig != null && websiteConfig.Backup) && !string.IsNullOrEmpty(destinationPath))
            {
                // backup the website before deploying
                var backupFolder = Path.Combine(backupFolderRoot, job.DeploymentName);
                if (!Directory.Exists(backupFolder))
                    Directory.CreateDirectory(backupFolder);
                // zip up the current contents and save to the backup folder
                var backupFile = Path.Combine(backupFolder, $"{job.DeploymentName}-{job.JobId}.zip");
                try
                {
                    domainLogger.Info($"Backing up existing destination path files...");
                    ZipFile.CreateFromDirectory(destinationPath, backupFile, CompressionLevel.Optimal, true);
                    domainLogger.Info($"Backed up existing destination files as '{backupFile}'.");
                }
                catch (Exception ex)
                {
                    domainLogger.Error(ex, $"Failed to backup destination path before deployment, aborting deployment!");
                    return false;
                }

                // success creating backup, delete the oldest backups as required
                var directoryInfo = new DirectoryInfo(backupFolder);
                var backupFiles = directoryInfo.GetFiles("*.zip", SearchOption.TopDirectoryOnly);
                var fileCount = 0;
                foreach (var file in backupFiles.OrderByDescending(x => x.CreationTimeUtc))
                {
                    fileCount++;
                    if (fileCount > _config.MaxBackupFiles)
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch (Exception ex)
                        {
                            domainLogger.Error(ex, $"Failed to delete historical backup file '{file.FullName}'");
                        }
                    }
                }
            }

            return true;
        }

        private void Cleanup(DeploymentQueueItem job, LogWrapper systemLogger, LogWrapper domainLogger, string jobFolder)
        {
            if (_config.CleanupAfterDeploy)
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
                    systemLogger.Error(ex, $"Failed to cleanup folder '{jobFolder}'");
                    domainLogger.Error(ex, $"Failed to cleanup folder '{jobFolder}'");
                }
            }
        }

        private void RunScriptFromFile(LogWrapper systemLogger, LogWrapper domainLogger, string jobId, string? scriptFile, string destinationPath)
        {
            var psExtensions = new List<string> { ".ps1", ".psm1", ".psd1" };
            if (string.IsNullOrEmpty(scriptFile) || !File.Exists(scriptFile))
                return;

            var startInfo = new ProcessStartInfo();

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

            // add variables to environment for build script
            var gitCommit = TryGetGitCommit();
            var gitCommitMessage = TryGetGitCommitMessage();
            startInfo.EnvironmentVariables["SD_BUILD_VERSION"] = "1.0.0";
            startInfo.EnvironmentVariables["SD_JOB_ID"] = jobId;
            startInfo.EnvironmentVariables["SD_BUILD_PATH"] = workingDirectory;
            startInfo.EnvironmentVariables["SD_DESTINATION_PATH"] = destinationPath;
            startInfo.EnvironmentVariables["SD_SCRIPT_FILENAME"] = scriptFilename;
            startInfo.EnvironmentVariables["SD_REPO_COMMIT"] = gitCommit;
            startInfo.EnvironmentVariables["SD_REPO_COMMIT_SHORT"] = gitCommit != null && gitCommit.Length >= 7 ? gitCommit.Substring(0, 7) : null;
            startInfo.EnvironmentVariables["SD_REPO_COMMIT_MESSAGE"] = gitCommitMessage;

            startInfo.FileName = scriptFilenameWithTool;
            startInfo.WorkingDirectory = workingDirectory;
            startInfo.Arguments = scriptArguments;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            try
            {
                systemLogger.Info($"Running deployment script '{scriptFile}'...");
                domainLogger.Info($"Running deployment script '{scriptFile}'...");

                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        domainLogger.Info($"SCRIPT| {e.Data}");
                        outputBuilder.Append(e.Data);
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        domainLogger.Error($"SCRIPT| {e.Data}");
                        errorBuilder.Append(e.Data);
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                systemLogger.Info($"Deployment script completed with exit code ({process.ExitCode})");
                domainLogger.Info($"Deployment script completed with exit code ({process.ExitCode})");
            }
            catch (Exception ex)
            {
                systemLogger.Error(ex, $"Error running script '{scriptFile}'");
                domainLogger.Error(ex, $"Error running script '{scriptFile}'");
            }
        }

        private string TryGetGitCommit()
        {
            try
            {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = "git.exe";
                startInfo.WorkingDirectory = Environment.CurrentDirectory;
                startInfo.Arguments = "rev-parse HEAD";
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
            catch (Exception) { }
            return string.Empty;
        }

        private string TryGetGitCommitMessage()
        {
            try
            {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = "git.exe";
                startInfo.WorkingDirectory = Environment.CurrentDirectory;
                startInfo.Arguments = "log -1 --pretty=format:\"%B\"";
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                using var process = new Process { StartInfo = startInfo };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
            catch (Exception) { }
            return string.Empty;
        }
    }
}
