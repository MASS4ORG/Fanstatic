using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Fanstatic.Commands.API.APIModels;

namespace Fanstatic.Commands.API;

/// <summary>
/// Provides functionality to parse documentation comments and extract relevant information
/// from syntax nodes in a C# code syntax tree.
/// </summary>
public static partial class DocumentationParser
{
    /// <summary>
    /// Parses the documentation associated with a given syntax node.
    /// </summary>
    /// <param name="node">The syntax node from which to extract documentation.</param>
    /// <returns>
    /// An instance of <see cref="DocumentationInfo"/> containing the extracted documentation details,
    /// including raw content and a summary if applicable.
    /// </returns>
    public static DocumentationInfo ParseDocumentation(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var documentationComment = node.GetLeadingTrivia()
            .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                 t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

        if (!documentationComment.IsKind(SyntaxKind.None))
        {
            var rawContent = documentationComment.ToString().Trim();
            return ParseXmlDocumentation(rawContent);
        }
        else
        {
            var regularComments = node.GetLeadingTrivia()
                .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                            t.IsKind(SyntaxKind.MultiLineCommentTrivia))
                .Select(t => t.ToString().Trim())
                .ToList();

            if (regularComments.Any())
            {
                var rawContent = string.Join(Environment.NewLine, regularComments);
                return new DocumentationInfo(
                    Summary: rawContent,
                    RawContent: rawContent
                );
            }
        }

        return new DocumentationInfo();
    }

    static DocumentationInfo ParseXmlDocumentation(string rawContent)
    {
        try
        {
            // Clean up the XML documentation
            var xmlContent = rawContent
                .Replace("///", "")
                .Replace("/**", "")
                .Replace("*/", "")
                .Trim();

            if (string.IsNullOrEmpty(xmlContent))
            {
                return new DocumentationInfo(RawContent: rawContent);
            }

            // Wrap in a root element to make it valid XML
            xmlContent = $"<doc>{xmlContent}</doc>";

            var doc = new XmlDocument
            {
                XmlResolver = null
            };
            doc.LoadXml(xmlContent);

            var summary = "";
            var returns = "";
            var example = "";
            var remarks = "";
            var parameters = new List<ParamDoc>();

            // Parse summary
            var summaryNode = doc.SelectSingleNode("//summary");
            if (summaryNode != null)
            {
                summary = ProcessXmlDocumentationText(summaryNode).Trim();
            }

            // Parse parameters
            var paramNodes = doc.SelectNodes("//param");
            if (paramNodes != null)
            {
                foreach (XmlNode paramNode in paramNodes)
                {
                    var nameAttr = paramNode.Attributes?["name"];
                    if (nameAttr != null)
                    {
                        parameters.Add(new ParamDoc(
                            Name: nameAttr.Value,
                            Description: ProcessXmlDocumentationText(paramNode).Trim()
                        ));
                    }
                }
            }

            // Parse returns
            var returnsNode = doc.SelectSingleNode("//returns");
            if (returnsNode != null)
            {
                returns = ProcessXmlDocumentationText(returnsNode).Trim();
            }

            // Parse example
            var exampleNode = doc.SelectSingleNode("//example");
            if (exampleNode != null)
            {
                example = ProcessXmlDocumentationText(exampleNode).Trim();
            }

            // Parse remarks
            var remarksNode = doc.SelectSingleNode("//remarks");
            if (remarksNode != null)
            {
                remarks = ProcessXmlDocumentationText(remarksNode).Trim();
            }

            return new DocumentationInfo(
                Summary: summary,
                Parameters: parameters.Any() ? parameters : null,
                Returns: returns,
                Example: example,
                Remarks: remarks,
                RawContent: rawContent
            );
        }
        catch (Exception)
        {
            // If XML parsing fails, use the raw content as summary
            return new DocumentationInfo(
                Summary: rawContent,
                RawContent: rawContent
            );
        }
    }

    static string ProcessXmlDocumentationText(XmlNode node)
    {
        var result = node.InnerXml;

        result = SeeCrefSelfClosingRegex().Replace(result, match =>
        {
            var typeName = match.Groups[1].Value.Split('.').Last();
            return $"`{typeName}`";
        });

        result = SeeCrefWithTextRegex().Replace(result, match =>
        {
            var text = match.Groups[2].Value;
            return string.IsNullOrEmpty(text) ? $"`{match.Groups[1].Value.Split('.').Last()}`" : text;
        });

        result = InlineCodeRegex().Replace(result, "`$1`");
        result = CodeBlockRegex().Replace(result, "`$1`");
        result = ParamRefRegex().Replace(result, "`$1`");
        result = XmlTagRegex().Replace(result, "");
        result = WhitespaceRegex().Replace(result, " ");

        return result.Trim();
    }

    [GeneratedRegex(@"<see\s+cref\s*=\s*""([^""]*)""\s*/>")]
    private static partial Regex SeeCrefSelfClosingRegex();

    [GeneratedRegex(@"<see\s+cref\s*=\s*""([^""]*)""\s*>([^<]*)</see>")]
    private static partial Regex SeeCrefWithTextRegex();

    [GeneratedRegex(@"<c>([^<]*)</c>")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"<code>([^<]*)</code>")]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"<paramref\s+name\s*=\s*""([^""]*)""\s*/>")]
    private static partial Regex ParamRefRegex();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex XmlTagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
