using Fanstatic.TemplateEngine;
using Xunit;

namespace Fanstatic.Test.TemplateEngine;

/// <summary>Unit tests for the TemplateMetricsReport class.</summary>
public class TemplateMetricsReportTests
{
    /// <summary>Confirms that the report sorts by total time and includes the metric columns with formatted values.</summary>
    [Fact]
    public void FormatSortsByTotalTimeAndIncludesMetricColumns()
    {
        TemplateMetric[] metrics =
        [
            new("slow.html", 3, TimeSpan.FromMilliseconds(24.5), TimeSpan.FromMilliseconds(12), 2),
            new("fast.html", 7, TimeSpan.FromMilliseconds(3), TimeSpan.FromMilliseconds(1), 6),
        ];

        var report = TemplateMetricsReport.Format(metrics);

        Assert.StartsWith("Template metrics\n", report, StringComparison.Ordinal);
        Assert.Contains("Template", report, StringComparison.Ordinal);
        Assert.Contains("Calls", report, StringComparison.Ordinal);
        Assert.Contains("Total", report, StringComparison.Ordinal);
        Assert.Contains("Max", report, StringComparison.Ordinal);
        Assert.Contains("Cache hits", report, StringComparison.Ordinal);
        Assert.True(report.IndexOf("slow.html", StringComparison.Ordinal) <
                    report.IndexOf("fast.html", StringComparison.Ordinal));
        Assert.Contains("24.50 ms", report, StringComparison.Ordinal);
        Assert.Contains("12.00 ms", report, StringComparison.Ordinal);
        Assert.Contains("3.00 ms", report, StringComparison.Ordinal);
        Assert.Contains("       6", report, StringComparison.Ordinal);
    }

    /// <summary>Confirms that formatting a null metric list throws an ArgumentNullException.</summary>
    [Fact]
    public void FormatThrowsForNullMetrics()
    {
        Assert.Throws<ArgumentNullException>(() => TemplateMetricsReport.Format(null!));
    }
}
