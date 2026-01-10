//using Microsoft.Web.Administration;
using System.Diagnostics;
using System.Management.Automation;
using System.Text;
using System.Xml;

namespace SimpleDeploy
{
    public class IISWebserverInterface : IWebserverInterface
    {
        public ILogger<IWebserverInterface> _logger;
        public IISWebserverInterface(ILogger<IWebserverInterface> logger)
        {
            _logger = logger;
        }

        public List<ServerWebsite> GetWebsites()
        {
            /*var results = GetWebsitesViaPowershell();
            if (!results.Any())
            {
                results = GetWebsitesViaAppcmd();
            }*/
            var results = GetWebsitesViaAppcmd();
            return results;
        }

        public ServerWebsite? GetWebsite(string website)
        {
            // not using powershell or Microsoft.Web.Administration due to versioning issues with different versions of IIS possibly being installed
            /*var results = GetWebsitesViaPowershell();
            if (!results.Any())
            {
                results = GetWebsitesViaAppcmd();
            }*/
            var results = GetWebsitesViaAppcmd();
            return results
                .Where(x => x.Bindings.Contains(website, StringComparison.InvariantCultureIgnoreCase))
                .FirstOrDefault();
        }

        private List<ServerWebsite> GetWebsitesViaPowershell()
        {
            // get list of websites from IIS
            try
            {
                using var ps = PowerShell.Create();
                ps.AddCommand(@"Import-Module").AddArgument("IISAdministration");
                ps.Invoke();
                ps.Commands.Clear();
                ps.AddCommand("Get-IISSite");
                var results = ps.Invoke();
                if (ps.Streams.Error.Count > 0)
                {
                    // errors
                    foreach (var error in ps.Streams.Error)
                    {
                        _logger.LogError($"[{nameof(IISWebserverInterface)}] {error}");
                    }
                    return new();
                }
                var websites = new List<ServerWebsite>();
                foreach (var outputItem in results)
                {
                    var physicalPath = string.Empty;
                    var websitePath = string.Empty;
                    /*var applications = outputItem.Properties["Applications"].Value as ApplicationCollection;
                    var application = applications?.FirstOrDefault();
                    if (application != null)
                    {
                        var vdirs = application.VirtualDirectories;
                        var vdir = vdirs?.FirstOrDefault();
                        if (vdir != null)
                        {
                            physicalPath = vdir.PhysicalPath;
                            websitePath = vdir.Path;
                        }
                    }*/
                    var name = outputItem.Properties["Name"].Value.ToString();
                    var id = (long)outputItem.Properties["Id"].Value;
                    var bindings = outputItem.Properties["Bindings"].Value.ToString();
                    //var state = outputItem.Properties["State"].Value.ToString(); // slow to access
                    var website = new ServerWebsite()
                    {
                        Id = (int)id,
                        Name = name ?? string.Empty,
                        //State = state?.Value ?? string.Empty,
                        Bindings = bindings ?? string.Empty,
                        PhysicalPath = physicalPath ?? string.Empty,
                        Path = websitePath ?? string.Empty,
                    };
                }
                return websites;
            }
            catch (CommandNotFoundException ex)
            {
                _logger.LogError(ex, $"[{nameof(IISWebserverInterface)}] IIS Powershell extensions are not available.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[{nameof(IISWebserverInterface)}] Failed to get IIS websites via Powershell.");
            }
            return new();
        }

        private List<ServerWebsite> GetWebsitesViaAppcmd()
        {
            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            var appCmdPath = System.IO.Path.Combine(systemRoot, @"System32\inetsrv\appcmd.exe");
            ExecuteCommand(appCmdPath, "list sites /xml", out var sitesOutput, out var sitesError);
            ExecuteCommand(appCmdPath, "list vdir /xml", out var vdirOutput, out var vdirError);

            // process response
            var sitesDoc = new XmlDocument();
            sitesDoc.LoadXml(sitesOutput);
            var vdirDoc = new XmlDocument();
            vdirDoc.LoadXml(vdirOutput);
            var sites = sitesDoc.SelectNodes("//SITE");
            var results = new List<ServerWebsite>();
            // for each website, also get the physical path
            foreach (XmlNode site in sites)
            {
                var name = site?.Attributes?.GetNamedItem("SITE.NAME");
                var id = site?.Attributes?.GetNamedItem("SITE.ID");
                var bindings = site?.Attributes?.GetNamedItem("bindings");
                var state = site?.Attributes?.GetNamedItem("state");
                var vdir = vdirDoc.SelectSingleNode($"//VDIR[@APP.NAME='{name?.Value}/']"); // command appeands a '/' char
                var physicalPath = vdir?.Attributes?.GetNamedItem("physicalPath");
                var websitePath = vdir?.Attributes?.GetNamedItem("path");
                var website = new ServerWebsite()
                {
                    Id = int.Parse(id?.Value ?? "-1"),
                    Name = name?.Value ?? string.Empty,
                    State = state?.Value ?? string.Empty,
                    Bindings = bindings?.Value ?? string.Empty,
                    PhysicalPath = physicalPath?.Value ?? string.Empty,
                    Path = websitePath?.Value ?? string.Empty,
                };
                results.Add(website);
            }
            return results;
        }

        private void ExecuteCommand(string filename, string arguments, out string output, out string error)
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            var startInfo = new ProcessStartInfo();
            //startInfo.FileName = appCmdPath;
            startInfo.FileName = filename;
            startInfo.Arguments = arguments;
            //startInfo.Arguments = "list sites /xml";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.Append(e.Data);
                }
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.Append(e.Data);
                }
            };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            output = outputBuilder.ToString();
            error = errorBuilder.ToString();
        }

        public bool Stop(string website)
        {
            var websiteInfo = GetWebsite(website);
            if (websiteInfo == null)
                return false;
            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            var appCmdPath = System.IO.Path.Combine(systemRoot, @"System32\inetsrv\appcmd.exe");
            // first stop the website
            ExecuteCommand(appCmdPath, $"stop site /site.name:{websiteInfo.Name}", out var sitesOutput, out var sitesError);
            if (sitesOutput.Contains("successfully stopped") || sitesOutput.Contains("already stopped"))
            {
                // stop the apppool
                ExecuteCommand(appCmdPath, $"stop apppool /apppool.name:{websiteInfo.Name}", out sitesOutput, out sitesError);
                if (sitesOutput.Contains("successfully stopped") || sitesOutput.Contains("already stopped"))
                    return true;
            }
            return false;
        }

        public bool Start(string website)
        {
            var websiteInfo = GetWebsite(website);
            if (websiteInfo == null)
                return false;
            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
            var appCmdPath = System.IO.Path.Combine(systemRoot, @"System32\inetsrv\appcmd.exe");
            // first start the apppool
            ExecuteCommand(appCmdPath, $"start apppool /apppool.name:{websiteInfo.Name}", out var sitesOutput, out var sitesError);
            if (sitesOutput.Contains("successfully started") || sitesOutput.Contains("already started"))
            {
                // then start the website
                ExecuteCommand(appCmdPath, $"start site /site.name:{websiteInfo.Name}", out sitesOutput, out sitesError);
                if (sitesOutput.Contains("successfully started") || sitesOutput.Contains("already started"))
                    return true;
            }
            return false;
        }

        public bool Restart(string website)
        {
            Stop(website);
            Thread.Sleep(500);
            Start(website);
            return true;
        }
    }
}
