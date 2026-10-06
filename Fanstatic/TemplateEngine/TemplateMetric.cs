namespace Fanstatic.TemplateEngine;

/// <summary>
/// Aggregated render metrics for a template.
/// </summary>
/// <param name="TemplatePath">The normalized template path, or the inline-template label.</param>
/// <param name="CallCount">The number of render calls.</param>
/// <param name="TotalTime">The total time spent rendering.</param>
/// <param name="MaximumTime">The longest individual render call.</param>
/// <param name="CacheHits">The number of calls that reused a compiled template.</param>
public sealed record TemplateMetric(
    string TemplatePath,
    long CallCount,
    TimeSpan TotalTime,
    TimeSpan MaximumTime,
    long CacheHits);

sealed class TemplateMetricCounter
{
    long _callCount;
    long _totalTicks;
    long _maximumTicks;
    long _cacheHits;

    public void Add(long elapsedTicks, bool cacheHit)
    {
        _ = Interlocked.Increment(ref _callCount);
        _ = Interlocked.Add(ref _totalTicks, elapsedTicks);
        if (cacheHit)
        {
            _ = Interlocked.Increment(ref _cacheHits);
        }

        var previousMaximum = Volatile.Read(ref _maximumTicks);
        while (elapsedTicks > previousMaximum)
        {
            var observed = Interlocked.CompareExchange(ref _maximumTicks, elapsedTicks, previousMaximum);
            if (observed == previousMaximum)
            {
                break;
            }

            previousMaximum = observed;
        }

    }

    public TemplateMetric Snapshot(string templatePath) =>
        new(templatePath, Volatile.Read(ref _callCount), TimeSpan.FromTicks(Volatile.Read(ref _totalTicks)),
            TimeSpan.FromTicks(Volatile.Read(ref _maximumTicks)), Volatile.Read(ref _cacheHits));
}
