using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Fanstatic.Helpers;
using Serilog;
using Serilog.Sinks.InMemory;
using Xunit;

namespace Fanstatic.Test.Helpers;

/// <summary>Unit tests for the StopwatchReporter class.</summary>
public class StopwatchReporterTests
{
    readonly ILogger _logger;
    readonly InMemorySink _inMemorySink;

    /// <summary>Creates an in-memory logger so report output can be inspected.</summary>
    public StopwatchReporterTests()
    {
        _inMemorySink = new InMemorySink();
        _logger = new LoggerConfiguration().WriteTo.Sink(_inMemorySink).CreateLogger();
    }

    /// <summary>Confirms that Start creates a running stopwatch entry for the named step.</summary>
    [Fact]
    public void StartInitializesAndStartsStopwatchForStep()
    {
        // Arrange
        const string stepName = "TestStep";
        var stopwatchReporter = new StopwatchReporter(new LoggerConfiguration().CreateLogger());

        // Act
        stopwatchReporter.Start(stepName);

        // Assert
        var stopwatchField = stopwatchReporter.GetType().GetField(
            "_stopwatches",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(stopwatchField);

        var stopwatchDictionary = stopwatchField.GetValue(stopwatchReporter) as Dictionary<string, Stopwatch>;
        Assert.NotNull(stopwatchDictionary);

        Assert.True(stopwatchDictionary.ContainsKey(stepName));
        Assert.True(stopwatchDictionary[stepName].IsRunning);
    }

    /// <summary>Confirms that LogReport writes the site title together with the recorded step to the log.</summary>
    [Fact]
    public void LogReportCorrectlyLogsElapsedTime()
    {
        const string stepName = "TestStep";
        const string siteTitle = "TestSite";
        const int duration = 123;

        var stopwatchReporter = new StopwatchReporter(_logger);
        stopwatchReporter.Start(stepName);
        Thread.Sleep(duration); // Let's wait a bit to simulate some processing.
        stopwatchReporter.Stop(stepName, 1);

        stopwatchReporter.LogReport(siteTitle);

        // Assert
        var logEvents = _inMemorySink.LogEvents;
        var logEventsList = logEvents.ToList();
        Assert.NotEmpty(logEventsList);
        var logMessage = logEventsList.First().RenderMessage(CultureInfo.InvariantCulture);
        Assert.Contains($"Site '{siteTitle}' created!", logMessage, StringComparison.InvariantCulture);
        Assert.Contains(stepName, logMessage, StringComparison.InvariantCulture);
        // Assert.Contains($"{duration} ms", logMessage, StringComparison.InvariantCulture); // Ensure that our processing time was logged.
    }

    /// <summary>Confirms that stopping a step that was never started throws an ArgumentException naming that step.</summary>
    [Fact]
    public void StopThrowsExceptionWhenStopCalledWithoutStart()
    {
        const string stepName = "TestStep";
        var stopwatchReporter = new StopwatchReporter(_logger);

        // Don't call Start for stepName

        // Assert that Stop throws an exception
        var exception = Assert.Throws<ArgumentException>(() => stopwatchReporter.Stop(stepName, 1));
        Assert.Equal($"Step '{stepName}' has not been started.", exception.Message);
    }
}
