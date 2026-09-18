using System.Text;
using Serilog;
using Fanstatic.Commands.API.APIModels;
using Fanstatic.Helpers;
using Fanstatic.Models;

namespace Fanstatic.Commands.API;

/// <summary>
/// The DocumentationGenerator class is responsible for generating documentation
/// for the structure of a project including classes, records, enums, and namespaces.
/// </summary>
public class DocumentationGenerator
{
    readonly string _outputDirectory;
    readonly ILogger _logger;

    /// <summary>
    /// Represents a generator responsible for creating documentation for the project structure,
    /// including classes, records, enums, and namespaces.
    /// </summary>
    /// <param name="outputDirectory">The directory where documentation will be generated</param>
    /// <param name="outputPolicy">Specifies how to handle an existing directory (Delete, Fail, or Overwrite)</param>
    /// <param name="logger"></param>
    public DocumentationGenerator(string outputDirectory, OutputPolicy outputPolicy, ILogger logger)
    {
        _outputDirectory = outputDirectory;
        _logger = logger;

        if (Directory.Exists(_outputDirectory))
        {
            switch (outputPolicy)
            {
                case OutputPolicy.delete:
                    Directory.Delete(_outputDirectory, recursive: true);
                    break;
                case OutputPolicy.fail:
                    throw new InvalidOperationException($"Output directory '{_outputDirectory}' already exists");
            }
        }

        Directory.CreateDirectory(_outputDirectory);
    }

    /// <summary>
    /// Asynchronously generates documentation for the given project structure, including
    /// class, namespace, and index files, and writes them to the specified output directory.
    /// </summary>
    /// <param name="structure">The structure of the project, including all classes and their namespaces, to generate documentation for.</param>
    /// <param name="options">The options for generating the documentation.</param>
    /// <returns>A task that represents the asynchronous documentation generation operation.</returns>
    public async Task GenerateDocumentationAsync(ProjectStructure structure, ApiGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(options);

        // Group partial classes by FullName and merge them
        var groupedClasses = PartialClassMerger.GroupPartialClasses(structure.AllClasses);

        // Generate individual class/record/enum documentation files
        await groupedClasses
            .Select(async classInfo => await GenerateAndWriteClassDocumentationAsync(classInfo, options))
            .ToArray()
            .WhenAll();

        // Generate namespace documentation files
        var groupedNamespaces = PartialClassMerger.GroupNamespaceClasses(structure.NamespaceClasses);
        await groupedNamespaces
            .Select(async kvp => await GenerateAndWriteNamespaceDocumentationAsync(kvp.Key, kvp.Value))
            .ToArray()
            .WhenAll();

        // Generate main index
        await GenerateAndWriteIndexAsync(structure, options);

        _logger.Information($"Documentation generated in: {_outputDirectory}");
    }

    async Task GenerateAndWriteClassDocumentationAsync(ClassInfo classInfo, ApiGeneratorOptions options) =>
        await WriteFileAsync(
            $"{classInfo.FullName.Replace('.', '_')}.md",
            GenerateClassContent(classInfo, options));

    async Task GenerateAndWriteNamespaceDocumentationAsync(string namespaceName, List<ClassInfo> classes) =>
        await WriteFileAsync(
            $"{namespaceName.Replace('.', '_')}.md",
            GenerateNamespaceContent(namespaceName, classes));

    async Task GenerateAndWriteIndexAsync(ProjectStructure structure, ApiGeneratorOptions options) =>
        await WriteFileAsync("_index.md", GenerateIndexContent(structure, options));

    async Task WriteFileAsync(string fileName, string content)
    {
        var filePath = Path.Combine(_outputDirectory, fileName);
        await File.WriteAllTextAsync(filePath, content);
        _logger.Information($"Generated: {fileName}");
    }

    static string GenerateClassContent(ClassInfo classInfo, ApiGeneratorOptions options) =>
        new StringBuilder()
            .AppendFrontMatter(classInfo.Name, "api",
                ("type", classInfo.Name),
                ("namespace", classInfo.Namespace),
                ("symbol", classInfo.TypeKind),
                ("source", classInfo.SourceFile),
                ("external_link", options.ExternalLink ?? ""))
            .AppendHeader(classInfo, options)
            .AppendClassDocumentation(classInfo)
            .AppendEnumValues(classInfo)
            .AppendFields(classInfo)
            .AppendProperties(classInfo)
            .AppendMethods(classInfo)
            .ToString();

    static string GenerateNamespaceContent(string namespaceName, List<ClassInfo> classes) =>
        new StringBuilder()
            .AppendFrontMatter(namespaceName, "api",
                ("type", namespaceName),
                ("namespace", namespaceName),
                ("symbol", "namespace"))
            .AppendLine($"This namespace contains {classes.Count} type(s).")
            .AppendLine()
            .AppendLine("## Types")
            .AppendLine()
            .AppendTypeList(classes)
            .ToString();

    static string GenerateIndexContent(ProjectStructure structure, ApiGeneratorOptions options)
    {
        var groupedClasses = PartialClassMerger.GroupPartialClasses(structure.AllClasses);

        return new StringBuilder()
            .AppendFrontMatter("API", "api",
                ("symbol", "index"))
            .AppendLine(
                $"This documentation contains {groupedClasses.Count} type(s) across {structure.NamespaceClasses.Count} namespace(s).")
            .AppendLine()
            .AppendLine("## Namespaces")
            .AppendLine()
            .AppendNamespaceList(structure.NamespaceClasses, options)
            .ToString();
    }
}
