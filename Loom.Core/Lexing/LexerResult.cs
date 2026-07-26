using Loom.Core.Diagnostics;
using Loom.Core.Text;

namespace Loom.Core.Lexing;

public sealed record LexerResult(List<Token> Tokens, List<Token> TokensWithTrivia, DiagnosticBag Diagnostics)
    : DiagnosedResult(Diagnostics);