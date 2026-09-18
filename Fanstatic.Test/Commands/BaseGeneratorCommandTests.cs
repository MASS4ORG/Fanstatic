using System.Reflection;
using NSubstitute;
using Serilog;
using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.TemplateEngine;
using Xunit;

namespace Fanstatic.Test.Commands;

public class BaseGeneratorCommandTests
{
    static readonly IGenerateOptions TestOptions = new GenerateOptions
    {
        SourceArgument = "test_source"
    };

    static readonly ILogger TestLogger = new LoggerConfiguration().CreateLogger();

    class BaseGeneratorCommandStub(IGenerateOptions options, ILogger logger, IFileSystem fs)
        : BaseGeneratorCommand(options, logger, fs);

    readonly IFileSystem _fs = Substitute.For<IFileSystem>();

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOptionsIsNull()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new BaseGeneratorCommandStub(null!, TestLogger, _fs));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new BaseGeneratorCommandStub(TestOptions, null!, _fs));
    }

    [Fact]
    public void CheckValueInDictionary_ShouldWorkCorrectly()
    {
        var type = typeof(FluidTemplateEngine);
        var method = type.GetMethod("CheckValueInDictionary", BindingFlags.NonPublic | BindingFlags.Static);
        var parameters = new object[]
        {
            new[] { "key" },
            new Dictionary<string, object> { { "key", "value" } },
            "value"
        };

        Assert.NotNull(method);
        var result = method.Invoke(null, parameters);

        Assert.NotNull(result);
        Assert.True((bool)result);
    }
}
