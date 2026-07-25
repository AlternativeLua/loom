using Loom.Core.Debug;
using Loom.Core.Diagnostics;
using Loom.Core.Parsing.AST;
using Loom.Core.Text;

namespace Loom.Testing;

[Collection("Assembly")]
public class ParserTest
{
    public static readonly IEnumerable<TheoryDataRow<string, string, string, string?>> ErrorTestCases =
    [
        new("let", InternalCodes.UnexpectedEof, "Expected identifier, got EOF.", null),
        new("let x:", InternalCodes.UnexpectedEof, "Expected type, got EOF.", null),
        new("!", InternalCodes.UnexpectedEof, "Unexpected end of file.", null),
        new("if )", InternalCodes.UnexpectedToken, "Expected expression, got ')'.", null),
        new("nameof(123)", InternalCodes.InvalidNameOf, "'123' is not a valid name.", null),
        new("(1 + 2", InternalCodes.UnexpectedEof, "Expected ')' here to close '(' at character 0, got EOF.", null),
        new("(1 + 2]", InternalCodes.UnexpectedToken, "Expected ')' here to close '(' at character 0, got ']'.", null),
        new("arr[0", InternalCodes.UnexpectedEof, "Expected ']', got EOF.", null),
        new("1 = 1", InternalCodes.InvalidAssignmentTarget, "Invalid assignment target.", null),
        new("a ? b : c = d", InternalCodes.InvalidAssignmentTarget, "Invalid assignment target.", null),
        new("fn foo", InternalCodes.MissingFunctionBody, "Expected function body, got EOF.", null),
        new("if true let x = 42", InternalCodes.DeclarationOutsideOfBlock, "Declarations can only be declared inside of a block.", "surround with '{' and '}'"),
        new(
            "if true { return 1 } else let x = 42",
            InternalCodes.DeclarationOutsideOfBlock,
            "Declarations can only be declared inside of a block.",
            "surround with '{' and '}'"
        ),
        new("while true let x = 1", InternalCodes.DeclarationOutsideOfBlock, "Declarations can only be declared inside of a block.", "surround with '{' and '}'"),
        new("after 10ms let x = 1", InternalCodes.DeclarationOutsideOfBlock, "Declarations can only be declared inside of a block.", null),
        new("declare fn foo(a: number)", InternalCodes.MissingDeclareFnReturnType, "Declared function signatures must have a return type.", null),
        new(
            "declare fn foo(a: number = 5): void",
            InternalCodes.UseOfDeclareFnParameterDefaults,
            "Parameters may not have default values in declared function signatures.",
            null
        ),
        new("declare fn foo(a): void", InternalCodes.MissingDeclareFnParameterType, "Parameters must have types in declared function signatures.", null),
        new("declare 123", InternalCodes.ExpectedDeclarationSignature, "Expected declaration signature, got '123'.", null),
        new("export event Foo()", InternalCodes.ExpectedExportableDeclaration, "Only 'fn', 'let', 'type', 'interface', 'enum' and 'trait' declarations can be exported, got 'event'.", null),
        new("export 123", InternalCodes.ExpectedExportableDeclaration, "Only 'fn', 'let', 'type', 'interface', 'enum' and 'trait' declarations can be exported, got '123'.", null),
        new("type Fn = fn(number)", InternalCodes.MissingDeclareFnReturnType, "Function types must have a return type.", null),
        new("type Fn = fn(x: number = 5): number", InternalCodes.UseOfDeclareFnParameterDefaults, "Parameters may not have default values in function types.", null),
        new("type Fn = fn(x): number", InternalCodes.MissingDeclareFnParameterType, "Parameters must have types in function types.", null),
        new("type F = (fn())", InternalCodes.MissingDeclareFnReturnType, "Function types must have a return type.", null),
        new("interface I { name }", InternalCodes.ExpectedInterfaceMemberType, "Expected indexer type, got '}'.", null),
        new("interface I { [number] }", InternalCodes.UnexpectedToken, "Expected property name, got '}'.", null),
        new("interface { }", InternalCodes.UnexpectedToken, "Expected interface name, got '{'.", null),
        new("interface I { 123 }", InternalCodes.UnexpectedToken, "Expected property name, got '123'.", null),
        new("after { }", InternalCodes.UnexpectedToken, "Expected expression, got '{'.", null),
        new("after 5s", InternalCodes.UnexpectedEof, "Unexpected end of file.", null),
        new("for : items { }", InternalCodes.UnexpectedToken, "Expected identifier, got ':'.", null),
        new("for x items { }", InternalCodes.UnexpectedToken, "Expected ':', got 'items'.", null),
        new("fn a<>() { }", InternalCodes.UnexpectedToken, "Expected type parameter name, got '>'.", null),
        new("fn a<T(x: T) { }", InternalCodes.UnexpectedToken, "Expected '>', got '('.", null),
        new("let x: List<number, string", InternalCodes.UnexpectedEof, "Expected '>', got EOF.", null),
        new("let x: List<number", InternalCodes.UnexpectedEof, "Expected '>', got EOF.", null),
        new("let x: List<>", InternalCodes.UnexpectedToken, "Expected type, got '>'.", null),
        new("let x: List<number,>", InternalCodes.UnexpectedToken, "Expected type, got '>'.", null),
        new("let x: List<number>>", InternalCodes.UnexpectedToken, "Expected expression, got '>'.", null),
        new("let x: number |", InternalCodes.UnexpectedEof, "Expected type, got EOF.", null),
        new("let x: number &", InternalCodes.UnexpectedEof, "Expected type, got EOF.", null),
        new("let x: (number", InternalCodes.UnexpectedEof, "Expected ')' here to close '(' at character 7, got EOF.", null),
        new("let x: keyof(T | number)", InternalCodes.UnexpectedToken, "Expected ')', got '|'.", null),
        new("let x: typeof", InternalCodes.UnexpectedEof, "Expected '(', got EOF.", null),
        new("let x: typeof(a", InternalCodes.UnexpectedEof, "Expected ')', got EOF.", null),
        new("let x: typeof()", InternalCodes.UnexpectedToken, "Expected expression, got ')'.", null),
        new("trait { }", InternalCodes.UnexpectedToken, "Expected trait name, got '{'.", null),
        new("trait Foo { fn bar() }", InternalCodes.MissingDeclareFnReturnType, "Declared function signatures must have a return type.", null),
        new("trait Foo { fn bar(x): void }", InternalCodes.MissingDeclareFnParameterType, "Parameters must have types in declared function signatures.", null),
        new(
            "trait Foo { fn bar(x: number = 5): void }",
            InternalCodes.UseOfDeclareFnParameterDefaults,
            "Parameters may not have default values in declared function signatures.",
            null
        ),
        new("implement", InternalCodes.UnexpectedEof, "Expected trait name, got EOF.", null),
        new("implement Foo", InternalCodes.UnexpectedEof, "Expected 'for', got EOF.", null),
        new("implement Foo for", InternalCodes.UnexpectedEof, "Expected interface name, got EOF.", null),
        new("implement Foo for Bar", InternalCodes.UnexpectedEof, "Expected '{', got EOF.", null),
        new("implement Foo for Bar {", InternalCodes.UnexpectedEof, "Expected '}', got EOF.", null),
        new("implement Foo for Bar { fn }", InternalCodes.UnexpectedToken, "Expected function name, got '}'.", null),
        new("implement 123 for Bar { }", InternalCodes.UnexpectedToken, "Expected trait name, got '123'.", null),
        new("implement Foo for Bar<T> { }", InternalCodes.UnexpectedToken, "Expected '{', got '<'.", null),
        new("implement Foo 123 Bar { }", InternalCodes.UnexpectedToken, "Expected 'for', got '123'.", null),
        new("implement Foo for 123 { }", InternalCodes.UnexpectedToken, "Expected interface name, got '123'.", null),
        new("nameof::<number>()", InternalCodes.InvalidTypeArguments, "May only get name of type when the type is a type name.", null),
        new("nameof::<T>(1)", InternalCodes.UnexpectedToken, "Expected ')', got '1'.", null),
        new("nameof::<T, U>()", InternalCodes.GenericArity, "Exactly one type parameter is allowed for 'nameof::<T>()'.", null),
        new("let x = mut 5", InternalCodes.UnexpectedToken, "Expected array literal after 'mut'.", null)
    ];

    public static readonly IEnumerable<TheoryDataRow<string, string>> SnapshotFiles = Utility.GetSnapshotFiles("AST", ".ast");

    [Theory]
    [MemberData(nameof(ErrorTestCases))]
    public void Parser_ErrorDiagnostics(string source, string code, string expectedMessage, string? hint = null)
    {
        var diagnostics = Utility.GetParserDiagnostics(source);
        Utility.AssertDiagnostic(diagnostics, code, expectedMessage, hint);
    }

    [Theory]
    [MemberData(nameof(SnapshotFiles))]
    public void Parser_Snapshots(string sourcePath, string snapshotPath)
    {
        var source = File.ReadAllText(sourcePath);
        var result = Utility.Parse(source);
        Utility.AssertNoErrors(result);

        var actual = AstInspector.Inspect(result.Tree).Replace(Environment.NewLine, "\n");
        var expected = File.ReadAllText(snapshotPath).Replace(Environment.NewLine, "\n");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Expect_MissingParen_PreservesFollowingDeclaration()
    {
        const string source = """
            let x = foo(1, 2
            let y = 3;
            """;

        var result = Utility.Parse(source);
        Utility.AssertDiagnostic(result.Diagnostics, InternalCodes.UnexpectedToken, "Expected ')', got 'let'.");

        Assert.Equal(2, result.Tree.Statements.Count);
        var first = Assert.IsType<VariableDeclaration>(result.Tree.Statements[0]);
        var second = Assert.IsType<VariableDeclaration>(result.Tree.Statements[1]);
        Assert.Equal("x", first.Name.Text);
        Assert.Equal("y", second.Name.Text);

        var invocation = Assert.IsType<Invocation>(first.EqualsValueClause!.Value);
        var rightParen = invocation.Arguments.RightParen;
        Assert.Equal(SyntaxKind.RParen, rightParen.Kind);
        Assert.Equal(0, rightParen.Span.Length);
        Assert.Equal(")", rightParen.Text);
    }

    [Fact]
    public void Expect_MissingParen_AtEof_InsertsZeroWidthToken()
    {
        var result = Utility.Parse("let x = foo(");
        var expectDiagnostic = result.Diagnostics.Find(d =>
            d.Code == InternalCodes.UnexpectedEof && d.Message == "Expected ')', got EOF."
        );
        Assert.NotNull(expectDiagnostic);

        var declaration = Assert.IsType<VariableDeclaration>(Assert.Single(result.Tree.Statements));
        var invocation = Assert.IsType<Invocation>(declaration.EqualsValueClause!.Value);
        var rightParen = invocation.Arguments.RightParen;
        Assert.Equal(SyntaxKind.RParen, rightParen.Kind);
        Assert.Equal(0, rightParen.Span.Length);
        Assert.Equal(")", rightParen.Text);
    }

    [Fact]
    public void Expect_MissingBracket_PreservesFollowingDeclaration()
    {
        const string source = """
            let x = arr[0
            let y = 1;
            """;

        var result = Utility.Parse(source);
        Utility.AssertDiagnostic(result.Diagnostics, InternalCodes.UnexpectedToken, "Expected ']', got 'let'.");

        Assert.Equal(2, result.Tree.Statements.Count);
        Assert.Equal("x", Assert.IsType<VariableDeclaration>(result.Tree.Statements[0]).Name.Text);
        Assert.Equal("y", Assert.IsType<VariableDeclaration>(result.Tree.Statements[1]).Name.Text);
    }

    [Fact]
    public void ParseBlock_UnterminatedAtEof_Terminates()
    {
        var result = Utility.Parse("fn foo() {");
        var fn = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Tree.Statements));
        var block = Assert.IsType<Block>(fn.Body);
        Assert.Equal(SyntaxKind.RBrace, block.RightBrace.Kind);
        Assert.Equal(0, block.RightBrace.Span.Length);
        Assert.Equal("}", block.RightBrace.Text);
    }

    [Fact]
    public void ParseBlock_Unterminated_KeepsInnerStatements()
    {
        const string source = """
            fn foo() {
            let y = 1;
            """;

        var result = Utility.Parse(source);
        var fn = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Tree.Statements));
        var block = Assert.IsType<Block>(fn.Body);
        var declaration = Assert.IsType<VariableDeclaration>(Assert.Single(block.Statements));
        Assert.Equal("y", declaration.Name.Text);
        Assert.Equal(0, block.RightBrace.Span.Length);
    }

    [Fact]
    public void ParseBlock_UnterminatedIf_Terminates()
    {
        var result = Utility.Parse("if true {");
        var @if = Assert.IsType<If>(Assert.Single(result.Tree.Statements));
        var block = Assert.IsType<Block>(@if.ThenBranch);
        Assert.Equal(0, block.RightBrace.Span.Length);
    }

    [Fact]
    public void Synchronize_JunkInsideBlock_PreservesFollowingDeclaration()
    {
        const string source = """
            fn f() {
            +
            let y = 1;
            }
            """;

        var result = Utility.Parse(source);
        Utility.AssertDiagnostic(result.Diagnostics, InternalCodes.UnexpectedToken, "Expected expression, got '+'.");

        var fn = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Tree.Statements));
        var block = Assert.IsType<Block>(fn.Body);
        Assert.Equal(2, block.Statements.Count);
        Assert.IsType<NullExpression>(Assert.IsType<ExpressionStatement>(block.Statements[0]).Expression);
        Assert.Equal("y", Assert.IsType<VariableDeclaration>(block.Statements[1]).Name.Text);
        Assert.Equal(1, block.RightBrace.Span.Length);
    }

    [Fact]
    public void Synchronize_JunkBeforeClosingBrace_DoesNotConsumeBrace()
    {
        const string source = """
            fn f() {
            +
            }
            """;

        var result = Utility.Parse(source);
        var fn = Assert.IsType<FunctionDeclaration>(Assert.Single(result.Tree.Statements));
        var block = Assert.IsType<Block>(fn.Body);
        Assert.Equal(SyntaxKind.RBrace, block.RightBrace.Kind);
        Assert.Equal(1, block.RightBrace.Span.Length);
        Assert.Equal("}", block.RightBrace.Text);
    }

    [Fact]
    public void Synchronize_TopLevelJunk_PreservesFollowingDeclaration()
    {
        const string source = """
            +
            let y = 1;
            """;

        var result = Utility.Parse(source);
        Assert.Equal(2, result.Tree.Statements.Count);
        Assert.IsType<NullExpression>(Assert.IsType<ExpressionStatement>(result.Tree.Statements[0]).Expression);
        Assert.Equal("y", Assert.IsType<VariableDeclaration>(result.Tree.Statements[1]).Name.Text);
    }

    [Fact]
    public void Unfinished_ProducesNull()
    {
        var tree = Utility.GetAST("let");
        Assert.Single(tree.Statements);
        var declaration = Assert.IsType<VariableDeclaration>(tree.Statements.First());
        Assert.Null(declaration.ColonTypeClause);
        Assert.Null(declaration.EqualsValueClause);
    }

    [Fact]
    public void Error_ProducesNullExpression()
    {
        var tree = Utility.GetAST("=");
        Assert.Single(tree.Statements);

        var expressionStatement = Assert.IsType<ExpressionStatement>(tree.Statements.First());
        Assert.IsType<NullExpression>(expressionStatement.Expression);
    }

    [Fact]
    public void Error_ProducesNullTypeExpression()
    {
        var tree = Utility.GetAST("type X = fn(a = 69): void");
        Assert.Single(tree.Statements);

        var alias = Assert.IsType<TypeAlias>(tree.Statements.First());
        Assert.IsType<NullTypeExpression>(alias.EqualsTypeClause.Type);
    }

    [Fact]
    public void Error_ProducesNullStatement()
    {
        var tree = Utility.GetAST("if x let y = 1");
        Assert.Single(tree.Statements);

        var @if = Assert.IsType<If>(tree.Statements.First());
        Assert.IsType<NullStatement>(@if.ThenBranch);
    }

    [Theory]
    [InlineData("123", 123L)]
    [InlineData("1e3", 100L)]
    [InlineData("0x100", 256L)]
    [InlineData("0xFF_FF", 65535L)]
    [InlineData("0Xf0D", 3853L)]
    [InlineData("0b011100110", 230L)]
    [InlineData("0b01110_0110", 230L)]
    [InlineData("0B11001", 25L)]
    [InlineData("0o400", 256L)]
    [InlineData("0O2340", 1248L)]
    [InlineData("1.23e3", 1230L)]
    [InlineData("420.69", 420.69d)]
    [InlineData("1.2345e3", 1234.5)]
    [InlineData("1_0_0________.6_9e5_1", long.MaxValue)]
    [InlineData("5s", 5L)]
    [InlineData("500ms", 0.5)]
    [InlineData("20hz", 0.05)]
    [InlineData("2_0.4_5ms", 0.02045)]
    [InlineData("0.5m", 30L)]
    [InlineData("20m", 1800L)]
    [InlineData("2h", 7200L)]
    [InlineData("'hello'", "hello")]
    [InlineData("\"abc\"", "abc")]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("none", null)]
    public void Parses_Literals(string source, object? expectedValue)
    {
        var tree = Utility.GetAST(source);
        Assert.Single(tree.Statements);

        var statement = tree.Statements.First();
        var expressionStatement = Assert.IsType<ExpressionStatement>(statement);
        var literal = Assert.IsType<Literal>(expressionStatement.Expression);
        Assert.Equal(source, literal.Token.Text);
        if (expectedValue != null)
            Assert.IsType(expectedValue.GetType(), literal.Value);
        else
            Assert.Null(literal.Value);
    }

    #region Event Attributes
    [Fact]
    public void Parses_InterfaceEvent_WithAttribute()
    {
        const string source = """
            interface X {
                [luau_name("Foo")]
                event abc(x: number);
            }
            """;

        var result = Utility.AssertNoErrors(Utility.Parse(source));
        var interfaceDeclaration = Assert.IsType<InterfaceDeclaration>(result.Tree.Statements.Single());
        Assert.NotNull(interfaceDeclaration.Body);

        var eventDeclaration = Assert.IsType<EventDeclaration>(Assert.Single(interfaceDeclaration.Body.Members));
        Assert.NotNull(eventDeclaration.Attributes);

        var attribute = Assert.Single(eventDeclaration.Attributes.AttributeList);
        var identifier = Assert.IsType<Identifier>(attribute.Expression);
        Assert.Equal("luau_name", identifier.Name.Text);
    }

    [Fact]
    public void Parses_TopLevelEvent_WithAttribute()
    {
        const string source = """
            [luau_name("Foo")]
            event abc(x: number);
            """;

        var result = Utility.AssertNoErrors(Utility.Parse(source));
        var eventDeclaration = Assert.IsType<EventDeclaration>(result.Tree.Statements.Single());
        Assert.NotNull(eventDeclaration.Attributes);

        var attribute = Assert.Single(eventDeclaration.Attributes.AttributeList);
        var identifier = Assert.IsType<Identifier>(attribute.Expression);
        Assert.Equal("luau_name", identifier.Name.Text);
    }

    [Fact]
    public void Parses_TopLevelArrayLiteralStatement_NotMistakenForAttributes()
    {
        var tree = Utility.GetAST("[1, 2, 3];");
        var statement = Assert.IsType<ExpressionStatement>(Assert.Single(tree.Statements));
        var arrayLiteral = Assert.IsType<ArrayLiteral>(statement.Expression);
        Assert.Equal(3, arrayLiteral.Expressions.Count);
    }

    [Fact]
    public void Parses_MutEvent_StillProducesMutEventError()
    {
        const string plainSource = """
            interface X {
                mut event abc;
            }
            """;

        const string attributedSource = """
            interface X {
                [luau_name("Foo")]
                mut event abc;
            }
            """;

        var plainDiagnostics = Utility.GetParserDiagnostics(plainSource);
        var attributedDiagnostics = Utility.GetParserDiagnostics(attributedSource);
        Assert.Equal(plainDiagnostics.Set.Count, attributedDiagnostics.Set.Count);
    }
    #endregion Event Attributes
}