using System.Text;
using Fanstatic.Commands.API.APIModels;

namespace Fanstatic.Commands.API;

/// <summary>
/// Provides a set of extension methods for constructing or appending structured content
/// to <see cref="StringBuilder"/> instances. These extensions streamline the process of adding
/// formatted documentation or metadata outputs, such as front matter, headers, or detailed class
/// documentation.
/// </summary>
public static class StringBuilderExtensions
{
    /// <summary>
    /// Appends front matter metadata to the beginning of a StringBuilder instance.
    /// This metadata includes title, type, creation date, and an optional list of parameters.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> instance to which the front matter is appended.</param>
    /// <param name="title">The title to be included in the front matter.</param>
    /// <param name="type">The type to be included in the front matter.</param>
    /// <param name="parameters">An optional list of key-value pairs representing additional metadata parameters.</param>
    /// <returns>The modified <see cref="StringBuilder"/> instance with the appended front matter.</returns>
    public static StringBuilder AppendFrontMatter(this StringBuilder sb, string title, string type,
        params (string key, string value)[] parameters)
    {
        ArgumentNullException.ThrowIfNull(sb);

        return sb.AppendLine("---")
            .AppendLine($"Title: \"{title}\"")
            .AppendLine($"Type: {type}")
            .AppendLine($"Created: \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"")
            .AppendLine("Params:")
            .AppendLines(parameters.Select(p => $"  {p.key}: \"{p.value}\""))
            .AppendLine("---")
            .AppendLine();
    }

    /// <summary>
    /// Appends a header for a class to the beginning of a StringBuilder instance.
    /// The header includes the class name, type kind, namespace link, and source file information.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> instance to which the header is appended.</param>
    /// <param name="classInfo">An instance of <see cref="ClassInfo"/> containing metadata about the class, such as its name, namespace, and type kind.</param>
    /// <param name="options">The options for generating the API documentation.</param>
    /// <returns>The modified <see cref="StringBuilder"/> instance with the appended header.</returns>
    public static StringBuilder AppendHeader(this StringBuilder sb, ClassInfo classInfo, ApiGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentNullException.ThrowIfNull(classInfo);
        ArgumentNullException.ThrowIfNull(options);

        var sourceFileText = GenerateSourceFileText(classInfo.SourceFile, options.ExternalLink);

        return sb
            .AppendLine($"- **Namespace:** [{classInfo.Namespace}]({NamespaceLink(classInfo.Namespace, options)})")
            .AppendLine($"- **Source File:** {sourceFileText}");
    }

    /// <summary>
    /// Generates the source file text with optional external links.
    /// </summary>
    /// <param name="sourceFile">The source file(s) - can be comma-separated for partial classes</param>
    /// <param name="externalLink">Optional external repository link</param>
    /// <returns>Formatted source file text with links if external link is provided</returns>
    static string GenerateSourceFileText(string sourceFile, string? externalLink)
    {
        if (string.IsNullOrEmpty(externalLink))
        {
            return sourceFile;
        }

        var files = sourceFile.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var linkedFiles = files.Select(file =>
        {
            var cleanLink = externalLink.TrimEnd('/');
            // Ensure the link starts with http:// or https://
            if (!cleanLink.StartsWith("http://") && !cleanLink.StartsWith("https://"))
            {
                cleanLink = "https://" + cleanLink;
            }

            return $"[{file}]({cleanLink}/blob/main/{file})";
        });

        return string.Join(", ", linkedFiles);
    }

    /// <summary>
    /// Appends class-level documentation details to a StringBuilder instance.
    /// This includes the summary, remarks, and an optional example section if provided.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> instance to which the documentation content is appended.</param>
    /// <param name="classInfo">The <see cref="ClassInfo"/> object containing the class documentation details to be appended.</param>
    /// <returns>The modified <see cref="StringBuilder"/> instance with the appended class documentation content.</returns>
    public static StringBuilder AppendClassDocumentation(this StringBuilder sb, ClassInfo classInfo)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentNullException.ThrowIfNull(classInfo);

        return string.IsNullOrEmpty(classInfo.ClassDocumentation.Summary)
            ? sb
            : sb
                .AppendLine()
                .AppendLine(classInfo.ClassDocumentation.Summary)
                .AppendLine()
                .AppendOptionalSection("### Remarks", classInfo.ClassDocumentation.Remarks)
                .AppendOptionalCodeSection("### Example", classInfo.ClassDocumentation.Example);
    }

    /// <summary>
    /// Appends a list of enum values with their names, optional values, and summaries
    /// to the provided StringBuilder instance in a formatted Markdown representation.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> instance to which the enum values are appended.</param>
    /// <param name="classInfo">The <see cref="ClassInfo"/> instance containing information about the enum values.</param>
    /// <returns>The modified <see cref="StringBuilder"/> instance with the appended enum values, or the unmodified instance if no enum values are present.</returns>
    public static StringBuilder AppendEnumValues(this StringBuilder sb, ClassInfo classInfo)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentNullException.ThrowIfNull(classInfo);

        return !classInfo.EnumValues.Any()
            ? sb
            : sb.AppendLine("## Enum Values")
                .AppendLine()
                .AppendLines(classInfo.EnumValues.Select(ev =>
                    $"- **{ev.Name}**" +
                    (!string.IsNullOrEmpty(ev.Value) ? $" = `{ev.Value}`" : "") +
                    (!string.IsNullOrEmpty(ev.Documentation.Summary) ? $": {ev.Documentation.Summary}" : "")))
                .AppendLine();
    }

    /// <summary>
    /// Appends field information from the provided <see cref="ClassInfo"/>
    /// to the given <see cref="StringBuilder"/> instance in a formatted style.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> instance where the field information will be appended.</param>
    /// <param name="classInfo">The <see cref="ClassInfo"/> object containing the field data to be appended.</param>
    /// <returns>The modified <see cref="StringBuilder"/> instance with the appended field information.</returns>
    public static StringBuilder AppendFields(this StringBuilder sb, ClassInfo classInfo)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentNullException.ThrowIfNull(classInfo);

        return !classInfo.Fields.Any()
            ? sb
            : sb.AppendLine("## Fields")
                .AppendLine()
                .AppendLines(classInfo.Fields.Select(field =>
                    $"### **{field.Name}** (*{field.Type}*)" +
                    (!string.IsNullOrEmpty(field.DefaultValue) ? $" = `{field.DefaultValue}`" : "") +
                    (!string.IsNullOrEmpty(field.Documentation.Summary) ? $": {field.Documentation.Summary}" : "")))
                .AppendLine();
    }

    /// <summary>
    /// Appends a formatted list of properties to the provided <see cref="StringBuilder"/> instance based on the given <see cref="ClassInfo"/> object.
    /// If the <see cref="ClassInfo"/> object contains no properties, no content is appended.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> instance to which the property information will be appended.</param>
    /// <param name="classInfo">The <see cref="ClassInfo"/> object containing property information to format and append.</param>
    /// <returns>The modified <see cref="StringBuilder"/> instance with the formatted property information appended.</returns>
    public static StringBuilder AppendProperties(this StringBuilder sb, ClassInfo classInfo)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentNullException.ThrowIfNull(classInfo);

        return !classInfo.Properties.Any()
            ? sb
            : sb.AppendLine("## Properties")
                .AppendLine()
                .AppendLines(classInfo.Properties.Select(prop =>
                {
                    var accessors = new List<string>();
                    if (prop.HasGetter) accessors.Add("get");
                    if (prop.HasSetter) accessors.Add("set");
                    var accessorInfo = accessors.Any() ? $" {{ {string.Join("; ", accessors)} }}" : "";

                    return $"### **{prop.Name}**\n\n(*{prop.Type}*){accessorInfo}" +
                           (!string.IsNullOrEmpty(prop.DefaultValue) ? $" = `{prop.DefaultValue}`" : "") +
                           (!string.IsNullOrEmpty(prop.Documentation.Summary) ? $": {prop.Documentation.Summary}" : "")
                           + "\n";
                }))
                .AppendLine();
    }

    /// <summary>
    /// Appends documentation for public methods of a class to the given StringBuilder instance.
    /// Each public method's documentation is formatted and included as a separate line.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> instance to append the method documentation to.</param>
    /// <param name="classInfo">An instance of <see cref="ClassInfo"/> containing information about the class and its public methods.</param>
    /// <returns>The modified <see cref="StringBuilder"/> instance with appended method documentation.</returns>
    public static StringBuilder AppendMethods(this StringBuilder sb, ClassInfo classInfo)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentNullException.ThrowIfNull(classInfo);

        return !classInfo.PublicMethods.Any()
            ? sb
            : sb.AppendLine("## Public Methods")
                .AppendLine()
                .AppendLines(classInfo.PublicMethods.Select(GenerateMethodDocumentation));
    }

    /// <summary>
    /// Appends a formatted list of type links to the StringBuilder instance.
    /// Each type is represented as a list item containing an icon, modifiers, and a hyperlink to the type's documentation.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> instance to which the type list is appended.</param>
    /// <param name="classes">A list of <see cref="ClassInfo"/> objects representing the types to be included in the list.</param>
    /// <returns>The modified <see cref="StringBuilder"/> instance with the appended type list.</returns>
    public static StringBuilder AppendTypeList(this StringBuilder sb, List<ClassInfo> classes)
    {
        var sortedClasses = classes.OrderBy(c => c.Name);
        return sb.AppendLines(sortedClasses.Select(classInfo =>
        {
            var icon = GetTypeIcon(classInfo);
            var modifierIcons =
                GetModifierIcons(classInfo.Properties.FirstOrDefault()?.Modifiers ?? "");
            var fileName = GetTypeFileName(classInfo);

            return $"- {icon} [{classInfo.Name}](./{fileName})";
        }));
    }

    static string GetTypeFileName(ClassInfo classInfo) =>
        $"{classInfo.Name.ToLower()}";

    /// <summary>
    /// Appends a list of namespaces and their associated classes to a StringBuilder instance.
    /// The namespaces are ordered alphabetically, and each namespace includes the names of its classes.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> instance to which the namespaces and class lists are appended.</param>
    /// <param name="namespaceClasses">A dictionary where the keys represent namespace names and the values are lists of <see cref="ClassInfo"/> instances corresponding to the classes within each namespace.</param>
    /// <param name="options">The options for generating the API documentation.</param>
    /// <returns>The modified <see cref="StringBuilder"/> instance with the appended namespace and class information.</returns>
    public static StringBuilder AppendNamespaceList(this StringBuilder sb,
        Dictionary<string, List<ClassInfo>> namespaceClasses, ApiGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentNullException.ThrowIfNull(namespaceClasses);
        ArgumentNullException.ThrowIfNull(options);

        return sb.AppendLines(namespaceClasses.OrderBy(x => x.Key).Select(kvp =>
        {
            var namespaceName = kvp.Key;
            var classes = kvp.Value;
            var typeCount = classes.Count;

            return
                $"- [{namespaceName}]({NamespaceLink(namespaceName, options)}) ({typeCount} type{(typeCount != 1 ? "s" : "")})";
        }));
    }

    static string NamespaceLink(string namespaceName, ApiGeneratorOptions options) =>
        $"/{options.Output}/{namespaceName.ToLower()}";

    static StringBuilder AppendOptionalSection(this StringBuilder sb, string header, string? content) =>
        string.IsNullOrEmpty(content)
            ? sb
            : sb.AppendLine(header)
                .AppendLine(content)
                .AppendLine();

    static StringBuilder AppendOptionalCodeSection(this StringBuilder sb, string header, string? content) =>
        string.IsNullOrEmpty(content)
            ? sb
            : sb.AppendLine(header)
                .AppendLine("```csharp")
                .AppendLine(content)
                .AppendLine("```")
                .AppendLine();

    static StringBuilder AppendLines(this StringBuilder sb, IEnumerable<string> lines) =>
        lines.Aggregate(sb, (current, line) => current.AppendLine(line));

    static string GenerateMethodDocumentation(MethodInfo method)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"### {method.Name}")
            .AppendLine()
            .AppendLine("```csharp")
            .AppendLine(
                $"{method.Modifiers} {method.ReturnType} {method.Name}({GenerateParameterSignature(method.Parameters)})")
            .AppendLine("```")
            .AppendLine();

        if (!string.IsNullOrEmpty(method.Documentation.Summary))
        {
            sb
                // .AppendLine("**Description:**")
                .AppendLine(method.Documentation.Summary)
                .AppendLine();
        }

        if (method.Parameters.Any())
        {
            sb.AppendLine("**Parameters:**")
                .AppendLine()
                .AppendLines(method.Parameters.Select(param =>
                {
                    var paramDoc = method.Documentation.Parameters.FirstOrDefault(p => p.Name == param.Name);
                    var description = paramDoc?.Description ?? "";
                    var defaultValue = !string.IsNullOrEmpty(param.DefaultValue)
                        ? $" (Default: `{param.DefaultValue}`)"
                        : "";

                    return
                        $"- `{param.Name}` (*{param.Type}*){(!string.IsNullOrEmpty(description) ? $": {description}" : "")}{defaultValue}";
                }))
                .AppendLine();
        }

        if (method.ReturnType != "void")
        {
            sb.AppendLine($"**Returns:** `{method.ReturnType}`")
                .AppendLine();
            if (!string.IsNullOrEmpty(method.Documentation.Returns))
            {
                sb.AppendLine($"- {method.Documentation.Returns}");
            }
        }

        if (!string.IsNullOrEmpty(method.Documentation.Example)
            || !string.IsNullOrEmpty(method.Documentation.Remarks))
        {
            sb.AppendOptionalCodeSection("**Example:**", method.Documentation.Example)
                .AppendOptionalSection("**Remarks:**", method.Documentation.Remarks)
                .AppendLine();
        }

        return sb.ToString();
    }

    static string GenerateParameterSignature(List<ParameterInfo> parameters) =>
        parameters.Any()
            ? string.Join(", ", parameters.Select(p =>
                $"{p.Type} {p.Name}" + (!string.IsNullOrEmpty(p.DefaultValue) ? $" = {p.DefaultValue}" : "")))
            : "";


    static string GetTypeIcon(ClassInfo classInfo) =>
        classInfo.TypeKind.ToLower() switch
        {
            "class" => "🄲",
            "enum" => "🄴",
            "interface" => "🄸",
            "record" => "🅁",
            "struct" => "🅂",
            _ => "🅃"
        };

    static string GetModifierIcons(string modifiers) =>
        modifiers.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(modifier => modifier.ToLower() switch
            {
                "abstract" => "🅐",
                "async" => "ⓐ",
                "const" => "🅒",
                "internal" => "🅘",
                "override" => "🅞",
                "partial" => "🅣",
                "private" => "Ⓟ",
                "protected" => "🅡",
                "public" => "🅟",
                "readonly" => "🅡",
                "sealed" => "Ⓢ",
                "static" => "🅢",
                "virtual" => "🅥",
                "volatile" => "ⓥ",
                _ => string.Empty
            })
            .Where(icon => !string.IsNullOrEmpty(icon))
            .Aggregate("", (acc, icon) => acc + icon);
}
