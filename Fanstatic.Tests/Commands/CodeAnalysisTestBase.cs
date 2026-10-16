using Fanstatic.Commands.API;
using Serilog;
using Xunit;

namespace Fanstatic.Test.Commands;

/// <summary>Provides a shared test base with a configured CodeAnalyzer, options, and assertion helpers.</summary>
public abstract class CodeAnalysisTestBase
{
    /// <summary>Logger shared by derived test fixtures.</summary>
    protected readonly ILogger Logger;
    /// <summary>Code analyzer under test, configured with <see cref="Logger"/>.</summary>
    protected readonly CodeAnalyzer Analyzer;
    /// <summary>Default API generator options pointing at the test project.</summary>
    protected readonly ApiGeneratorOptions Options;
    /// <summary>Relative path to the sample C# project used by tests.</summary>
    protected const string TestProjectPath = "../../../.TestSites/10-cs-project";

    /// <summary>Initializes the logger, analyzer, and default options.</summary>
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

    /// <summary>Substring assertion with invariant culture comparison.</summary>
    protected static void AssertContains(string expected, string actual)
    {
        Assert.Contains(expected, actual, StringComparison.InvariantCulture);
    }

    /// <summary>Asserts that a substring is absent, using invariant culture comparison.</summary>
    protected static void AssertDoesNotContain(string expected, string actual)
    {
        Assert.DoesNotContain(expected, actual, StringComparison.InvariantCulture);
    }

    /// <summary>String equality assertion with invariant culture comparison.</summary>
    protected static void AssertEqual(string expected, string actual)
    {
        Assert.Equal(expected, actual, StringComparer.InvariantCulture);
    }

    /// <summary>Asserts that a string starts with the expected prefix.</summary>
    protected static void AssertStartsWith(string expected, string actual)
    {
        Assert.StartsWith(expected, actual, StringComparison.InvariantCulture);
    }

    /// <summary>Asserts that a string ends with the expected suffix.</summary>
    protected static void AssertEndsWith(string expected, string actual)
    {
        Assert.EndsWith(expected, actual, StringComparison.InvariantCulture);
    }
}
