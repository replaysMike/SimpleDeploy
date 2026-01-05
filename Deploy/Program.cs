using CommandLine;
using Deploy;
using SimpleDeploy.Cmdlet.Services;

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
            Console.WriteLine("Example: deploy -a artifact.zip -w example.com");
            Console.WriteLine("Example: deploy -w example.com");
        }
    });
return (int)exitCode;

ExitCode AddArtifact(Options options)
{
    var artifactService = new ArtifactService();
    var path = Environment.CurrentDirectory;
    if (string.IsNullOrWhiteSpace(options.File))
    {
        Console.WriteLine("Error: Artifact file must be specified using the -f option.");
        return ExitCode.InvalidArguments;
    }
    if (string.IsNullOrWhiteSpace(options.Website))
    {
        Console.WriteLine("Error: Website must be specified using the -w option.");
        return ExitCode.InvalidArguments;
    }
    var filePath = Path.GetFullPath(Path.Combine(path, options.File));
    if (!System.IO.File.Exists(filePath))
    {
        Console.WriteLine($"File '{filePath}' does not exist!");
        return ExitCode.InvalidArguments;
    }
    var store = artifactService.CreateOrLoadStore(path, options.Website);
    if (options.Verbose) Console.WriteLine($"Artifacts store: {store}");
    // add to store
    if (!artifactService.AddArtifact(filePath))
    {
        Console.WriteLine($"File '{filePath}' already added to artifacts.");
        return ExitCode.InvalidArguments;
    }

    // overwrite the temporary store
    artifactService.WriteArtifactDatabase(store);
    Console.WriteLine($"File '{filePath}' added to artifacts.");
    return ExitCode.Success;
}

ExitCode RemoveArtifact(Options options)
{
    var artifactService = new ArtifactService();
    var path = Environment.CurrentDirectory;
    if (string.IsNullOrWhiteSpace(options.File))
    {
        Console.WriteLine($"File must be specified.");
        return ExitCode.InvalidArguments;
    }
    if (string.IsNullOrWhiteSpace(options.Website))
    {
        Console.WriteLine($"Website must be specified.");
        return ExitCode.InvalidArguments;
    }
    var filePath = Path.GetFullPath(Path.Combine(path, options.File));
    var store = artifactService.CreateOrLoadStore(path, options.Website);
    if (artifactService.ArtifactStore.Count == 0)
    {
        Console.WriteLine($"No artifacts have been added! Syntax: Add-Artifact -File ./examplefile.zip -Website example.com");
        return ExitCode.InvalidArguments;
    }
    if (artifactService.RemoveArtifact(filePath))
    {
        Console.WriteLine($"Artifact '{filePath}' has been removed.");
        // success
        // overwrite the temporary store
        artifactService.WriteArtifactDatabase(store);
        return ExitCode.Success;
    }

    Console.WriteLine($"Artifact '{filePath}' has not been added, cannot remove.");
    return ExitCode.InvalidArguments;
}

ExitCode GetArtifacts(Options options)
{
    var artifactService = new ArtifactService();
    var path = Environment.CurrentDirectory;
    if (string.IsNullOrWhiteSpace(options.Website))
    {
        Console.WriteLine($"Website must be specified.");
        return ExitCode.InvalidArguments;
    }
    var store = artifactService.CreateOrLoadStore(path, options.Website);
    if (artifactService.ArtifactStore.Count == 0)
    {
        Console.WriteLine($"No artifacts have been added! Syntax: Add-Artifact -File ./examplefile.zip -Website example.com");
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
    if (string.IsNullOrWhiteSpace(options.Website))
    {
        Console.WriteLine($"Website must be specified.");
        return ExitCode.InvalidArguments;
    }
    if (string.IsNullOrWhiteSpace(options.Host))
    {
        Console.WriteLine($"Host must be specified.");
        return ExitCode.InvalidArguments;
    }
    var store = artifactService.CreateOrLoadStore(path, options.Website);
    if (options.Verbose) Console.WriteLine($"Artifacts file: {store}");
    if (options.Verbose) Console.WriteLine($"Artifacts count: {artifactService.ArtifactStore.Count}");
    if (artifactService.ArtifactStore.Count == 0)
    {
        Console.WriteLine($"No artifacts have been added! Syntax: Add-Artifact -File ./examplefile.zip -Website example.com");
        return ExitCode.InvalidArguments;
    }
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
        deployService.Deploy(artifactService, options.Website, options.DeploymentScript, options.Host, options.Username ?? string.Empty, options.Password ?? string.Empty, options.Token ?? string.Empty, options.Port, options.Timeout, options.RequestTimeout, onVerbose, onWarning);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.GetBaseException().Message}");
        return ExitCode.GeneralError;
    }
    return ExitCode.Success;
}