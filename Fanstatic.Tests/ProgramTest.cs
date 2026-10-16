using Serilog.Events;
using Xunit;

namespace Fanstatic.Test;

/// <summary>Unit tests for the Program class.</summary>
public class ProgramTests : TestSetup
{
    /// <summary>Confirms that verbose mode enables debug logging while information logging stays on either way.</summary>
    [Theory]
    [InlineData(false, LogEventLevel.Information)]
    [InlineData(true, LogEventLevel.Debug)]
    public void CreateLoggerSetsLogLevel(bool verbose, LogEventLevel expected)
    {
        // Act
        var logger = Program.CreateLogger(verbose);

        // Assert
        Assert.Equal(expected == LogEventLevel.Debug, logger.IsEnabled(LogEventLevel.Debug));
        Assert.True(logger.IsEnabled(LogEventLevel.Information));
    }
}
