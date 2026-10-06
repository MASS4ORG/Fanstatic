using System.Globalization;
using System.Text;

namespace Fanstatic.TemplateEngine;

/// <summary>
/// Formats aggregated template render metrics for build output.
/// </summary>
public static class TemplateMetricsReport
{
    /// <summary>
    /// Creates a table sorted by total render time, descending.
    /// </summary>
    /// <param name="metrics">The metrics to format.</param>
    /// <returns>A text table suitable for logging.</returns>
    public static string Format(IEnumerable<TemplateMetric> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var rows = metrics.OrderByDescending(metric => metric.TotalTime)
            .ThenBy(metric => metric.TemplatePath, StringComparer.Ordinal)
            .ToArray();
        var pathWidth = Math.Max("Template".Length,
            rows.Select(metric => metric.TemplatePath.Length).DefaultIfEmpty(0).Max());
        var report = new StringBuilder("Template metrics\n");
        report.Append(CultureInfo.InvariantCulture,
            $"{"Template".PadRight(pathWidth)}  {"Calls",8}  {"Total",12}  {"Max",12}  {"Cache hits",10}");

        foreach (var metric in rows)
        {
            report.Append('\n');
            report.Append(CultureInfo.InvariantCulture,
                $"{metric.TemplatePath.PadRight(pathWidth)}  {metric.CallCount,8}  " +
                $"{FormatDuration(metric.TotalTime),12}  " +
                $"{FormatDuration(metric.MaximumTime),12}  {metric.CacheHits,10}");
        }

        return report.ToString();
    }

    static string FormatDuration(TimeSpan duration) =>
        $"{duration.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture)} ms";
}
