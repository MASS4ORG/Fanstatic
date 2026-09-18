using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serilog;
using Fanstatic.Commands.API.APIModels;

namespace Fanstatic.Commands.API;

/// <summary>
/// The CodeAnalyzer class provides functionality to analyze the structure of a C# project.
/// It processes all C# files within a given project directory and extracts information about
/// namespaces and classes defined in the project.
/// </summary>
public class CodeAnalyzer(ILogger logger)
{
    /// <summary>
    /// Analyzes the structure of a C# project by processing all .cs files in the specified project directory.
    /// </summary>
    /// <param name="projectPath">The file path to the root directory of the project to analyze.</param>
    /// <returns>Returns a <see cref="ProjectStructure"/> object containing the analyzed structure of namespaces and classes.</returns>
    public async Task<ProjectStructure> AnalyzeProjectAsync(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

        var structure = new ProjectStructure();

        // Find all C# files in the project
        var csharpFiles = GetCSharpFiles(projectPath);

        logger.Information($"Found {csharpFiles.Count} C# files to analyze.");

        foreach (var filePath in csharpFiles)
        {
            try
            {
                var sourceCode = await File.ReadAllTextAsync(filePath);
                var fileStructure = await AnalyzeSourceCodeAsync(sourceCode, filePath);

                structure.AllClasses.AddRange(fileStructure.AllClasses);
                foreach (var kvp in fileStructure.NamespaceClasses)
                {
                    if (!structure.NamespaceClasses.ContainsKey(kvp.Key))
                    {
                        structure.NamespaceClasses[kvp.Key] = new List<ClassInfo>();
                    }

                    structure.NamespaceClasses[kvp.Key].AddRange(kvp.Value);
                }
            }
            catch (Exception ex)
            {
                logger.Information($"Error reading file {filePath}: {ex.Message}");
            }
        }

        return structure;
    }

    static List<string> GetCSharpFiles(string path)
    {
        var files = new List<string>();
        try
        {
            files.AddRange(Directory.GetFiles(path, "*.cs"));
            foreach (var dir in Directory.GetDirectories(path))
            {
                if (dir.Contains("bin") || dir.Contains("obj") || dir.Contains("publish")) continue;
                files.AddRange(GetCSharpFiles(dir));
            }
        }
        catch (UnauthorizedAccessException) { }
        return files;
    }


    /// <summary>
    /// Analyzes the content of a C# source code and returns a project structure for a single file.
    /// This is a drop-in replacement for AnalyzeProjectAsync when analyzing single file content.
    /// </summary>
    /// <param name="sourceCode">The C# source code content to analyze.</param>
    /// <param name="sourceFileName">The name of the source file (for reference purposes).</param>
    /// <returns>Returns a <see cref="ProjectStructure"/> object containing the analyzed structure of the source code.</returns>
    public Task<ProjectStructure> AnalyzeSourceCodeAsync(string sourceCode, string sourceFileName = "Unknown.cs")
    {
        ArgumentNullException.ThrowIfNull(sourceCode);

        var structure = new ProjectStructure();
        var classes = AnalyzeFile(sourceCode, sourceFileName);

        structure.AllClasses.AddRange(classes);

        foreach (var classInfo in classes)
        {
            if (!structure.NamespaceClasses.ContainsKey(classInfo.Namespace))
            {
                structure.NamespaceClasses[classInfo.Namespace] = new List<ClassInfo>();
            }

            structure.NamespaceClasses[classInfo.Namespace].Add(classInfo);
        }

        return Task.FromResult(structure);
    }

    /// <summary>
    /// Analyzes the content of a C# source file and extracts type information.
    /// </summary>
    /// <param name="sourceCode">The C# source code content to analyze.</param>
    /// <param name="sourceFileName">The name of the source file (for reference purposes).</param>
    /// <returns>Returns a list of <see cref="ClassInfo"/> objects representing the types found in the source code.</returns>
    List<ClassInfo> AnalyzeFile(string sourceCode, string sourceFileName = "Unknown.cs")
    {
        var classes = new List<ClassInfo>();

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetCompilationUnitRoot();

            // Find all type declarations (class, struct, enum, record, interface)
            var typeDeclarations = root.DescendantNodes()
                .Where(n => n is ClassDeclarationSyntax ||
                            n is StructDeclarationSyntax ||
                            n is EnumDeclarationSyntax ||
                            n is RecordDeclarationSyntax ||
                            n is InterfaceDeclarationSyntax)
                .ToList();

            foreach (var typeDecl in typeDeclarations)
            {
                var classInfo = AnalyzeType(typeDecl, sourceFileName);
                if (classInfo != null)
                {
                    classes.Add(classInfo);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Information($"Error analyzing source code from {sourceFileName}: {ex.Message}");
        }

        return classes;
    }

    ClassInfo? AnalyzeType(SyntaxNode typeDecl, string sourceFileName)
    {
        string typeName;
        string typeKind;

        switch (typeDecl)
        {
            case ClassDeclarationSyntax classDecl:
                typeName = classDecl.Identifier.ValueText;
                typeKind = "class";
                break;
            case StructDeclarationSyntax structDecl:
                typeName = structDecl.Identifier.ValueText;
                typeKind = "struct";
                break;
            case EnumDeclarationSyntax enumDecl:
                typeName = enumDecl.Identifier.ValueText;
                typeKind = "enum";
                break;
            case RecordDeclarationSyntax recordDecl:
                typeName = recordDecl.Identifier.ValueText;
                typeKind = "record";
                break;
            case InterfaceDeclarationSyntax interfaceDecl:
                typeName = interfaceDecl.Identifier.ValueText;
                typeKind = "interface";
                break;
            default:
                return null;
        }

        var namespaceName = GetNamespace(typeDecl);
        var fullName = string.IsNullOrEmpty(namespaceName) || namespaceName == "Global"
            ? typeName
            : $"{namespaceName}.{typeName}";

        var classInfo = new ClassInfo(
            Name: typeName,
            FullName: fullName,
            Namespace: namespaceName,
            SourceFile: Path.GetFileName(sourceFileName),
            TypeKind: typeKind,
            ClassDocumentation: DocumentationParser.ParseDocumentation(typeDecl)
        );

        // Analyze different members based on type
        if (typeDecl is ClassDeclarationSyntax or StructDeclarationSyntax or InterfaceDeclarationSyntax
            or RecordDeclarationSyntax)
        {
            classInfo = classInfo with
            {
                PublicMethods = GetPublicMethods(typeDecl),
                Properties = GetProperties(typeDecl),
                Fields = GetFields(typeDecl)
            };
        }
        else if (typeDecl is EnumDeclarationSyntax enumDecl)
        {
            classInfo = classInfo with { EnumValues = GetEnumValues(enumDecl) };
        }

        logger.Information($"Analyzed {typeKind}: {fullName}");
        return classInfo;
    }

    List<PropertyInfo> GetProperties(SyntaxNode typeDecl)
    {
        var properties = new List<PropertyInfo>();

        var propertyDeclarations = Enumerable.Empty<PropertyDeclarationSyntax>();

        if (typeDecl is ClassDeclarationSyntax classDecl)
        {
            propertyDeclarations = classDecl.Members.OfType<PropertyDeclarationSyntax>();
        }
        else if (typeDecl is StructDeclarationSyntax structDecl)
        {
            propertyDeclarations = structDecl.Members.OfType<PropertyDeclarationSyntax>();
        }
        else if (typeDecl is InterfaceDeclarationSyntax interfaceDecl)
        {
            propertyDeclarations = interfaceDecl.Members.OfType<PropertyDeclarationSyntax>();
        }
        else if (typeDecl is RecordDeclarationSyntax recordDecl)
        {
            propertyDeclarations = recordDecl.Members.OfType<PropertyDeclarationSyntax>();
        }

        var publicProperties = propertyDeclarations
            .Where(p => p.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword))
                        || typeDecl is InterfaceDeclarationSyntax)
            .ToList();

        foreach (var property in publicProperties)
        {
            var hasGetter = property.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) ??
                            false;
            var hasSetter = property.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)) ??
                            false;

            var propertyInfo = new PropertyInfo(
                Name: property.Identifier.ValueText,
                Type: property.Type.ToString(),
                Modifiers: string.Join(" ", property.Modifiers.Select(m => m.ValueText)),
                HasGetter: hasGetter,
                HasSetter: hasSetter,
                DefaultValue: property.Initializer?.Value.ToString(),
                Documentation: DocumentationParser.ParseDocumentation(property)
            );

            properties.Add(propertyInfo);
        }

        return properties;
    }

    List<FieldInfo> GetFields(SyntaxNode typeDecl)
    {
        var fields = new List<FieldInfo>();

        var fieldDeclarations = Enumerable.Empty<FieldDeclarationSyntax>();

        if (typeDecl is ClassDeclarationSyntax classDecl)
        {
            fieldDeclarations = classDecl.Members.OfType<FieldDeclarationSyntax>();
        }
        else if (typeDecl is StructDeclarationSyntax structDecl)
        {
            fieldDeclarations = structDecl.Members.OfType<FieldDeclarationSyntax>();
        }
        else if (typeDecl is InterfaceDeclarationSyntax interfaceDecl)
        {
            fieldDeclarations = interfaceDecl.Members.OfType<FieldDeclarationSyntax>();
        }
        else if (typeDecl is RecordDeclarationSyntax recordDecl)
        {
            fieldDeclarations = recordDecl.Members.OfType<FieldDeclarationSyntax>();
        }

        var publicFields = fieldDeclarations
            .Where(f => f.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
            .ToList();

        foreach (var field in publicFields)
        {
            foreach (var variable in field.Declaration.Variables)
            {
                var fieldInfo = new FieldInfo(
                    Name: variable.Identifier.ValueText,
                    Type: field.Declaration.Type.ToString(),
                    Modifiers: string.Join(" ", field.Modifiers.Select(m => m.ValueText)),
                    DefaultValue: variable.Initializer?.Value.ToString(),
                    Documentation: DocumentationParser.ParseDocumentation(field)
                );

                fields.Add(fieldInfo);
            }
        }

        return fields;
    }

    List<EnumValueInfo> GetEnumValues(EnumDeclarationSyntax enumDecl)
    {
        var enumValues = new List<EnumValueInfo>();

        foreach (var member in enumDecl.Members)
        {
            var enumValue = new EnumValueInfo(
                Name: member.Identifier.ValueText,
                Value: member.EqualsValue?.Value.ToString(),
                Documentation: DocumentationParser.ParseDocumentation(member)
            );

            enumValues.Add(enumValue);
        }

        return enumValues;
    }


    static string GetNamespace(SyntaxNode typeDecl)
    {
        var namespaceDecl = typeDecl.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDecl != null)
        {
            return namespaceDecl.Name.ToString();
        }

        var fileScopedNamespace = typeDecl.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScopedNamespace != null)
        {
            return fileScopedNamespace.Name.ToString();
        }

        return "Global";
    }

    List<MethodInfo> GetPublicMethods(SyntaxNode typeDecl)
    {
        var methods = new List<MethodInfo>();

        var methodDeclarations = Enumerable.Empty<MethodDeclarationSyntax>();

        if (typeDecl is ClassDeclarationSyntax classDecl)
        {
            methodDeclarations = classDecl.Members.OfType<MethodDeclarationSyntax>();
        }
        else if (typeDecl is StructDeclarationSyntax structDecl)
        {
            methodDeclarations = structDecl.Members.OfType<MethodDeclarationSyntax>();
        }
        else if (typeDecl is InterfaceDeclarationSyntax interfaceDecl)
        {
            methodDeclarations = interfaceDecl.Members.OfType<MethodDeclarationSyntax>();
        }
        else if (typeDecl is RecordDeclarationSyntax recordDecl)
        {
            methodDeclarations = recordDecl.Members.OfType<MethodDeclarationSyntax>();
        }

        var publicMethods = methodDeclarations
            .Where(m => m.Modifiers.Any(mod => mod.IsKind(
                            SyntaxKind.PublicKeyword))
                        || typeDecl is InterfaceDeclarationSyntax)
            .ToList();

        foreach (var method in publicMethods)
        {
            var methodInfo = new MethodInfo(
                Name: method.Identifier.ValueText,
                ReturnType: method.ReturnType.ToString(),
                Modifiers: string.Join(" ", method.Modifiers.Select(m => m.ValueText)),
                Parameters: GetMethodParameters(method),
                Documentation: DocumentationParser.ParseDocumentation(method)
            );

            methods.Add(methodInfo);
        }

        return methods;
    }

    static List<ParameterInfo> GetMethodParameters(MethodDeclarationSyntax method)
    {
        return [.. method.ParameterList.Parameters
            .Select(param => new ParameterInfo(
                Name: param.Identifier.ValueText,
                Type: param.Type?.ToString() ?? "var",
                DefaultValue: param.Default?.Value.ToString()
            ))];
    }
}
