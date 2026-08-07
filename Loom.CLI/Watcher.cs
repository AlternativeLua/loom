using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Loom.Config;
using Loom.Core.Diagnostics;
using Loom.Core.Pipeline;

namespace Loom.CLI;

internal sealed class Watcher(DiagnosticOptions diagnosticOptions)
{
    private const string ConfigFileName = "loom-config.toml";

    private readonly HashSet<string> _pending = [];
    [AllowNull] private CompilationUnit _unit;

    public int Start(LoomConfig config)
    {
        Log.Info("Starting watch mode...");
        while (true)
        {
            var nextConfig = Watch(config);
            if (nextConfig == null)
                return 0;

            config = nextConfig;
        }
    }

    private LoomConfig? Watch(LoomConfig config)
    {
        var events = new BlockingCollection<string?>();
        _unit = new CompilationUnit(config, diagnosticOptions);
        Log.OutputResult(_unit.Compile());

        _pending.Clear();

        using var sourceWatcher = CreateSourceWatcher(config, events);
        using var projectWatcher = CreateProjectWatcher(config, events);
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            events.CompleteAdding();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            Log.Info($"Watching for changes. Press {Colors.Pink}Ctrl+C{Colors.Reset} to stop.");

            var restartNeeded = false;
            while (!events.IsCompleted)
            {
                if (!events.TryTake(out var path, TimeSpan.FromMilliseconds(200)))
                {
                    if (restartNeeded)
                        return ReloadConfig(config.ProjectDirectory) ?? config;

                    if (_pending.Count == 0) continue;

                    var changed = _pending.ToHashSet();
                    _pending.Clear();

                    Log.OutputResult(_unit.Recompile(changed.ToDictionary(k => k, File.ReadAllText)));
                    continue;
                }

                if (path == null)
                    restartNeeded = true;
                else
                    _pending.Add(path);
            }

            return null;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private FileSystemWatcher CreateSourceWatcher(LoomConfig config, BlockingCollection<string?> events)
    {
        var watcher = new FileSystemWatcher(config.Files.SourceDirectory)
        {
            Filter = $"*{FileManager.LoomExtension}",
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        watcher.Changed += (_, e) => OnSourceChanged(events, e);
        watcher.Created += (_, e) => OnSourceCreatedOrChanged(events, e);
        watcher.Deleted += (_, _) => OnSourceStructureChanged(events);
        watcher.Renamed += (_, _) => OnSourceStructureChanged(events);

        return watcher;
    }

    private static FileSystemWatcher CreateProjectWatcher(LoomConfig config, BlockingCollection<string?> events)
    {
        var watcher = new FileSystemWatcher(config.ProjectDirectory) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName, EnableRaisingEvents = true };
        watcher.Changed += (_, e) => OnProjectFileChanged(events, e);
        watcher.Created += (_, e) => OnProjectFileChanged(events, e);
        
        return watcher;
    }

    // Some editors save by deleting and recreating the file (an atomic replace)
    // rather than writing in place, so a "created" event for an already-tracked
    // path is just its new content, not a structural change.
    private void OnSourceCreatedOrChanged(BlockingCollection<string?> events, FileSystemEventArgs e) =>
        events.Add(
            _unit.SourceFiles.FirstOrDefault(file => file.AbsolutePath == e.FullPath) != null
                ? e.FullPath
                : null
        );

    private static void OnSourceChanged(BlockingCollection<string?> events, FileSystemEventArgs e) => events.Add(e.FullPath);
    private static void OnSourceStructureChanged(BlockingCollection<string?> events) => events.Add(null);

    private static void OnProjectFileChanged(BlockingCollection<string?> events, FileSystemEventArgs e)
    {
        if (e.Name is ConfigFileName or RojoResolver.ProjectFileName)
            events.Add(null);
    }

    private static LoomConfig? ReloadConfig(string projectDirectory) => ConfigReader.LocateFromDirectory(projectDirectory, out _);
}