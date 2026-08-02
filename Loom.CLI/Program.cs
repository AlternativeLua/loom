using System.Text;
using Loom.Config;
using Loom.Core.Diagnostics;
using Loom.Core.Pipeline;

Console.OutputEncoding = Encoding.UTF8;
var diagnosticOptions = new DiagnosticOptions { FailFast = true };

var directory = args.ElementAtOrDefault(0) ?? ".";
var config = ConfigReader.LocateFromDirectory(directory);
if (config == null)
    throw new ArgumentException($"Could not locate Loom configuration file in directory '{directory}'.");

var compilationUnit = new CompilationUnit(config, diagnosticOptions);
var result = compilationUnit.Compile();
writeIncludeFolder(config);
var diagnosticInfo = result.Files
    .Where(f => !f.SourceFile.IsDeclaration)
    .Select(f => (config.Debug ? f.Diagnostics : f.Diagnostics.WithoutInfo()).ToString())
    .Where(diagnostics => !string.IsNullOrEmpty(diagnostics));

var failureInfo = result.Failures.Count == 0
    ? []
    : new[]
    {
        $"Not compiled: {string.Join(", ", result.Failures.Select(failure => failure.File.Name))}",
        DiagnosticBag.Concat(result.Failures.ConvertAll(failure => failure.Diagnostics)).WithoutInfo().ToString()
    };

Console.WriteLine(string.Join(Environment.NewLine, diagnosticInfo.Concat(failureInfo)));
const string includeFolderName = "include";
return;

static void writeIncludeFolder(LoomConfig config)
{
    var sourceDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", includeFolderName);
    if (!Directory.Exists(sourceDirectory))
        throw new DirectoryNotFoundException($"Include directory not found: {sourceDirectory}");

    var includeFolder = Path.Combine(config.ProjectDirectory, includeFolderName);
    Directory.CreateDirectory(includeFolder);
    copyDirectory(sourceDirectory, includeFolder);
}

static void copyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(source, directory);
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