using Fanstatic.TemplateEngine;
using Xunit;

namespace Fanstatic.Test.TemplateEngine;

public class TemplateMetricsReportTests
{
    [Fact]
    public void Format_SortsByTotalTimeAndIncludesMetricColumns()
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

    [Fact]
    public void Format_ThrowsForNullMetrics()
    {
        Assert.Throws<ArgumentNullException>(() => TemplateMetricsReport.Format(null!));
    }
}
