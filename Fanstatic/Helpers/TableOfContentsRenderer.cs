using System.Net;
using System.Text;
using Fanstatic.Models;

namespace Fanstatic.Helpers;

/// <summary>
/// Builds a nested table of contents from a page's collected headings.
/// </summary>
public static class TableOfContentsRenderer
{
    /// <summary>
    /// Renders the headings in the configured level range as a nested navigation element.
    /// </summary>
    public static string Render(IReadOnlyList<Heading> headings, int startLevel, int endLevel)
    {
        ArgumentNullException.ThrowIfNull(headings);

        var visible = headings
            .Where(heading => heading.Level >= startLevel && heading.Level <= endLevel && heading.Id.Length != 0)
            .ToList();
        var builder = new StringBuilder("<nav id=\"TableOfContents\">");
        var index = 0;
        RenderLevel(builder, visible, ref index, startLevel);
        return builder.Append("</nav>").ToString();
    }

    static void RenderLevel(StringBuilder builder, IReadOnlyList<Heading> headings, ref int index, int level)
    {
        _ = builder.Append("<ul>");
        while (index < headings.Count)
        {
            var heading = headings[index];
            if (heading.Level < level)
            {
                break;
            }

            if (heading.Level > level)
            {
                RenderLevel(builder, headings, ref index, heading.Level);
                continue;
            }

            index++;
            _ = builder.Append("<li><a href=\"#")
                .Append(heading.Id)
                .Append("\">")
                .Append(WebUtility.HtmlEncode(heading.Text))
                .Append("</a>");
            if (index < headings.Count && headings[index].Level > level)
            {
                RenderLevel(builder, headings, ref index, headings[index].Level);
            }

            _ = builder.Append("</li>");
        }

        _ = builder.Append("</ul>");
    }
}
