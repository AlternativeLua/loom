using Loom.Core.Diagnostics;
using Loom.Core.Parsing.AST;
using Loom.Core.Resolving;
using Loom.Core.Text;
using Loom.Luau.AST;
using Type = Loom.Core.TypeChecking.Types.Type;

namespace Loom.Core.Pipeline;

public sealed class CompiledFile(SourceFile sourceFile)
{
    public SourceFile SourceFile { get; } = sourceFile;
    public required string Path { get; init; }
    public required DiagnosticBag Diagnostics { get; init; }
    public required string RenderedLuau { get; init; }
    public required LuauTree LuauTree { get; init; }
    public required Type ReturnType { get; init; }
    public required SemanticModel SemanticModel { get; init; }
    public required Tree Tree { get; init; }
    public required IReadOnlyList<Token> Tokens { get; init; }
}
