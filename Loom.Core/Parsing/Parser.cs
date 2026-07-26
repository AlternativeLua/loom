using System.Diagnostics.CodeAnalysis;
using Loom.Core.Diagnostics;
using Loom.Core.Lexing;
using Loom.Core.Parsing.AST;
using Loom.Core.Text;

namespace Loom.Core.Parsing;

public sealed partial class Parser(LexerResult lexerResult)
{
    private readonly DiagnosticBag _diagnostics = new();
    private int _position;

    public ParserResult Parse()
    {
        var statements = new List<Statement>();
        while (!IsEof())
        {
            var previousPosition = _position;
            statements.Add(ParseStatement());
            EnsureProgress(previousPosition);
        }

        var tree = new Tree(lexerResult, statements);
        return new ParserResult(tree, _diagnostics);
    }

    private void EnsureProgress(int previousPosition)
    {
        if (_position != previousPosition || IsEof())
            return;

        Advance();
    }

    private List<T> ParseDelimited<T>(Func<T> parse, SyntaxKind delimiter = SyntaxKind.Comma)
    {
        var first = parse();
        if (first == null)
            return [];

        var nodes = new List<T> { first };
        while (Match(delimiter))
        {
            var node = parse();
            if (node == null) continue;
            nodes.Add(node);
        }

        return nodes;
    }

    private bool ValidateFunctionSignature(string kind, LocationSpan span, [NotNullWhen(true)] ColonTypeClause? returnType, Parameters? parameters) =>
        ValidateSignatureReturnType(kind, span, returnType) && ValidateSignatureParameters(kind, parameters);

    private bool ValidateSignatureParameters(string kind, Parameters? parameters)
    {
        var parameterWithDefault = parameters?.ParameterList.Find(p => p.EqualsValueClause != null);
        if (parameterWithDefault != null)
        {
            _diagnostics.Error(
                parameterWithDefault,
                InternalCodes.UseOfDeclareFnParameterDefaults,
                $"Parameters may not have default values in {kind}."
            );

            return false;
        }

        var parameterWithoutType = parameters?.ParameterList.Find(p => p.ColonTypeClause == null);
        if (parameterWithoutType == null)
            return true;

        _diagnostics.Error(
            parameterWithoutType,
            InternalCodes.MissingDeclareFnParameterType,
            $"Parameters must have types in {kind}."
        );

        return false;
    }

    private bool ValidateSignatureReturnType(string kind, LocationSpan span, ColonTypeClause? returnType)
    {
        if (returnType != null)
            return true;

        _diagnostics.Error(
            span,
            InternalCodes.MissingDeclareFnReturnType,
            $"{(kind.Length > 0 ? char.ToUpperInvariant(kind[0]) + kind[1..] : kind)} must have a return type."
        );

        return false;
    }

    private bool AssertDeclarationInsideOfBlock(Statement statement)
    {
        if (statement is not NamedDeclaration namedDeclaration)
            return true;

        _diagnostics.Error(
            namedDeclaration,
            InternalCodes.DeclarationOutsideOfBlock,
            "Declarations can only be declared inside of a block.",
            "surround with '{' and '}'"
        );

        return false;
    }

    private bool MatchClosingArrow([MaybeNullWhen(false)] out Token closingArrow)
    {
        closingArrow = null;
        if (IsEof())
            return false;

        return Current().Kind switch
        {
            SyntaxKind.RArrow => (closingArrow = Advance()) != null,
            SyntaxKind.RArrowRArrow => SplitAndAdvance(1, SyntaxKind.RArrow, out closingArrow),
            SyntaxKind.RArrowRArrowRArrow => SplitAndAdvance(1, SyntaxKind.RArrowRArrow, out closingArrow),
            _ => false
        };
    }

    // evil token splitting function
    private bool SplitAndAdvance(int splitIndex, SyntaxKind remainderKind, out Token closingArrow)
    {
        var token = Current();
        closingArrow = new Token(SyntaxKind.RArrow, token.File, new TextSpan(token.Span.Position, splitIndex), token.Text[..splitIndex]);

        var remainder = new Token(
            remainderKind,
            token.File,
            new TextSpan(token.Span.Position + splitIndex, token.Span.Length - splitIndex),
            token.Text[splitIndex..]
        );

        lexerResult.Tokens[_position] = closingArrow;
        lexerResult.Tokens.Insert(_position + 1, remainder);
        Advance();
        return true;
    }

    private bool Match([MaybeNullWhen(false)] out Token token, SyntaxKind kindA, SyntaxKind kindB) => Match(out token, kind => kind == kindA || kind == kindB);
    private bool Match(SyntaxKind kind) => Match(out _, kind);
    private void Match(SyntaxKind kindA, SyntaxKind kindB) => Match(out _, kindA, kindB);

    private bool Match([MaybeNullWhen(false)] out Token token, SyntaxKind kind)
    {
        if (IsEof())
        {
            token = null;
            return false;
        }

        token = Current();
        var match = kind == token.Kind;
        if (match)
            Advance();

        return match;
    }

    private bool Match([MaybeNullWhen(false)] out Token token, Predicate<SyntaxKind> predicate)
    {
        if (IsEof())
        {
            token = null;
            return false;
        }

        token = Current();
        var match = predicate(token.Kind);
        if (match)
            Advance();

        return match;
    }

    private Token ExpectIdentifier() => ExpectIdentifier("identifier");
    private Token ExpectIdentifier(string expected) => Expect(SyntaxKind.Identifier, expected);
    private Token Expect(SyntaxKind kind, string expected) => Expect(kind, token => $"Expected {expected}, got {SafeTokenText(token)}.");

    private Token Expect(SyntaxKind kind, Func<Token?, string>? message = null)
    {
        if (IsEof())
        {
            var last = lexerResult.Tokens[Math.Max(_position - 1, 0)];
            var text = SyntaxFacts.GetText(kind) ?? kind.ToString();
            _diagnostics.Error(last, InternalCodes.UnexpectedEof, message != null ? message(null) : $"Expected '{text}', got EOF.");
            return last;
        }

        var token = Advance();
        if (token.Kind == kind)
            return token;

        var expected = SyntaxFacts.GetText(kind) ?? kind.ToString();
        _diagnostics.Error(
            token,
            InternalCodes.UnexpectedToken,
            message != null ? message(token) : $"Expected '{expected}', got {SafeTokenText(token)}."
        );

        return token;
    }

    private Token Advance()
    {
        var current = Current();
        _position++;
        return current;
    }

    private Token Current() => lexerResult.Tokens[_position];
    private SyntaxKind PeekKind(int offset)
    {
        var index = _position + offset;
        return index >= 0 && index < lexerResult.Tokens.Count ? lexerResult.Tokens[index].Kind : SyntaxKind.Eof;
    }
    private bool IsEof() => Current().Kind == SyntaxKind.Eof;
    private static string SafeTokenText(Token? token) => token is { Kind: not SyntaxKind.Eof } ? $"'{token.Text}'" : "EOF";
}