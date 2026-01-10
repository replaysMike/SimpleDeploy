using CommandLine;
using Deploy;
using SimpleDeploy.Cmdlet.Services;
using System.Management;

var exitCode = ExitCode.Success;
Parser.Default.ParseArguments<Options>(args)
    .WithParsed<Options>(o =>
    {
        if (o.AddArtifact)
        {
            exitCode = AddArtifact(o);
        }
        else if (o.RemoveArtifact)
        {
            exitCode = RemoveArtifact(o);
        }
        else if (o.GetArtifacts)
        {
            exitCode = GetArtifacts(o);
        }
        else if (o.Deploy)
        {
            exitCode = Deploy(o);
        }
        else
        {
            exitCode = ExitCode.InvalidArguments;
            Console.WriteLine("Error: no command/unknown command specified");
            Console.WriteLine("Example (add artifact): Deploy.exe -a -f examplefile.zip -n example.com");
            Console.WriteLine("Example (deploy): Deploy.exe -d -h localhost -n example.com");
        }
    });
return (int)exitCode;

ExitCode AddArtifact(Options options)
{
    var artifactService = new ArtifactService();
    var path = Environment.CurrentDirectory;
    if (options.File == null || !options.File.Any())
    {
        Console.WriteLine("Error: Artifact file(s) must be specified (-f).");
        return ExitCode.InvalidArguments;
    }
    if (string.IsNullOrWhiteSpace(options.DeploymentName))
    {
        Console.WriteLine("Error: A deployment name must be specified (-n).");
        return ExitCode.InvalidArguments;
    }
    // check if files exist
    foreach (var file in options.File)
    {
        var filePath = Path.GetFullPath(Path.Combine(path, file));
        if (!System.IO.File.Exists(filePath))
        {
            Console.WriteLine($"File '{filePath}' does not exist!");
            return ExitCode.InvalidArguments;
        }
    }
    var store = artifactService.CreateOrLoadStore(path, options.DeploymentName);
    if (options.Verbose) Console.WriteLine($"Artifacts store: {store}");
    // add to store
    foreach (var file in options.File)
    {
        var filePath = Path.GetFullPath(Path.Combine(path, file));
        if (!artifactService.AddArtifact(filePath))
        {
            Console.WriteLine($"File '{filePath}' already added to artifacts.");
        }
        else
        {
            Console.WriteLine($"File '{filePath}' added to artifacts.");

        }
    }

    // overwrite the temporary store
    artifactService.WriteArtifactDatabase(store);
    return ExitCode.Success;
}

ExitCode RemoveArtifact(Options options)
{
    var artifactService = new ArtifactService();
    var path = Environment.CurrentDirectory;
    if (options.File == null || !options.File.Any())
    {
        Console.WriteLine($"File must be specified (-f).");
        return ExitCode.InvalidArguments;
    }
    if (string.IsNullOrWhiteSpace(options.DeploymentName))
    {
        Console.WriteLine($"A deployment name must be specified (-n).");
        return ExitCode.InvalidArguments;
    }
    var store = artifactService.CreateOrLoadStore(path, options.DeploymentName);
    if (artifactService.ArtifactStore.Count == 0)
    {
        Console.WriteLine($"No artifacts have been added! Syntax: Deploy.exe -f ./examplefile.zip -n example.com");
        return ExitCode.InvalidArguments;
    }

    foreach (var file in options.File)
    {
        var filePath = Path.GetFullPath(Path.Combine(path, file));
        if (!artifactService.RemoveArtifact(filePath))
        {
            Console.WriteLine($"Artifact '{filePath}' has not been added, cannot remove.");
        }
        else
        {
            Console.WriteLine($"Artifact '{filePath}' has been removed.");
        }
    }
    // success
    // overwrite the temporary store
    artifactService.WriteArtifactDatabase(store);
    return ExitCode.Success;
}

ExitCode GetArtifacts(Options options)
{
    var artifactService = new ArtifactService();
    var path = Environment.CurrentDirectory;
    if (string.IsNullOrWhiteSpace(options.DeploymentName))
    {
        Console.WriteLine($"A deployment name must be specified (-n).");
        return ExitCode.InvalidArguments;
    }
    var store = artifactService.CreateOrLoadStore(path, options.DeploymentName);
    if (artifactService.ArtifactStore.Count == 0)
    {
        Console.WriteLine($"No artifacts have been added! Syntax: Deploy.exe -f ./examplefile.zip -n example.com");
        return ExitCode.InvalidArguments;
    }

    foreach (var artifact in artifactService.ArtifactStore)
    {
        if (string.IsNullOrEmpty(artifact))
            continue;
        var artifactPath = Path.GetFullPath(artifact);
        if (options.Verbose) Console.WriteLine($" - Artifact: {artifactPath} ({(File.Exists(artifactPath) ? "exists" : "does not exist")})");
        else Console.WriteLine($"{artifactPath}");
    }
    return ExitCode.Success;
}

ExitCode Deploy(Options options)
{
    var artifactService = new ArtifactService();
    var deployService = new DeployService();
    var path = Environment.CurrentDirectory;
    // check for artifacts for the website in temp store
    // upload
    // delete from temp store
    if (string.IsNullOrWhiteSpace(options.DeploymentName))
    {
        Console.WriteLine($"A deployment name must be specified (-n).");
        return ExitCode.InvalidArguments;
    }
    if (string.IsNullOrWhiteSpace(options.Host))
    {
        Console.WriteLine($"Host to deploy to must be specified (-h).");
        return ExitCode.InvalidArguments;
    }
    var store = artifactService.CreateOrLoadStore(path, options.DeploymentName);
    // to support in-line specified files, add them to the artifact store temporarily
    if (options.File != null && options.File.Any())
    {
        foreach (var file in options.File)
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
                if (File.Exists(fullPath))
                    artifactService.AddArtifact(fullPath);
            }
        }
    }
    if (artifactService.ArtifactStore.Count == 0)
    {
        Console.WriteLine($"No artifacts have been added! Syntax: Deploy.exe -f ./examplefile.zip -n example.com");
        return ExitCode.InvalidArguments;
    }
    if (options.Verbose && File.Exists(store)) Console.WriteLine($"Artifacts file: {store}");
    if (options.Verbose) Console.WriteLine($"Artifacts count: {artifactService.ArtifactStore.Count}");

    // upload artifacts
    try
    {
        Action<string> onVerbose = str =>
        {
            if (options.Verbose)
            {
                Console.WriteLine(str);
            }
        };
        Action<string> onWarning = str => Console.WriteLine(str);
        Action<string> onInteractive = str =>
        {
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(str);
            Console.ForegroundColor = color;
        };
        deployService.Deploy(artifactService, options.DeploymentName, options.Domain, options.DeploymentScript, options.Host, options.Username ?? string.Empty, options.Password ?? string.Empty, options.Token ?? string.Empty, options.Port, options.Timeout, options.RequestTimeout, options.AutoCopy, options.AutoExtract, options.IgnoreCert, options.Interactive, options.IIS, onVerbose, onWarning, onInteractive);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.GetBaseException().Message}");
        return ExitCode.GeneralError;
    }
    return ExitCode.Success;
}