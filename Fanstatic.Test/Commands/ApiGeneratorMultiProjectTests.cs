using System.Text;
using Serilog;
using Fanstatic.Commands.API;
using Fanstatic.Commands.API.APIModels;
using Xunit;

namespace Fanstatic.Test.Commands;

public class ApiGeneratorMultiProjectTests : CodeAnalysisTestBase
{
    [Fact]
    public void GetProjectPaths_ShouldReturnSinglePath()
    {
        // Arrange
        var options = new ApiGeneratorOptions
        {
            SourceProjects = ["./single-project"]
        };

        // Act
        var paths = options.SourceProjects.ToList();

        // Assert
        Assert.Single(paths);
        Assert.Equal("./single-project", paths[0]);
    }

    [Fact]
    public void GetProjectPaths_ShouldReturnMultiplePaths()
    {
        // Arrange
        var options = new ApiGeneratorOptions
        {
            SourceProjects = ["./project1", "./project2", "./project3"]
        };

        // Act
        var paths = options.SourceProjects.ToList();

        // Assert
        Assert.Equal(3, paths.Count);
        Assert.Equal("./project1", paths[0]);
        Assert.Equal("./project2", paths[1]);
        Assert.Equal("./project3", paths[2]);
    }

    [Fact]
    public async Task AnalyzeMultipleProjects_ShouldMergeResults()
    {
        // Arrange
        var testProject1 = TestProjectPath;
        var testProject2 = Path.Combine(Path.GetDirectoryName(TestProjectPath)!, "12-partial-classes");

        // Create a mock analyzer setup
        var logger = new LoggerConfiguration()
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
        var analyzer = new CodeAnalyzer(logger);

        // Act
        var structure1 = await analyzer.AnalyzeProjectAsync(testProject1);
        var structure2 = await analyzer.AnalyzeProjectAsync(testProject2);

        // Assert - Verify that we get classes from both projects
        Assert.NotEmpty(structure1.AllClasses);
        Assert.NotEmpty(structure2.AllClasses);

        // Verify specific classes exist
        Assert.Contains(structure1.AllClasses, c => c.Name == "SampleClass");
        Assert.Contains(structure2.AllClasses, c => c.Name == "PartialTestClass");
    }

    [Theory]
    [InlineData("github.com/user/repo", "TestFile.cs",
        "[TestFile.cs](https://github.com/user/repo/blob/main/TestFile.cs)")]
    [InlineData("https://github.com/user/repo", "TestFile.cs",
        "[TestFile.cs](https://github.com/user/repo/blob/main/TestFile.cs)")]
    [InlineData("http://github.com/user/repo", "TestFile.cs",
        "[TestFile.cs](http://github.com/user/repo/blob/main/TestFile.cs)")]
    [InlineData("gitlab.com/user/repo", "TestFile.cs",
        "[TestFile.cs](https://gitlab.com/user/repo/blob/main/TestFile.cs)")]
    [InlineData("", "TestFile.cs", "TestFile.cs")]
    [InlineData(null, "TestFile.cs", "TestFile.cs")]
    public void ExternalLink_ShouldFormatCorrectly(string? externalLink, string sourceFile, string expected)
    {
        // Arrange
        var options = new ApiGeneratorOptions
        {
            SourceProjects = ["./test"],
            ExternalLink = externalLink
        };

        var classInfo = new ClassInfo(
            Name: "TestClass",
            FullName: "TestNamespace.TestClass",
            Namespace: "TestNamespace",
            SourceFile: sourceFile,
            TypeKind: "class"
        );

        // Act - Generate documentation content
        var content = GenerateClassContent(classInfo, options);

        // Assert
        if (string.IsNullOrEmpty(externalLink))
        {
            Assert.Contains($"- **Source File:** {sourceFile}", content, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains($"- **Source File:** {expected}", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ExternalLink_ShouldHandleMultipleFiles()
    {
        // Arrange
        var options = new ApiGeneratorOptions
        {
            SourceProjects = ["./test"],
            ExternalLink = "github.com/user/repo"
        };

        var classInfo = new ClassInfo(
            Name: "PartialClass",
            FullName: "TestNamespace.PartialClass",
            Namespace: "TestNamespace",
            SourceFile: "File1.cs, File2.cs, File3.cs",
            TypeKind: "class"
        );

        // Act
        var content = GenerateClassContent(classInfo, options);

        // Assert
        Assert.Contains("[File1.cs](https://github.com/user/repo/blob/main/File1.cs)", content,
            StringComparison.Ordinal);
        Assert.Contains("[File2.cs](https://github.com/user/repo/blob/main/File2.cs)", content,
            StringComparison.Ordinal);
        Assert.Contains("[File3.cs](https://github.com/user/repo/blob/main/File3.cs)", content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalLink_ShouldIncludeInFrontMatter()
    {
        // Arrange
        var options = new ApiGeneratorOptions
        {
            SourceProjects = ["./test"],
            ExternalLink = "github.com/user/repo"
        };

        var classInfo = new ClassInfo(
            Name: "TestClass",
            FullName: "TestNamespace.TestClass",
            Namespace: "TestNamespace",
            SourceFile: "TestFile.cs",
            TypeKind: "class"
        );

        // Act
        var content = GenerateClassContent(classInfo, options);

        // Assert
        Assert.Contains("external_link: \"github.com/user/repo\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalLink_ShouldHandleEmptyInFrontMatter()
    {
        // Arrange
        var options = new ApiGeneratorOptions
        {
            SourceProjects = ["./test"],
            ExternalLink = null
        };

        var classInfo = new ClassInfo(
            Name: "TestClass",
            FullName: "TestNamespace.TestClass",
            Namespace: "TestNamespace",
            SourceFile: "TestFile.cs",
            TypeKind: "class"
        );

        // Act
        var content = GenerateClassContent(classInfo, options);

        // Assert
        Assert.Contains("external_link: \"\"", content, StringComparison.Ordinal);
    }

    static string GenerateClassContent(ClassInfo classInfo, ApiGeneratorOptions options)
    {
        var sb = new StringBuilder();
        return sb.AppendFrontMatter(classInfo.Name, "api",
                ("type", classInfo.Name),
                ("namespace", classInfo.Namespace),
                ("symbol", classInfo.TypeKind),
                ("source", classInfo.SourceFile),
                ("external_link", options.ExternalLink ?? ""))
            .AppendHeader(classInfo, options)
            .AppendClassDocumentation(classInfo)
            .ToString();
    }
}
