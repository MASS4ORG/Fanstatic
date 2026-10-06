using Serilog.Events;
using Xunit;

namespace Fanstatic.Test;

public class ProgramTests : TestSetup
{
    [Theory]
    [InlineData(false, LogEventLevel.Information)]
    [InlineData(true, LogEventLevel.Debug)]
    public void CreateLogger_SetsLogLevel(bool verbose, LogEventLevel expected)
    {
        // Act
        var logger = Program.CreateLogger(verbose);

        // Assert
        Assert.Equal(expected == LogEventLevel.Debug, logger.IsEnabled(LogEventLevel.Debug));
        Assert.True(logger.IsEnabled(LogEventLevel.Information));
    }
}
