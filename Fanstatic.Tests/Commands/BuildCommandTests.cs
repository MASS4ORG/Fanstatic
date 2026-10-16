using Fanstatic.Commands.Build;
using Fanstatic.Helpers;
using NSubstitute;
using Serilog;
using Serilog.Sinks.InMemory;
using Xunit;

namespace Fanstatic.Test.Commands;

/// <summary>Unit tests for the BuildCommand class.</summary>
public class BuildCommandTests : TestSetup
{
    readonly ILogger _logger;
    readonly IFileSystem _fileSystem;
    readonly BuildOptions _options;

    /// <inheritdoc/>
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

    /// <summary>Confirms that BuildCommand can be constructed when parameters are valid.</summary>
    [Fact]
    public void ConstructorShouldNotThrowExceptionWhenParametersAreValid()
    {
        // Act
        var result = new BuildCommand(_options, _logger, _fileSystem);

        // Assert
        Assert.IsType<BuildCommand>(result);
    }

    /// <summary>Confirms that BuildCommand constructor throws ArgumentNullException when options is null.</summary>
    [Fact]
    public void ConstructorShouldThrowArgumentNullExceptionWhenOptionsIsNull()
    {
        // Act and Assert
        Assert.Throws<ArgumentNullException>(() =>
            new BuildCommand(null!, _logger, _fileSystem));
    }

    /// <summary>Confirms that BuildCommand.Run returns 0 when execution succeeds.</summary>
    [Fact]
    public void Run()
    {
        // Act
        var command = new BuildCommand(_options, _logger, _fileSystem);
        var result = command.Run();

        // Assert
        Assert.Equal(0, result);
    }

    /// <inheritdoc/>
    /// <summary>Confirms that CopyFolder creates the output directory when the source folder exists.</summary>
    [Fact]
    public void CopyFolderShouldCallCreateDirectoryWhenSourceFolderExists()
    {
        // Arrange
        _fileSystem.DirectoryExists("sourceFolder").Returns(true);
        var buildCommand = new BuildCommand(_options, _logger, _fileSystem);

        // Act
        buildCommand.CopyFolder("sourceFolder", "outputFolder");

        // Assert
        _fileSystem.Received(1).DirectoryCreateDirectory("outputFolder");
    }

    /// <inheritdoc/>
    /// <summary>Confirms that CopyFolder does not create an output directory when the source folder does not exist.</summary>
    [Fact]
    public void
        CopyFolderShouldNotCallCreateDirectoryWhenSourceFolderDoesNotExist()
    {
        // Arrange
        _fileSystem.DirectoryExists("sourceFolder").Returns(false);
        var buildCommand = new BuildCommand(_options, _logger, _fileSystem);

        // Act
        buildCommand.CopyFolder("sourceFolder", "outputFolder");

        // Assert
        _fileSystem.DidNotReceive().DirectoryCreateDirectory(Arg.Any<string>());
    }

    /// <inheritdoc/>
    /// <summary>Confirms that CopyFolder preserves subfolder structure when copying nested files.</summary>
    [Fact]
    public void CopyFolderShouldKeepSubfoldersWhenSourceFolderHasNestedFiles()
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

    /// <inheritdoc/>
    /// <summary>Confirms that CopyFolder copies nothing when the source folder has no files.</summary>
    [Fact]
    public void CopyFolderShouldNotCopyAnythingWhenSourceFolderHasNoFiles()
    {
        // Arrange
        _fileSystem.DirectoryExists("sourceFolder").Returns(true);
        var buildCommand = new BuildCommand(_options, _logger, _fileSystem);

        // Act
        buildCommand.CopyFolder("sourceFolder", "outputFolder");

        // Assert
        _fileSystem.DidNotReceive().FileCopy(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    /// <inheritdoc/>
    /// <summary>Confirms that BuildSite returns failure exit codes when template errors are found.</summary>
    [Theory]
    [InlineData(TestSitePathConst07, 1, 0)]
    [InlineData(TestSitePathConst06, 0, 0)]
    [InlineData(TestSitePathConst15, 1, 0)]
    public async Task RunShouldReturnFailureWhenTemplateErrorsWereFound(string testSitePath,
        int expectedExitCode, int expectedContinueOnErrorExitCode)
    {
        // Act
        var exitCode = await BuildSite(testSitePath, continueOnError: false);
        var continueOnErrorExitCode = await BuildSite(testSitePath, continueOnError: true);

        // Assert
        Assert.Equal(expectedExitCode, exitCode);
        Assert.Equal(expectedContinueOnErrorExitCode, continueOnErrorExitCode);
    }

    /// <inheritdoc/>
    /// <summary>Confirms that BuildCommand copies nested static folders to the output directory.</summary>
    [Fact]
    public async Task RunShouldCopyNestedStaticFolders()
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

    /// <inheritdoc/>
    /// <summary>Confirms that one error log is emitted for each template with a syntax error.</summary>
    [Fact]
    public async Task RunShouldLogOneMessagePerTemplateWhenSyntaxErrorIsFound()
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

    /// <summary>Builds the specified test site and returns the exit code.</summary>
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

    /// <summary>Creates a new temporary output path for test artifacts.</summary>
    static string NewOutputPath() =>
        Path.Combine(Path.GetTempPath(), "fanstatic-test-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Deletes the specified output directory if it exists.</summary>
    static void DeleteOutput(string outputPath)
    {
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, recursive: true);
        }
    }
}
