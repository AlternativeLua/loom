using System.Text;
using Loom.CLI;
using Loom.Config;
using Loom.Core.Diagnostics;
using Loom.Core.Pipeline;

Console.OutputEncoding = Encoding.UTF8;

var watch = args.Any(a => a is "-w" or "--watch");
var directory = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith('-')) ?? ".");
var diagnosticOptions = new DiagnosticOptions { FailFast = !watch };

var config = ConfigReader.LocateFromDirectory(directory, out var configDiagnostics);
if (config == null)
{
    if (configDiagnostics.Count == 0)
        throw new ArgumentException($"Could not locate Loom configuration file in directory '{directory}'.");

    Console.WriteLine(string.Join(Environment.NewLine, configDiagnostics.Select(diagnostic => $"{ConfigReader.ConfigFileName} {diagnostic}")));
    return 1;
}

FileManager.WriteIncludeFolder(config.ProjectDirectory);
if (!watch)
{
    Log.OutputResult(new CompilationUnit(config, diagnosticOptions).Compile());
    return 0;
}

var watcher = new Watcher(diagnosticOptions);
return watcher.Start(config);