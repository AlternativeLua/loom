using Loom.Core.Diagnostics;

namespace Loom.Core.Pipeline;

public static class Log
{
    public static void Info(string message) => Console.WriteLine($"[Info] {message}");
    
    public static void OutputResult(CompilationResult result)
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
}