using System.Text;
using FolkerKinzel.Strings;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Fanstatic.Parsers;

/// <summary>
/// Responsible for parsing the content front matter using YAML
/// </summary>
public class YamlParser : IFrontMatterParser
{
    /// <summary>
    /// YamlDotNet parser, strictly set to allow automatically parse only known fields
    /// </summary>
    readonly IDeserializer _deserializer;

    /// <summary>
    /// ctor
    /// </summary>
    public YamlParser()
    {
        _deserializer = new StaticDeserializerBuilder(new StaticAotContext())
            .WithTypeConverter(new UriTypeConverter())
            .WithTypeConverter(new ParamsConverter())
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithCaseInsensitivePropertyMatching()
            .Build();
    }

    /// <inheritdoc/>
    public T Parse<T>(string content)
    {
        try
        {
            return _deserializer.Deserialize<T>(content);
        }
        catch (Exception ex)
        {
            var cause = ex;
            while (cause.InnerException is not null)
            {
                cause = cause.InnerException;
            }

            var location = ex is YamlException { Start: var start }
                ? $" (front matter line {start.Line}, column {start.Column})"
                : string.Empty;
            var hint = cause is InvalidCastException
                ? "; the value type does not match the field, for example a single value where a list is expected"
                : string.Empty;
            throw new FormatException($"Invalid YAML for '{typeof(T).Name}'{location}: {cause.Message}{hint}", ex);
        }
    }

    /// <inheritdoc/>
    public void SerializeAndSave<T>(T data, string fileFullPath)
    {
        var serializer = new SerializerBuilder()
            .IgnoreFields()
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitEmptyCollections
                | DefaultValuesHandling.OmitDefaults
                | DefaultValuesHandling.OmitNull)
            .Build();
        var dataString = serializer.Serialize(data);
        File.WriteAllText(fileFullPath, dataString);
    }

    /// <inheritdoc/>
    public (string, string) SplitFrontMatterAndContent(in string fileContent)
    {
        using var content = new StringReader(fileContent);
        var frontMatterBuilder = new StringBuilder();
        string? line;

        // find the start of the block
        while ((line = content.ReadLine()) != null && line != "---")
        {
        }

        // find the end of the block
        while ((line = content.ReadLine()) != null && line != "---")
        {
            _ = frontMatterBuilder.AppendLine(line);
        }

        frontMatterBuilder.TrimEnd();

        // Join the read lines to form the front matter
        var yaml = frontMatterBuilder.ToString();
        var rawContent = content.ReadToEnd();

        return (yaml, rawContent);
    }
}
