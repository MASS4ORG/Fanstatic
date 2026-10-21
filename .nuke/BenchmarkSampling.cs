#nullable enable

namespace Build;

/// <summary>Collects current and reference builds under the same runner conditions.</summary>
internal static class BenchmarkSampling
{
    /// <summary>Alternates build order to distribute changes in runner load across both versions.</summary>
    internal static (BenchmarkSamples Current, BenchmarkSamples? Reference) Measure(
        Func<BenchmarkRun> current, Func<BenchmarkRun>? reference, int runs)
    {
        List<BenchmarkRun> currentRuns = [];
        List<BenchmarkRun> referenceRuns = [];
        for (var run = 0; run < runs; run++)
        {
            if (reference is not null && run % 2 == 0)
            {
                referenceRuns.Add(reference());
            }

            currentRuns.Add(current());
            if (reference is not null && run % 2 != 0)
            {
                referenceRuns.Add(reference());
            }
        }

        return (Summarize(currentRuns), reference is null ? null : Summarize(referenceRuns));
    }

    static BenchmarkSamples Summarize(IReadOnlyList<BenchmarkRun> runs) =>
        new(runs, Median(runs.Select(static run => run.ParseMilliseconds)),
            Median(runs.Select(static run => run.CreateMilliseconds)),
            Median(runs.Select(static run => run.WallMilliseconds)));

    static double Median(IEnumerable<double> samples)
    {
        var sorted = samples.Order().ToArray();
        return sorted[sorted.Length / 2];
    }
}

/// <summary>Phase durations and total elapsed time for one site build.</summary>
internal sealed record BenchmarkRun(double ParseMilliseconds, double CreateMilliseconds, double WallMilliseconds);

/// <summary>Individual builds and the median duration of each measured phase.</summary>
internal sealed record BenchmarkSamples(
    IReadOnlyList<BenchmarkRun> Runs,
    double ParseMedianMilliseconds,
    double CreateMedianMilliseconds,
    double WallMedianMilliseconds);
