using System.Reflection;
using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.TemplateEngine;
using NSubstitute;
using Serilog;
using Xunit;

namespace Fanstatic.Test.Commands;

/// <summary>Unit tests for the BaseGeneratorCommand class.</summary>
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

    /// <summary>Confirms that the constructor rejects a null options argument.</summary>
    [Fact]
    public void ConstructorShouldThrowArgumentNullExceptionWhenOptionsIsNull()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new BaseGeneratorCommandStub(null!, TestLogger, _fs));
    }

    /// <summary>Confirms that the constructor rejects a null logger argument.</summary>
    [Fact]
    public void ConstructorShouldThrowArgumentNullExceptionWhenLoggerIsNull()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new BaseGeneratorCommandStub(TestOptions, null!, _fs));
    }

    /// <summary>Confirms that CheckValueInDictionary returns true when the dictionary holds the expected value for the key.</summary>
    [Fact]
    public void CheckValueInDictionaryShouldWorkCorrectly()
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
