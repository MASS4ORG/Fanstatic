using Build;
using Xunit;

namespace Fanstatic.Test.Helpers;

/// <summary>Verifies benchmark comparisons use samples collected under the same runner conditions.</summary>
public class BenchmarkSamplingTests
{
    /// <summary>A slower runner affects both versions equally and must not create a false regression.</summary>
    [Fact]
    public void SameRunnerSamplesShouldCancelRunnerSpeedAndLoadChanges()
    {
        var sequence = 0;
        var order = new List<string>();
        BenchmarkRun Measure(string version)
        {
            order.Add(version);
            var duration = ++sequence * 100;
            return new BenchmarkRun(duration, duration * 2, duration * 3);
        }

        var (current, reference) = BenchmarkSampling.Measure(
            () => Measure("current"), () => Measure("reference"), 5);

        Assert.NotNull(reference);
        Assert.Equal(5, current.Runs.Count);
        Assert.Equal(5, reference.Runs.Count);
        Assert.Equal(600, current.ParseMedianMilliseconds);
        Assert.Equal(500, reference.ParseMedianMilliseconds);
        Assert.Equal(1200, current.CreateMedianMilliseconds);
        Assert.Equal(1000, reference.CreateMedianMilliseconds);
        Assert.Equal(1800, current.WallMedianMilliseconds);
        Assert.Equal(1500, reference.WallMedianMilliseconds);
        Assert.Equal(["reference", "current", "current", "reference", "reference", "current",
            "current", "reference", "reference", "current"], order);
    }

    /// <summary>Pairing preserves a real slowdown even when the runner is slower than the recorded history.</summary>
    [Fact]
    public void SameRunnerSamplesShouldRetainRealRegressions()
    {
        var (current, reference) = BenchmarkSampling.Measure(
            () => new BenchmarkRun(140, 280, 420), () => new BenchmarkRun(100, 200, 300), 5);

        Assert.NotNull(reference);
        Assert.True(current.ParseMedianMilliseconds > reference.ParseMedianMilliseconds * 1.25);
        Assert.True(current.CreateMedianMilliseconds > reference.CreateMedianMilliseconds * 1.25);
    }

    /// <summary>Local history comparisons collect only the current version's samples.</summary>
    [Fact]
    public void WithoutReferenceShouldOnlyMeasureCurrentVersion()
    {
        var runs = 0;
        var (current, reference) = BenchmarkSampling.Measure(
            () => new BenchmarkRun(++runs, runs * 2, runs * 3), null, 5);

        Assert.Null(reference);
        Assert.Equal(5, runs);
        Assert.Equal(3, current.ParseMedianMilliseconds);
        Assert.Equal(6, current.CreateMedianMilliseconds);
        Assert.Equal(9, current.WallMedianMilliseconds);
    }
}
