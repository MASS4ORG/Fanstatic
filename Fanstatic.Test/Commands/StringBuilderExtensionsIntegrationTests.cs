using System.Text;
using Serilog;
using Fanstatic.Commands.API;
using Fanstatic.Commands.API.APIModels;
using Xunit;

namespace Fanstatic.Test.Commands;

public class StringBuilderExtensionsIntegrationTests
{
    readonly CodeAnalyzer _analyzer;
    readonly ApiGeneratorOptions _options;
    const string TestProjectPath = "../../../.TestSites/10-cs-project";

    public StringBuilderExtensionsIntegrationTests()
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        _analyzer = new CodeAnalyzer(logger);
        _options = new ApiGeneratorOptions { Output = "api" };
    }

    [Fact]
    public async Task GenerateCompleteClassDocumentation_ShouldProduceValidMarkdown()
    {
        // Arrange
        var result = await _analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");
        var sb = new StringBuilder();

        // Act
        sb.AppendFrontMatter($"{sampleClass.TypeKind} {sampleClass.Name}", sampleClass.TypeKind,
                ("namespace", sampleClass.Namespace),
                ("sourceFile", sampleClass.SourceFile))
            .AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"# {sampleClass.Name}")
            .AppendLine()
            .AppendHeader(sampleClass, _options)
            .AppendClassDocumentation(sampleClass)
            .AppendFields(sampleClass)
            .AppendProperties(sampleClass)
            .AppendMethods(sampleClass);

        var markdown = sb.ToString();

        // Assert
        Assert.Contains("---", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Title: \"class SampleClass\"", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Type: class", markdown, StringComparison.InvariantCulture);
        Assert.Contains("# SampleClass", markdown, StringComparison.InvariantCulture);
        Assert.Contains("- **Namespace:** [TestProject.Models](/api/testproject.models)", markdown,
            StringComparison.InvariantCulture);
        Assert.Contains("- **Source File:** SampleClass.cs", markdown, StringComparison.InvariantCulture);
        Assert.Contains("sample class used for testing", markdown, StringComparison.InvariantCulture);
        Assert.Contains("## Fields", markdown, StringComparison.InvariantCulture);
        Assert.Contains("## Properties", markdown, StringComparison.InvariantCulture);
        Assert.Contains("## Public Methods", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### ProcessData", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### Calculate", markdown, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task GenerateEnumDocumentation_ShouldProduceValidMarkdown()
    {
        // Arrange
        var result = await _analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleStatus = result.AllClasses.First(c => c.Name == "SampleStatus");
        var sb = new StringBuilder();

        // Act
        sb.AppendFrontMatter($"{sampleStatus.TypeKind} {sampleStatus.Name}", sampleStatus.TypeKind,
                ("namespace", sampleStatus.Namespace),
                ("sourceFile", sampleStatus.SourceFile))
            .AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"# {sampleStatus.Name}")
            .AppendLine()
            .AppendHeader(sampleStatus, _options)
            .AppendClassDocumentation(sampleStatus)
            .AppendEnumValues(sampleStatus);

        var markdown = sb.ToString();

        // Assert
        Assert.Contains("Title: \"enum SampleStatus\"", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Type: enum", markdown, StringComparison.InvariantCulture);
        Assert.Contains("# SampleStatus", markdown, StringComparison.InvariantCulture);
        Assert.Contains("## Enum Values", markdown, StringComparison.InvariantCulture);
        Assert.Contains("- **None** = `0`: Indicates that the entity has not been initialized", markdown,
            StringComparison.InvariantCulture);
        Assert.Contains("- **Pending** = `1`: Indicates that the entity is currently being processed", markdown,
            StringComparison.InvariantCulture);
        Assert.Contains("- **Completed** = `100`: Indicates that the entity has completed", markdown,
            StringComparison.InvariantCulture);
        Assert.Contains("- **Failed** = `999`: Indicates that the entity has failed", markdown,
            StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task GenerateRecordDocumentation_ShouldProduceValidMarkdown()
    {
        // Arrange
        var result = await _analyzer.AnalyzeProjectAsync(TestProjectPath);
        var userRecord = result.AllClasses.First(c => c.Name == "UserRecord");
        var sb = new StringBuilder();

        // Act
        sb.AppendFrontMatter($"{userRecord.TypeKind} {userRecord.Name}", userRecord.TypeKind,
                ("namespace", userRecord.Namespace),
                ("sourceFile", userRecord.SourceFile))
            .AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"# {userRecord.Name}")
            .AppendLine()
            .AppendHeader(userRecord, _options)
            .AppendClassDocumentation(userRecord)
            .AppendProperties(userRecord)
            .AppendMethods(userRecord);

        var markdown = sb.ToString();

        // Assert
        Assert.Contains("Title: \"record UserRecord\"", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Type: record", markdown, StringComparison.InvariantCulture);
        Assert.Contains("# UserRecord", markdown, StringComparison.InvariantCulture);
        Assert.Contains("data transfer object", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### Remarks", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Records provide value-based equality", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### Example", markdown, StringComparison.InvariantCulture);
        Assert.Contains("var user = new UserRecord", markdown, StringComparison.InvariantCulture);
        Assert.Contains("## Public Methods", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### IsValid", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### ToDisplayString", markdown, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task GenerateStructDocumentation_ShouldProduceValidMarkdown()
    {
        // Arrange
        var result = await _analyzer.AnalyzeProjectAsync(TestProjectPath);
        var dimensions = result.AllClasses.First(c => c.Name == "Dimensions");
        var sb = new StringBuilder();

        // Act
        sb.AppendFrontMatter($"{dimensions.TypeKind} {dimensions.Name}", dimensions.TypeKind,
                ("namespace", dimensions.Namespace),
                ("sourceFile", dimensions.SourceFile))
            .AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"# {dimensions.Name}")
            .AppendLine()
            .AppendHeader(dimensions, _options)
            .AppendClassDocumentation(dimensions)
            .AppendFields(dimensions)
            .AppendProperties(dimensions)
            .AppendMethods(dimensions);

        var markdown = sb.ToString();

        // Assert
        Assert.Contains("Title: \"struct Dimensions\"", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Type: struct", markdown, StringComparison.InvariantCulture);
        Assert.Contains("# Dimensions", markdown, StringComparison.InvariantCulture);
        Assert.Contains("lightweight data structure", markdown, StringComparison.InvariantCulture);
        Assert.Contains("## Fields", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### **MaxWidth**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### **MaxHeight**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("## Properties", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### **Width**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### **Height**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### **AspectRatio**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("## Public Methods", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### CalculateArea", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### Scale", markdown, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task GenerateInterfaceDocumentation_ShouldProduceValidMarkdown()
    {
        // Arrange
        var result = await _analyzer.AnalyzeProjectAsync(TestProjectPath);
        var dataService = result.AllClasses.First(c => c.Name == "IDataService");
        var sb = new StringBuilder();

        // Act
        sb.AppendFrontMatter($"{dataService.TypeKind} {dataService.Name}", dataService.TypeKind,
                ("namespace", dataService.Namespace),
                ("sourceFile", dataService.SourceFile))
            .AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"# {dataService.Name}")
            .AppendLine()
            .AppendHeader(dataService, _options)
            .AppendClassDocumentation(dataService)
            .AppendMethods(dataService);

        var markdown = sb.ToString();

        // Assert
        Assert.Contains("Title: \"interface IDataService\"", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Type: interface", markdown, StringComparison.InvariantCulture);
        Assert.Contains("# IDataService", markdown, StringComparison.InvariantCulture);
        Assert.Contains("contract for data service operations", markdown, StringComparison.InvariantCulture);
        Assert.Contains("## Public Methods", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### GetUserAsync", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### CreateUserAsync", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### UpdateUserAsync", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### DeleteUserAsync", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Task<UserRecord?>", markdown, StringComparison.InvariantCulture);
        Assert.Contains("**Parameters:**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("**Returns:**", markdown, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task GenerateNamespaceOverview_ShouldProduceValidMarkdown()
    {
        // Arrange
        var result = await _analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sb = new StringBuilder();

        // Act
        sb.AppendFrontMatter("API Documentation", "api-overview")
            .AppendLine("# API Documentation")
            .AppendLine()
            .AppendLine("## Namespaces")
            .AppendLine()
            .AppendNamespaceList(result.NamespaceClasses, _options)
            .AppendLine()
            .AppendLine("## All Types")
            .AppendLine()
            .AppendTypeList(result.AllClasses);

        var markdown = sb.ToString();

        // Assert
        Assert.Contains("Title: \"API Documentation\"", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Type: api-overview", markdown, StringComparison.InvariantCulture);
        Assert.Contains("## Namespaces", markdown, StringComparison.InvariantCulture);
        Assert.Contains("- [TestProject.Models](/api/testproject.models)", markdown, StringComparison.InvariantCulture);
        Assert.Contains("- [TestProject.Services](/api/testproject.services)", markdown,
            StringComparison.InvariantCulture);
        Assert.Contains("## All Types", markdown, StringComparison.InvariantCulture);
        Assert.Contains("🄲 [SampleClass](./sampleclass)", markdown, StringComparison.InvariantCulture);
        Assert.Contains("🅁 [UserRecord](./userrecord)", markdown, StringComparison.InvariantCulture);
        Assert.Contains("🅂 [Dimensions](./dimensions)", markdown, StringComparison.InvariantCulture);
        Assert.Contains("🄴 [SampleStatus](./samplestatus)", markdown, StringComparison.InvariantCulture);
        Assert.Contains("🄸 [IDataService](./idataservice)", markdown, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task GenerateNamespaceDocumentation_ShouldProduceValidMarkdown()
    {
        // Arrange
        var result = await _analyzer.AnalyzeProjectAsync(TestProjectPath);
        var modelsNamespace = result.NamespaceClasses["TestProject.Models"];
        var sb = new StringBuilder();

        // Act
        sb.AppendFrontMatter("TestProject.Models Namespace", "namespace",
                ("namespace", "TestProject.Models"))
            .AppendLine("# TestProject.Models")
            .AppendLine()
            .AppendLine("Types in this namespace:")
            .AppendLine()
            .AppendTypeList(modelsNamespace);

        var markdown = sb.ToString();

        // Assert
        Assert.Contains("Title: \"TestProject.Models Namespace\"", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Type: namespace", markdown, StringComparison.InvariantCulture);
        Assert.Contains("# TestProject.Models", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Types in this namespace:", markdown, StringComparison.InvariantCulture);
        Assert.Contains("🄲 [SampleClass](./sampleclass)", markdown, StringComparison.InvariantCulture);
        Assert.Contains("🅁 [UserRecord](./userrecord)", markdown, StringComparison.InvariantCulture);
        Assert.Contains("🄴 [SampleStatus](./samplestatus)", markdown, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task GenerateMethodDocumentationWithComplexSignature_ShouldProduceValidMarkdown()
    {
        // Arrange
        var result = await _analyzer.AnalyzeProjectAsync(TestProjectPath);
        var dataService = result.AllClasses.First(c => c.Name == "IDataService");
        var getUsersMethod = dataService.PublicMethods.First(m => m.Name == "GetUsersAsync");
        var sb = new StringBuilder();

        // Act - Generate just the method documentation
        sb.AppendLine("## Method Details")
            .AppendLine();

        // Use the internal method generation logic
        var methodDoc = GenerateMethodDocumentation(getUsersMethod);
        sb.Append(methodDoc);

        var markdown = sb.ToString();

        // Assert
        Assert.Contains("## Method Details", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### GetUsersAsync", markdown, StringComparison.InvariantCulture);
        Assert.Contains("```csharp", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Task<IEnumerable<UserRecord>>", markdown, StringComparison.InvariantCulture);
        Assert.Contains("Func<UserRecord, bool>? filter = null", markdown, StringComparison.InvariantCulture);
        Assert.Contains("int maxResults = 100", markdown, StringComparison.InvariantCulture);
        Assert.Contains("```", markdown, StringComparison.InvariantCulture);
        Assert.Contains("**Parameters:**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("- `filter` (*Func<UserRecord, bool>?*)", markdown, StringComparison.InvariantCulture);
        Assert.Contains("- `maxResults` (*int*)", markdown, StringComparison.InvariantCulture);
        Assert.Contains("(Default: `100`)", markdown, StringComparison.InvariantCulture);
        Assert.Contains("**Returns:** `Task<IEnumerable<UserRecord>>`", markdown, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task GeneratePropertyDocumentationWithAccessors_ShouldProduceValidMarkdown()
    {
        // Arrange
        var result = await _analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");
        var sb = new StringBuilder();

        // Act
        sb.AppendLine("## Properties")
            .AppendLine()
            .AppendProperties(sampleClass);

        var markdown = sb.ToString();

        // Assert
        Assert.Contains("## Properties", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### **Name**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("(*string*) { get; set }", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### **Id**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("(*int*) { get;", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### **DisplayName**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### **CreatedAt**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("(*DateTime*) { get; set }", markdown, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task GenerateFieldDocumentationWithModifiers_ShouldProduceValidMarkdown()
    {
        // Arrange
        var result = await _analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");
        var sb = new StringBuilder();

        // Act
        sb.AppendLine("## Fields")
            .AppendLine()
            .AppendFields(sampleClass);

        var markdown = sb.ToString();

        // Assert
        Assert.Contains("## Fields", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### **DefaultPrefix**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("(*string*)", markdown, StringComparison.InvariantCulture);
        Assert.Contains("### **PublicField**", markdown, StringComparison.InvariantCulture);
        Assert.Contains("(*int*) = `100`", markdown, StringComparison.InvariantCulture);
    }

    [Theory]
    [InlineData("SampleClass", "class")]
    [InlineData("UserRecord", "record")]
    [InlineData("Dimensions", "struct")]
    [InlineData("SampleStatus", "enum")]
    [InlineData("IDataService", "interface")]
    public async Task GenerateDocumentationForDifferentTypeKinds_ShouldProduceCorrectMarkdown(string typeName,
        string expectedTypeKind)
    {
        // Arrange
        var result = await _analyzer.AnalyzeProjectAsync(TestProjectPath);
        var type = result.AllClasses.First(c => c.Name == typeName);
        var sb = new StringBuilder();

        // Act
        sb.AppendFrontMatter($"{type.TypeKind} {type.Name}", type.TypeKind)
            .AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"# {type.Name}")
            .AppendHeader(type, _options);

        var markdown = sb.ToString();

        // Assert
        Assert.Contains($"Title: \"{expectedTypeKind} {typeName}\"", markdown, StringComparison.InvariantCulture);
        Assert.Contains($"Type: {expectedTypeKind}", markdown, StringComparison.InvariantCulture);
        Assert.Contains($"# {typeName}", markdown, StringComparison.InvariantCulture);
        Assert.Contains($"- **Namespace:** [{type.Namespace}]", markdown, StringComparison.InvariantCulture);
        Assert.Contains($"- **Source File:** {type.SourceFile}", markdown, StringComparison.InvariantCulture);
    }

    // Helper method to test the internal method generation
    static string GenerateMethodDocumentation(MethodInfo method)
    {
        var sb = new StringBuilder();

        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"### {method.Name}")
            .AppendLine()
            .AppendLine("```csharp")
            .AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                $"{method.Modifiers} {method.ReturnType} {method.Name}({GenerateParameterSignature(method.Parameters)})")
            .AppendLine("```")
            .AppendLine();

        if (!string.IsNullOrEmpty(method.Documentation.Summary))
        {
            sb.AppendLine(method.Documentation.Summary)
                .AppendLine();
        }

        if (method.Parameters.Any())
        {
            sb.AppendLine("**Parameters:**")
                .AppendLine();

            foreach (var param in method.Parameters)
            {
                var paramDoc = method.Documentation.Parameters.FirstOrDefault(p => p.Name == param.Name);
                var description = paramDoc?.Description ?? "";
                var defaultValue = !string.IsNullOrEmpty(param.DefaultValue)
                    ? $" (Default: `{param.DefaultValue}`)"
                    : "";

                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
                    $"- `{param.Name}` (*{param.Type}*){(!string.IsNullOrEmpty(description) ? $": {description}" : "")}{defaultValue}");
            }

            sb.AppendLine();
        }

        if (method.ReturnType != "void")
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"**Returns:** `{method.ReturnType}`")
                .AppendLine();
            if (!string.IsNullOrEmpty(method.Documentation.Returns))
            {
                sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- {method.Documentation.Returns}");
            }
        }

        return sb.ToString();
    }

    static string GenerateParameterSignature(List<ParameterInfo> parameters) =>
        parameters.Any()
            ? string.Join(", ", parameters.Select(p =>
                $"{p.Type} {p.Name}" + (!string.IsNullOrEmpty(p.DefaultValue) ? $" = {p.DefaultValue}" : "")))
            : "";
}
