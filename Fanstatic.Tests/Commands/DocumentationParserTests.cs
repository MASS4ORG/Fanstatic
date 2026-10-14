using Fanstatic.Commands.API;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Fanstatic.Test.Commands;

/// <summary>Covers the DocumentationParser behavior for XML and regular comment parsing.</summary>
public class DocumentationParserTests
{
    static Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax GetRoot(string sourceCode) =>
        CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();

    [Fact]
    /// <summary>Confirms that parsing a node without comments yields empty documentation.</summary>
    public void ParseDocumentationShouldReturnEmptyDocumentationForNodeWithoutComments()
    {
        // Arrange
        var sourceCode = "public class TestClass { }";
        var root = GetRoot(sourceCode);
        var classNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(classNode);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Summary);
        Assert.Empty(result.RawContent);
        Assert.Empty(result.Parameters);
        Assert.Empty(result.Returns);
        Assert.Empty(result.Example);
        Assert.Empty(result.Remarks);
    }

    [Fact]
    /// <summary>Confirms that a summary XML comment is parsed into the summary and raw content.</summary>
    public void ParseDocumentationShouldParseBasicXmlDocumentation()
    {
        // Arrange
        var sourceCode = """
                         /// <summary>
                         /// This is a test class for documentation parsing.
                         /// </summary>
                         public class TestClass { }
                         """;
        var root = GetRoot(sourceCode);
        var classNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(classNode);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("This is a test class for documentation parsing.", result.Summary);
        Assert.Contains("<summary>", result.RawContent, StringComparison.InvariantCulture);
    }

    [Fact]
    /// <summary>Confirms that method parameter and return documentation is parsed.</summary>
    public void ParseDocumentationShouldParseMethodWithParameters()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                             /// <summary>
                             /// Processes the given data.
                             /// </summary>
                             /// <param name="data">The input data to process.</param>
                             /// <param name="options">Configuration options for processing.</param>
                             /// <returns>The processed result as a string.</returns>
                             public string Process(string data, int options) => "";
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.Equal("Processes the given data.", result.Summary);
        Assert.Equal("The processed result as a string.", result.Returns);
        Assert.Equal(2, result.Parameters.Count);

        var dataParam = result.Parameters.FirstOrDefault(p => p.Name == "data");
        var optionsParam = result.Parameters.FirstOrDefault(p => p.Name == "options");

        Assert.NotNull(dataParam);
        Assert.NotNull(optionsParam);
        Assert.Equal("The input data to process.", dataParam.Description);
        Assert.Equal("Configuration options for processing.", optionsParam.Description);
    }

    [Fact]
    /// <summary>Confirms that remarks and example documentation sections are parsed.</summary>
    public void ParseDocumentationShouldParseExampleAndRemarks()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         /// <summary>
                         /// A sample method for testing.
                         /// </summary>
                         /// <remarks>
                         /// This method is used for unit testing purposes only.
                         /// Do not use in production code.
                         /// </remarks>
                         /// <example>
                         /// var result = TestMethod();
                         /// Console.WriteLine(result);
                         /// </example>
                         public void TestMethod() { }
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.Equal("A sample method for testing.", result.Summary);
        Assert.Contains("This method is used for unit testing purposes only.", result.Remarks,
            StringComparison.InvariantCulture);
        Assert.Contains("var result = TestMethod();", result.Example, StringComparison.InvariantCulture);
        Assert.Contains("Console.WriteLine(result);", result.Example, StringComparison.InvariantCulture);
    }

    [Fact]
    /// <summary>Confirms that see cref tags are rendered inside summaries, parameters, and returns.</summary>
    public void ParseDocumentationShouldHandleSeeCrefTags()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         /// <summary>
                         /// Creates a new instance of <see cref="System.Text.StringBuilder"/>.
                         /// </summary>
                         /// <param name="value">Initial value for the <see cref="StringBuilder"/>.</param>
                         /// <returns>A new <see cref="System.Collections.Generic.List{T}"/> instance.</returns>
                         public object CreateInstance(string value) => null;
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.Contains("`StringBuilder`", result.Summary, StringComparison.InvariantCulture);
        Assert.Contains("`StringBuilder`", result.Parameters[0].Description, StringComparison.InvariantCulture);
        Assert.Contains("`List{T}`", result.Returns, StringComparison.InvariantCulture);
    }

    [Fact]
    /// <summary>Confirms that c and code tags are rendered as inline code.</summary>
    public void ParseDocumentationShouldHandleCodeTags()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         /// <summary>
                         /// Use <c>Console.WriteLine</c> to output text.
                         /// For multiline code use <code>var x = 10; Console.WriteLine(x);</code> instead.
                         /// </summary>
                         public void TestMethod() { }
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.Contains("`Console.WriteLine`", result.Summary, StringComparison.InvariantCulture);
        Assert.Contains("`var x = 10; Console.WriteLine(x);`", result.Summary, StringComparison.InvariantCulture);
    }

    [Fact]
    /// <summary>Confirms that paramref tags are rendered inside summaries.</summary>
    public void ParseDocumentationShouldHandleParamrefTags()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         /// <summary>
                         /// Processes the <paramref name="input"/> parameter.
                         /// </summary>
                         /// <param name="input">The data to process.</param>
                         public void Process(string input) { }
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.Contains("`input`", result.Summary, StringComparison.InvariantCulture);
    }

    [Fact]
    /// <summary>Confirms that see tags with inner text are rendered.</summary>
    public void ParseDocumentationShouldHandleSeeTagsWithText()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         /// <summary>
                         /// See <see cref="System.String">string documentation</see> for details.
                         /// </summary>
                         public void TestMethod() { }
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.Contains("string documentation", result.Summary, StringComparison.InvariantCulture);
    }

    [Fact]
    /// <summary>Confirms that whitespace in documentation is normalized.</summary>
    public void ParseDocumentationShouldNormalizeWhitespace()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         /// <summary>
                         /// This    has    multiple     spaces
                         /// and multiple
                         /// lines with    extra   whitespace.
                         /// </summary>
                         public void TestMethod() { }
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.DoesNotContain("    ", result.Summary,
            StringComparison.InvariantCulture); // Multiple spaces should be normalized
        Assert.Contains("This has multiple spaces and multiple lines with extra whitespace.", result.Summary,
            StringComparison.InvariantCulture);
    }

    [Fact]
    /// <summary>Confirms that invalid XML documentation falls back to raw content.</summary>
    public void ParseDocumentationShouldHandleInvalidXml()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         /// <summary>
                         /// This has invalid XML <unclosed tag
                         /// </summary>
                         public void TestMethod() { }
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act & Assert - Should not throw exception
        var result = DocumentationParser.ParseDocumentation(methodNode);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Summary); // Should fall back to raw content as summary
    }

    [Fact]
    /// <summary>Confirms that regular single-line comments are parsed as documentation.</summary>
    public void ParseDocumentationShouldParseRegularComments()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         // This is a regular single-line comment
                         // with multiple lines
                         public void TestMethod() { }
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("This is a regular single-line comment", result.Summary, StringComparison.InvariantCulture);
        Assert.Contains("with multiple lines", result.Summary, StringComparison.InvariantCulture);
    }

    [Fact]
    /// <summary>Confirms that multi-line block comments are parsed as documentation.</summary>
    public void ParseDocumentationShouldParseMultiLineComments()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         /*
                          * This is a multi-line comment
                          * spanning several lines
                          */
                         public void TestMethod() { }
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("This is a multi-line comment", result.Summary, StringComparison.InvariantCulture);
        Assert.Contains("spanning several lines", result.Summary, StringComparison.InvariantCulture);
    }

    [Fact]
    /// <summary>Confirms that empty XML tags produce empty documentation values.</summary>
    public void ParseDocumentationShouldHandleEmptyXmlTags()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         /// <summary></summary>
                         /// <param name="data"></param>
                         /// <returns></returns>
                         public string TestMethod(string data) => "";
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.Empty(result.Summary);
        Assert.Empty(result.Returns);
        Assert.Single(result.Parameters);
        Assert.Equal("data", result.Parameters[0].Name);
        Assert.Empty(result.Parameters[0].Description);
    }

    [Fact]
    /// <summary>Confirms that the raw XML content is preserved during parsing.</summary>
    public void ParseDocumentationShouldPreserveRawContent()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         /// <summary>
                         /// Test summary with <see cref="System.String"/> references.
                         /// </summary>
                         /// <param name="input">Input parameter.</param>
                         public void TestMethod(string input) { }
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.Contains("<summary>", result.RawContent, StringComparison.InvariantCulture);
        Assert.Contains("<see cref=\"System.String\"/>", result.RawContent, StringComparison.InvariantCulture);
        Assert.Contains("<param name=\"input\">", result.RawContent, StringComparison.InvariantCulture);
    }

    [Fact]
    /// <summary>Confirms that nested XML tags are parsed into all documentation sections.</summary>
    public void ParseDocumentationShouldHandleComplexXmlStructure()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                         /// <summary>
                         /// Complex method with nested tags and <see cref="List{T}">generic references</see>.
                         /// Uses <c>async/await</c> pattern for <paramref name="data"/> processing.
                         /// </summary>
                         /// <param name="data">The <c>string</c> data to process.</param>
                         /// <param name="timeout">Timeout in milliseconds (default: <c>5000</c>).</param>
                         /// <returns>
                         /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
                         /// The task result contains the processed <see cref="System.String"/>.
                         /// </returns>
                         /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
                         /// <exception cref="TimeoutException">Thrown when operation exceeds <paramref name="timeout"/>.</exception>
                         /// <remarks>
                         /// This method demonstrates complex XML documentation parsing.
                         /// It includes multiple types of references and formatting.
                         /// </remarks>
                         /// <example>
                         /// <code>
                         /// var result = await ProcessAsync("test data", 10000);
                         /// Console.WriteLine($"Result: {result}");
                         /// </code>
                         /// </example>
                         public async Task<string> ProcessAsync(string data, int timeout = 5000) => "";
                         }
                         """;
        var root = GetRoot(sourceCode);
        var methodNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(methodNode);

        // Assert
        Assert.Contains("Complex method with nested tags", result.Summary, StringComparison.InvariantCulture);
        Assert.Contains("`async/await`", result.Summary, StringComparison.InvariantCulture);
        Assert.Contains("`data`", result.Summary, StringComparison.InvariantCulture);

        Assert.Equal(2, result.Parameters.Count);
        var dataParam = result.Parameters.First(p => p.Name == "data");
        var timeoutParam = result.Parameters.First(p => p.Name == "timeout");

        Assert.Contains("`string`", dataParam.Description, StringComparison.InvariantCulture);
        Assert.Contains("default: `5000`", timeoutParam.Description, StringComparison.InvariantCulture);

        Assert.Contains("`Task{TResult}`", result.Returns, StringComparison.InvariantCulture);
        Assert.Contains("`String`", result.Returns, StringComparison.InvariantCulture);

        Assert.Contains("demonstrates complex XML", result.Remarks, StringComparison.InvariantCulture);
        Assert.Contains("var result = await ProcessAsync", result.Example, StringComparison.InvariantCulture);
    }

    [Fact]
    /// <summary>Confirms that property documentation is parsed.</summary>
    public void ParseDocumentationShouldHandlePropertyDocumentation()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                             /// <summary>
                             /// Gets or sets the name value.
                             /// </summary>
                             /// <value>A string representing the name.</value>
                             public string Name { get; set; }
                         }
                         """;
        var root = GetRoot(sourceCode);
        var propertyNode = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>().First();

        // Act
        var result = DocumentationParser.ParseDocumentation(propertyNode);

        // Assert
        Assert.Equal("Gets or sets the name value.", result.Summary);
        Assert.NotEmpty(result.RawContent);
    }

    [Fact]
    /// <summary>Confirms that field documentation is parsed.</summary>
    public void ParseDocumentationShouldHandleFieldDocumentation()
    {
        // Arrange
        var sourceCode = """
                         public class TestClass
                         {
                             /// <summary>
                             /// A constant representing the default timeout value.
                             /// </summary>
                             public const int DefaultTimeout = 5000;
                         }
                         """;
        var root = GetRoot(sourceCode);
        var fieldNode = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>()
            .First();

        // Act
        var result = DocumentationParser.ParseDocumentation(fieldNode);

        // Assert
        Assert.Equal("A constant representing the default timeout value.", result.Summary);
    }

    [Fact]
    /// <summary>Confirms that enum value documentation is parsed.</summary>
    public void ParseDocumentationShouldHandleEnumValueDocumentation()
    {
        // Arrange
        var sourceCode = """
                         public enum Status
                         {
                             /// <summary>
                             /// Indicates an active state.
                             /// </summary>
                             Active,

                             /// <summary>
                             /// Indicates an inactive state.
                             /// </summary>
                             Inactive
                         }
                         """;
        var root = GetRoot(sourceCode);
        var enumMemberNode = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.EnumMemberDeclarationSyntax>().First();

        // Act
        var result = DocumentationParser.ParseDocumentation(enumMemberNode);

        // Assert
        Assert.Equal("Indicates an active state.", result.Summary);
    }
}
