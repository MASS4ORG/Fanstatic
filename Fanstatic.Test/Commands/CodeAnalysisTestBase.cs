using Serilog;
using Fanstatic.Commands.API;
using Xunit;

namespace Fanstatic.Test.Commands;

public abstract class CodeAnalysisTestBase
{
    protected readonly ILogger Logger;
    protected readonly CodeAnalyzer Analyzer;
    protected readonly ApiGeneratorOptions Options;
    protected const string TestProjectPath = "../../../.TestSites/10-cs-project";

    protected CodeAnalysisTestBase()
    {
        Logger = new LoggerConfiguration()
            .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
        Analyzer = new CodeAnalyzer(Logger);
        Options = new ApiGeneratorOptions
        {
            SourceProjects = [TestProjectPath],
            Output = "output"
        };
    }

    protected static void AssertContains(string expected, string actual)
    {
        Assert.Contains(expected, actual, StringComparison.InvariantCulture);
    }

    protected static void AssertDoesNotContain(string expected, string actual)
    {
        Assert.DoesNotContain(expected, actual, StringComparison.InvariantCulture);
    }

    protected static void AssertEqual(string expected, string actual)
    {
        Assert.Equal(expected, actual, StringComparer.InvariantCulture);
    }

    protected static void AssertStartsWith(string expected, string actual)
    {
        Assert.StartsWith(expected, actual, StringComparison.InvariantCulture);
    }

    protected static void AssertEndsWith(string expected, string actual)
    {
        Assert.EndsWith(expected, actual, StringComparison.InvariantCulture);
    }
}
