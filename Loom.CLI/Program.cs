using System.Collections.Concurrent;
using System.Text;
using Loom.Config;
using Loom.Core.Diagnostics;
using Loom.Core.Pipeline;

const string includeFolderName = "include";
const string configFileName = "loom-config.toml";

Console.OutputEncoding = Encoding.UTF8;

var watch = args.Any(a => a is "-w" or "--watch");

// a dependency's diagnostics are about code the user cannot fix, so a build reports only that the package
// failed; this asks for them in full, for debugging a package from a project that consumes it
var dependencyDiagnostics = args.Any(a => a is "--dependency-diagnostics");
var directory = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith('-')) ?? ".");
var diagnosticOptions = new DiagnosticOptions { FailFast = !watch, ReportDependencyDiagnostics = dependencyDiagnostics };

var config = ConfigReader.LocateFromDirectory(directory, out var configDiagnostics);
if (config == null)
{
    if (configDiagnostics.Count == 0)
        throw new ArgumentException($"Could not locate Loom configuration file in directory '{directory}'.");

    Console.WriteLine(string.Join(Environment.NewLine, configDiagnostics.Select(diagnostic => $"{configFileName} {diagnostic}")));
    return 1;
}

writeIncludeFolder(config);

if (!watch)
{
    printResult(new CompilationUnit(config, diagnosticOptions).Compile());
    return 0;
}

return runWatchLoop(config);

int runWatchLoop(LoomConfig watchedConfig)
{
    var unit = new CompilationUnit(watchedConfig, diagnosticOptions);
    printResult(unit.Compile());

    var events = new BlockingCollection<string?>();
    using var sourceWatcher = new FileSystemWatcher(watchedConfig.Files.SourceDirectory)
    {
        Filter = $"*{FileManager.LoomExtension}",
        IncludeSubdirectories = true,
        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
    };

    using var projectWatcher = new FileSystemWatcher(watchedConfig.ProjectDirectory) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };

    void onSourceChanged(object? _, FileSystemEventArgs e) => events.Add(e.FullPath);
    void onSourceStructureChanged(object? _, FileSystemEventArgs e) => events.Add(null);

    // some editors save by deleting and recreating the file (an atomic replace) rather than
    // writing in place, so a 'created' event for an already-tracked path is just its new content,
    // not a structural change - only a genuinely new path needs the unit rebuilt from scratch
    void onSourceCreatedOrChanged(object? _, FileSystemEventArgs e)
    {
        if (unit.SourceFiles.Any(file => file.AbsolutePath == e.FullPath))
            events.Add(e.FullPath);
        else
            events.Add(null);
    }

    void onProjectFileChanged(object? _, FileSystemEventArgs e)
    {
        if (e.Name is configFileName or RojoResolver.ProjectFileName)
            events.Add(null);
    }

    sourceWatcher.Changed += onSourceChanged;
    sourceWatcher.Created += onSourceCreatedOrChanged;
    sourceWatcher.Deleted += onSourceStructureChanged;
    sourceWatcher.Renamed += onSourceStructureChanged;
    projectWatcher.Changed += onProjectFileChanged;
    projectWatcher.Created += onProjectFileChanged;

    sourceWatcher.EnableRaisingEvents = true;
    projectWatcher.EnableRaisingEvents = true;

    Console.CancelKeyPress += (_, cancelArgs) =>
    {
        cancelArgs.Cancel = true;
        events.CompleteAdding();
    };

    Console.WriteLine("[Info] Watching for changes. Press Ctrl+C to stop.");

    var pending = new HashSet<string>();
    var restartNeeded = false;
    while (!events.IsCompleted)
    {
        if (!events.TryTake(out var path, TimeSpan.FromMilliseconds(200)))
        {
            if (restartNeeded)
                return runWatchLoop(reloadConfig(directory) ?? watchedConfig);

            if (pending.Count == 0)
                continue;

            var changed = pending.ToHashSet();
            pending.Clear();
            printResult(unit.Recompile(changed));
            continue;
        }

        if (path == null)
            restartNeeded = true;
        else
            pending.Add(path);
    }

    return 0;
}

static LoomConfig? reloadConfig(string projectDirectory) => ConfigReader.LocateFromDirectory(projectDirectory, out _);

void printResult(CompilationResult result)
{
    var diagnosticInfo = result.Files
        .Where(f => !f.SourceFile.IsDeclaration)
        .Select(f => f.Diagnostics.WithoutInfo().ToString())
        .Where(diagnostics => !string.IsNullOrEmpty(diagnostics));

    var failureInfo = result.Failures.Count == 0
        ? []
        : new[]
        {
            $"Not compiled: {string.Join(", ", result.Failures.Select(failure => failure.File.Name))}",
            DiagnosticBag.Concat(result.Failures.ConvertAll(failure => failure.Diagnostics)).WithoutInfo().ToString()
        };

    var lines = diagnosticInfo.Concat(failureInfo).ToList();
    if (lines.Count > 0)
        Console.WriteLine(string.Join(Environment.NewLine, lines));

    var timingLine = $"[Info] Compiled in {result.Elapsed.TotalSeconds:F3} seconds.";
    if (result.EstimatedTimeSaved > TimeSpan.Zero)
        timingLine += $" Time saved by heuristics: {result.EstimatedTimeSaved.TotalSeconds:F3} seconds.";

    Console.WriteLine(timingLine);
}

void writeIncludeFolder(LoomConfig writtenConfig)
{
    var sourceDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", includeFolderName);
    if (!Directory.Exists(sourceDirectory))
        throw new DirectoryNotFoundException($"Include directory not found: {sourceDirectory}");

    var includeFolder = Path.Combine(writtenConfig.ProjectDirectory, includeFolderName);
    Directory.CreateDirectory(includeFolder);
    copyDirectory(sourceDirectory, includeFolder);
}

static void copyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var directoryPath in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(source, directoryPath);
        Directory.CreateDirectory(Path.Combine(destination, relative));
    }

    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(source, file);
        var destinationFile = Path.Combine(destination, relative);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
        File.Copy(file, destinationFile, overwrite: true);
    }
}
