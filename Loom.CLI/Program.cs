using System.Text;
using Loom.Config;
using Loom.Core.Diagnostics;
using Loom.Core.Pipeline;

Console.OutputEncoding = Encoding.UTF8;

// the CLI has nothing left to do once a file fails to compile, so it stops at the first error.
// set FailFast to false to see every diagnostic of a run instead.
var diagnosticOptions = new DiagnosticOptions { FailFast = true };

var directory = args.ElementAtOrDefault(0) ?? ".";
var loomConfig = ConfigReader.LocateFromDirectory(directory);
if (loomConfig == null)
    throw new ArgumentException($"Could not locate Loom configuration file in directory '{directory}'.");

var compilationUnit = new CompilationUnit(loomConfig, diagnosticOptions);
var result = compilationUnit.Compile();
var debugInfo = result.Files
    .Where(f => !f.SourceFile.IsDeclaration)
    .Select(f => f.GetDebugInfo(false, debugDiagnostics: loomConfig.Debug));

// files the compiler gave up on have no debug info of their own, and with fail-fast off nothing else
// would report them. Their diagnostics are merged rather than printed per file, since one failure of
// the unit — the module graph, say — fails every file with the same error.
var failureInfo = result.Failures.Count == 0
    ? []
    : new[]
    {
        $"Not compiled: {string.Join(", ", result.Failures.Select(failure => failure.File.Name))}",
        DiagnosticBag.Concat(result.Failures.ConvertAll(failure => failure.Diagnostics)).WithoutInfo().ToString()
    };

Console.WriteLine(string.Join(Environment.NewLine, debugInfo.Concat(failureInfo)));