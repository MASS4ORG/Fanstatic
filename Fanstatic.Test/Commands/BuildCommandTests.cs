using Fanstatic.Commands.Build;
using Fanstatic.Helpers;
using NSubstitute;
using Serilog;
using Serilog.Sinks.InMemory;
using Xunit;

namespace Fanstatic.Test.Commands;

public class BuildCommandTests : TestSetup
{
    readonly ILogger _logger;
    readonly IFileSystem _fileSystem;
    readonly BuildOptions _options;

    public BuildCommandTests()
    {
        _logger = Substitute.For<ILogger>();
        _fileSystem = Substitute.For<IFileSystem>();
        _fileSystem.FileExists("./fanstatic.yaml").Returns(true);
        _fileSystem.FileReadAllText("./fanstatic.yaml").Returns(
            """
            Title: test
            """);
        _options = new BuildOptions { Output = "test" };
    }

    [Fact]
    public void Constructor_ShouldNotThrowException_WhenParametersAreValid()
    {
        // Act
        var result = new BuildCommand(_options, _logger, _fileSystem);

        // Assert
        Assert.IsType<BuildCommand>(result);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOptionsIsNull()
    {
        // Act and Assert
        Assert.Throws<ArgumentNullException>(() =>
            new BuildCommand(null!, _logger, _fileSystem));
    }

    [Fact]
    public void Run()
    {
        // Act
        var command = new BuildCommand(_options, _logger, _fileSystem);
        var result = command.Run();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void CopyFolder_ShouldCallCreateDirectory_WhenSourceFolderExists()
    {
        // Arrange
        _fileSystem.DirectoryExists("sourceFolder").Returns(true);
        var buildCommand = new BuildCommand(_options, _logger, _fileSystem);

        // Act
        buildCommand.CopyFolder("sourceFolder", "outputFolder");

        // Assert
        _fileSystem.Received(1).DirectoryCreateDirectory("outputFolder");
    }

    [Fact]
    public void
        CopyFolder_ShouldNotCallCreateDirectory_WhenSourceFolderDoesNotExist()
    {
        // Arrange
        _fileSystem.DirectoryExists("sourceFolder").Returns(false);
        var buildCommand = new BuildCommand(_options, _logger, _fileSystem);

        // Act
        buildCommand.CopyFolder("sourceFolder", "outputFolder");

        // Assert
        _fileSystem.DidNotReceive().DirectoryCreateDirectory(Arg.Any<string>());
    }

    [Fact]
    public void CopyFolder_ShouldKeepSubfolders_WhenSourceFolderHasNestedFiles()
    {
        // Arrange
        _fileSystem.DirectoryExists("sourceFolder").Returns(true);
        _fileSystem.DirectoryGetFiles("sourceFolder", "*.*", true).Returns(
        [
            Path.Combine("sourceFolder", "robots.txt"),
            Path.Combine("sourceFolder", "css", "site.css")
        ]);
        var buildCommand = new BuildCommand(_options, _logger, _fileSystem);

        // Act
        buildCommand.CopyFolder("sourceFolder", "outputFolder");

        // Assert
        _fileSystem.Received(1).DirectoryCreateDirectory(Path.Combine("outputFolder", "css"));
        _fileSystem.Received(1).FileCopy(Path.Combine("sourceFolder", "robots.txt"),
            Path.Combine("outputFolder", "robots.txt"), true);
        _fileSystem.Received(1).FileCopy(Path.Combine("sourceFolder", "css", "site.css"),
            Path.Combine("outputFolder", "css", "site.css"), true);
    }

    [Fact]
    public void CopyFolder_ShouldNotCopyAnything_WhenSourceFolderHasNoFiles()
    {
        // Arrange
        _fileSystem.DirectoryExists("sourceFolder").Returns(true);
        var buildCommand = new BuildCommand(_options, _logger, _fileSystem);

        // Act
        buildCommand.CopyFolder("sourceFolder", "outputFolder");

        // Assert
        _fileSystem.DidNotReceive().FileCopy(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Theory]
    [InlineData(TestSitePathConst07, 1, 0)]
    [InlineData(TestSitePathConst06, 0, 0)]
    [InlineData(TestSitePathConst15, 1, 0)]
    public async Task Run_ShouldReturnFailure_WhenTemplateErrorsWereFound(string testSitePath,
        int expectedExitCode, int expectedContinueOnErrorExitCode)
    {
        // Act
        var exitCode = await BuildSite(testSitePath, continueOnError: false);
        var continueOnErrorExitCode = await BuildSite(testSitePath, continueOnError: true);

        // Assert
        Assert.Equal(expectedExitCode, exitCode);
        Assert.Equal(expectedContinueOnErrorExitCode, continueOnErrorExitCode);
    }

    [Fact]
    public async Task Run_ShouldCopyNestedStaticFolders()
    {
        // Arrange
        var outputPath = NewOutputPath();
        var options = new BuildOptions
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst16)),
            Output = outputPath
        };

        try
        {
            // Act
            var exitCode = await BuildCommand.Create(options, _logger);

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Equal("body { color: rebeccapurple; }", File.ReadAllText(Path.Combine(outputPath, "css", "site.css")));
            Assert.Equal("console.log('theme');", File.ReadAllText(Path.Combine(outputPath, "js", "app.js")));
            Assert.Equal("SITE-ROOT-FILE", File.ReadAllText(Path.Combine(outputPath, "robots.txt")));
        }
        finally
        {
            DeleteOutput(outputPath);
        }
    }

    [Fact]
    public async Task Run_ShouldLogOneMessagePerTemplate_WhenSyntaxErrorIsFound()
    {
        // Arrange
        var inMemorySink = new InMemorySink();
        var logger = new LoggerConfiguration().WriteTo.Sink(inMemorySink).CreateLogger();

        // Act
        _ = await BuildSite(TestSitePathConst07, continueOnError: false, logger);

        // Assert
        var indexErrorLines = inMemorySink.LogEvents
            .Select(logEvent => logEvent.RenderMessage())
            .Where(rendered => rendered.Contains("_default/index.html:1:14", StringComparison.Ordinal))
            .ToList();

        Assert.Single(indexErrorLines);
    }

    async Task<int> BuildSite(string testSitePath, bool continueOnError, ILogger? logger = null)
    {
        var outputPath = NewOutputPath();
        var options = new BuildOptions
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, testSitePath)),
            Output = outputPath,
            ContinueOnError = continueOnError
        };

        try
        {
            return await BuildCommand.Create(options, logger ?? _logger);
        }
        finally
        {
            DeleteOutput(outputPath);
        }
    }

    static string NewOutputPath() =>
        Path.Combine(Path.GetTempPath(), "fanstatic-test-" + Guid.NewGuid().ToString("N")[..8]);

    static void DeleteOutput(string outputPath)
    {
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, recursive: true);
        }
    }
}
