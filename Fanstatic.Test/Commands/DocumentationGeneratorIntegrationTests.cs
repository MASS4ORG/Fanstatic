using Serilog;
using Fanstatic.Commands.API;
using Fanstatic.Commands.API.APIModels;
using Fanstatic.Models;
using Xunit;

namespace Fanstatic.Test.Commands;

public class DocumentationGeneratorIntegrationTests : CodeAnalysisTestBase
{
    [Fact]
    public async Task DocumentationGenerator_ShouldMergePartialClasses()
    {
        // Arrange
        var partialTestPath = Path.Combine(Path.GetDirectoryName(TestProjectPath)!, "12-partial-classes");
        var outputPath = Path.Combine(Path.GetTempPath(), "test-docs-" + Guid.NewGuid().ToString("N")[..8]);

        var options = new ApiGeneratorOptions
        {
            SourceProjects = [partialTestPath],
            Output = "api",
            OutputPolicy = OutputPolicy.delete
        };

        var logger = new LoggerConfiguration()
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();

        // Act
        var structure = await Analyzer.AnalyzeProjectAsync(partialTestPath);
        var generator = new DocumentationGenerator(outputPath, OutputPolicy.delete, logger);

        // Assert - Before merging, we should have 3 partial class entries
        Assert.Equal(3, structure.AllClasses.Count(c => c.Name == "PartialTestClass"));

        // Generate documentation (this internally merges partial classes)
        await generator.GenerateDocumentationAsync(structure, options);

        // Check that the generated documentation exists
        var generatedFiles = Directory.GetFiles(outputPath, "*.md");
        Assert.NotEmpty(generatedFiles);

        // Check that there's only one file for the merged partial class
        var partialClassFiles = generatedFiles
            .Where(f => Path.GetFileName(f).Contains("PartialTestClass", StringComparison.Ordinal)).ToList();
        Assert.Single(partialClassFiles);

        // Read the generated file and verify it contains merged content
        var partialClassFile = partialClassFiles[0];
        var content = await File.ReadAllTextAsync(partialClassFile, TestContext.Current.CancellationToken);

        // Verify the source file parameter contains all three source files
        Assert.Contains("PartialClass1.cs", content, StringComparison.Ordinal);
        Assert.Contains("PartialClass2.cs", content, StringComparison.Ordinal);
        Assert.Contains("PartialClass3.cs", content, StringComparison.Ordinal);

        // Verify it contains content from all three partial files
        Assert.Contains("FirstMethod", content, StringComparison.Ordinal);
        Assert.Contains("SecondMethod", content, StringComparison.Ordinal);
        Assert.Contains("ThirdMethod", content, StringComparison.Ordinal);
        Assert.Contains("StaticMethod", content, StringComparison.Ordinal);

        Assert.Contains("FirstProperty", content, StringComparison.Ordinal);
        Assert.Contains("SecondProperty", content, StringComparison.Ordinal);
        Assert.Contains("ThirdProperty", content, StringComparison.Ordinal);

        Assert.Contains("FirstField", content, StringComparison.Ordinal);
        Assert.Contains("SecondField", content, StringComparison.Ordinal);
        Assert.Contains("ThirdField", content, StringComparison.Ordinal);

        // Cleanup
        Directory.Delete(outputPath, recursive: true);
    }

    [Fact]
    public async Task DocumentationGenerator_ShouldIncludeSourceFileInFrontMatter()
    {
        // Arrange
        var outputPath = Path.Combine(Path.GetTempPath(), "test-docs-" + Guid.NewGuid().ToString("N")[..8]);

        var options = new ApiGeneratorOptions
        {
            SourceProjects = [TestProjectPath],
            Output = "api",
            OutputPolicy = OutputPolicy.delete
        };

        var logger = new LoggerConfiguration()
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();

        // Act
        var structure = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var generator = new DocumentationGenerator(outputPath, OutputPolicy.delete, logger);
        await generator.GenerateDocumentationAsync(structure, options);

        // Assert
        var generatedFiles = Directory.GetFiles(outputPath, "*.md");
        Assert.NotEmpty(generatedFiles);

        // Check a few generated files to ensure they have the source parameter
        var sampleClassFile =
            generatedFiles.FirstOrDefault(f => Path.GetFileName(f).Contains("SampleClass", StringComparison.Ordinal));
        if (sampleClassFile != null)
        {
            var content = await File.ReadAllTextAsync(sampleClassFile, TestContext.Current.CancellationToken);
            Assert.Contains("source:", content, StringComparison.Ordinal);
            Assert.Contains("SampleClass.cs", content, StringComparison.Ordinal);
        }

        var userRecordFile =
            generatedFiles.FirstOrDefault(f => Path.GetFileName(f).Contains("UserRecord", StringComparison.Ordinal));
        if (userRecordFile != null)
        {
            var content = await File.ReadAllTextAsync(userRecordFile, TestContext.Current.CancellationToken);
            Assert.Contains("source:", content, StringComparison.Ordinal);
            Assert.Contains("SampleRecord.cs", content, StringComparison.Ordinal);
        }

        // Cleanup
        Directory.Delete(outputPath, recursive: true);
    }

    [Fact]
    public async Task DocumentationGenerator_ShouldGenerateCorrectCounts()
    {
        // Arrange
        var partialTestPath = Path.Combine(Path.GetDirectoryName(TestProjectPath)!, "12-partial-classes");
        var outputPath = Path.Combine(Path.GetTempPath(), "test-docs-" + Guid.NewGuid().ToString("N")[..8]);

        var options = new ApiGeneratorOptions
        {
            SourceProjects = [partialTestPath],
            Output = "api",
            OutputPolicy = OutputPolicy.delete
        };

        var logger = new LoggerConfiguration()
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();

        // Act
        var structure = await Analyzer.AnalyzeProjectAsync(partialTestPath);
        var generator = new DocumentationGenerator(outputPath, OutputPolicy.delete, logger);
        await generator.GenerateDocumentationAsync(structure, options);

        // Assert - Check the index file for correct counts
        var indexFile = Path.Combine(outputPath, "_index.md");
        Assert.True(File.Exists(indexFile));

        var indexContent = await File.ReadAllTextAsync(indexFile, TestContext.Current.CancellationToken);

        // Should show 1 unique type after merging (not 3 separate entries)
        Assert.Contains("1 type(s)", indexContent, StringComparison.Ordinal);

        // Cleanup
        Directory.Delete(outputPath, recursive: true);
    }

    [Fact]
    public async Task DocumentationGenerator_ShouldHandleMultipleProjects()
    {
        // Arrange
        var partialTestPath = Path.Combine(Path.GetDirectoryName(TestProjectPath)!, "12-partial-classes");
        var outputPath = Path.Combine(Path.GetTempPath(), "test-docs-" + Guid.NewGuid().ToString("N")[..8]);

        var options = new ApiGeneratorOptions
        {
            SourceProjects = [TestProjectPath, partialTestPath],
            Output = "api",
            OutputPolicy = OutputPolicy.delete,
            ExternalLink = "github.com/user/repo"
        };

        var logger = new LoggerConfiguration()
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();

        // Act - Analyze multiple projects
        var analyzer = new CodeAnalyzer(logger);
        var structure1 = await analyzer.AnalyzeProjectAsync(TestProjectPath);
        var structure2 = await analyzer.AnalyzeProjectAsync(partialTestPath);

        // Combine structures manually (simulating what ApiGeneratorCommand does)
        var combinedStructure = new ProjectStructure();
        combinedStructure.AllClasses.AddRange(structure1.AllClasses);
        combinedStructure.AllClasses.AddRange(structure2.AllClasses);

        foreach (var kvp in structure1.NamespaceClasses)
        {
            combinedStructure.NamespaceClasses[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in structure2.NamespaceClasses)
        {
            if (!combinedStructure.NamespaceClasses.ContainsKey(kvp.Key))
            {
                combinedStructure.NamespaceClasses[kvp.Key] = new List<ClassInfo>();
            }

            combinedStructure.NamespaceClasses[kvp.Key].AddRange(kvp.Value);
        }

        var generator = new DocumentationGenerator(outputPath, OutputPolicy.delete, logger);
        await generator.GenerateDocumentationAsync(combinedStructure, options);

        // Assert - Check that classes from both projects are included
        var generatedFiles = Directory.GetFiles(outputPath, "*.md");
        Assert.NotEmpty(generatedFiles);

        // Should have classes from both projects
        var sampleClassFile =
            generatedFiles.FirstOrDefault(f => Path.GetFileName(f).Contains("SampleClass", StringComparison.Ordinal));
        var partialClassFile = generatedFiles.FirstOrDefault(f =>
            Path.GetFileName(f).Contains("PartialTestClass", StringComparison.Ordinal));

        Assert.NotNull(sampleClassFile);
        Assert.NotNull(partialClassFile);

        // Check external links are included
        var sampleContent = await File.ReadAllTextAsync(sampleClassFile, TestContext.Current.CancellationToken);
        var partialContent = await File.ReadAllTextAsync(partialClassFile, TestContext.Current.CancellationToken);

        Assert.Contains("external_link: \"github.com/user/repo\"", sampleContent, StringComparison.Ordinal);
        Assert.Contains("external_link: \"github.com/user/repo\"", partialContent, StringComparison.Ordinal);

        // Check that source files are linked
        Assert.Contains("[SampleClass.cs](https://github.com/user/repo/blob/main/SampleClass.cs)", sampleContent,
            StringComparison.Ordinal);

        // For partial classes, should have multiple linked files
        Assert.Contains("[PartialClass1.cs](https://github.com/user/repo/blob/main/PartialClass1.cs)", partialContent,
            StringComparison.Ordinal);
        Assert.Contains("[PartialClass2.cs](https://github.com/user/repo/blob/main/PartialClass2.cs)", partialContent,
            StringComparison.Ordinal);
        Assert.Contains("[PartialClass3.cs](https://github.com/user/repo/blob/main/PartialClass3.cs)", partialContent,
            StringComparison.Ordinal);

        // Cleanup
        Directory.Delete(outputPath, recursive: true);
    }
}
