using Loom.Core.Diagnostics;

namespace Loom.Core.FlowAnalysis;

public sealed record FlowAnalyzerResult(DiagnosticBag Diagnostics)
    : DiagnosedResult(Diagnostics);