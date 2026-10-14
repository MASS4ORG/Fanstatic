using Fanstatic.Commands.NewSite;
using Fanstatic.Helpers;
using Fanstatic.Models;
using NSubstitute;
using Serilog;
using Xunit;

namespace Fanstatic.Test.Commands;

public class NewSiteCommandTests
{
    readonly ILogger _logger = Substitute.For<ILogger>();
    readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    readonly ISite _site = Substitute.For<ISite>();

    [Fact]
    public void CreateShouldReturnNewSiteCommandWhenParametersAreValid()
    {
        // Arrange
        var options = new NewSiteOptions
        { Output = "test", Title = "Test", Description = "Test", BaseUrl = new("https://test.com") };

        // Act
        var result = NewSiteCommand.Run(options, _logger, _fileSystem);

        // Assert
        Assert.IsType<NewSiteCommand>(result);
    }

    [Fact]
    public void CreateShouldThrowArgumentNullExceptionWhenOptionsIsNull()
    {
        // Act and Assert
        Assert.Throws<ArgumentNullException>(testCode: () => NewSiteCommand.Run(null!, _logger, _fileSystem));
    }

    [Fact]
    public void CreateShouldNotReturnNullWhenParametersAreValid()
    {
        // Arrange
        var options = new NewSiteOptions
        { Output = "test", Title = "Test", Description = "Test", BaseUrl = new("https://test.com") };

        // Act
        var result = NewSiteCommand.Run(options, _logger, _fileSystem);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void RunShouldLogInformationWhenCreatingNewSite()
    {
        // Arrange
        var options = new NewSiteOptions
        { Output = "test", Title = "Test", Description = "Test", BaseUrl = new("https://test.com"), Force = false };
        _fileSystem.FileExists(Arg.Any<string>()).Returns(false);

        var newSiteCommand = NewSiteCommand.Run(options, _logger, _fileSystem);

        // Act
        newSiteCommand.Run();

        // Assert
        _logger.Received(1)
            .Information("Creating a new site: {Title} at {OutputPath}", options.Title, Arg.Any<string>());
    }

    [Fact]
    public void RunShouldCallCreateDirectoryWithCorrectPathsForEachFolder()
    {
        // Arrange
        var options = new NewSiteOptions
        { Output = "test", Title = "Test", Description = "Test", BaseUrl = new("https://test.com"), Force = false };
        _site.SourceFolders.Returns(["folder1", "folder2"]);
        _fileSystem.FileExists(Arg.Any<string>()).Returns(false);

        var newSiteCommand = new NewSiteCommand(options, _logger, _fileSystem, _site);

        // Act
        newSiteCommand.Run();

        // Assert
        _fileSystem.Received(1).DirectoryCreateDirectory("folder1");
        _fileSystem.Received(1).DirectoryCreateDirectory("folder2");
    }

    [Fact]
    public void RunShouldReturn1WhenCreateDirectoryThrowsException()
    {
        // Arrange
        var options = new NewSiteOptions
        { Output = "test", Title = "Test", Description = "Test", BaseUrl = new("https://test.com"), Force = false };
        _site.SourceFolders.Returns(["folder1", "folder2"]);
        _fileSystem.FileExists(Arg.Any<string>()).Returns(false);
        _fileSystem.When(x => x.DirectoryCreateDirectory(Arg.Any<string>()))
            .Do(_ => throw new ArgumentNullException());

        var newSiteCommand = new NewSiteCommand(options, _logger, _fileSystem, _site);

        // Act
        var result = newSiteCommand.Run();

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void RunShouldReturn0WhenDirectoryDoesNotExist()
    {
        // Arrange
        var options = new NewSiteOptions { Output = "test", Force = false };
        _fileSystem.FileExists(Arg.Any<string>()).Returns(false);

        var newSiteCommand = new NewSiteCommand(options, _logger, _fileSystem, _site);

        // Act
        var result = newSiteCommand.Run();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void RunShouldReturn1WhenForceIsFalseAndDirectoryExists()
    {
        // Arrange
        var options = new NewSiteOptions { Output = "test", Force = false };
        _fileSystem.FileExists(Arg.Any<string>()).Returns(true);

        var newSiteCommand = new NewSiteCommand(options, _logger, _fileSystem, _site);

        // Act
        var result = newSiteCommand.Run();

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void RunShouldReturn0WhenForceIsTrueAndDirectoryExists()
    {
        // Arrange
        var options = new NewSiteOptions { Output = "test", Force = true };
        _fileSystem.FileExists(Arg.Any<string>()).Returns(true);

        var newSiteCommand = new NewSiteCommand(options, _logger, _fileSystem, _site);

        // Act
        var result = newSiteCommand.Run();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void RunShouldReturn1WhenExportThrowsException()
    {
        // Arrange
        var options = new NewSiteOptions { Output = "test", Force = true };
        _fileSystem.FileExists(Arg.Any<string>()).Returns(false);
        _site.Parser
            .When(x => x.SerializeAndSave(Arg.Any<SiteSettings>(), Arg.Any<string>()))
            .Do(_ => throw new ArgumentNullException());

        var newSiteCommand = new NewSiteCommand(options, _logger, _fileSystem, _site);

        // Act
        var result = newSiteCommand.Run();

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void RunShouldCallCreateDirectoryForEachFolder()
    {
        // Arrange
        var options = new NewSiteOptions { Output = "test", Force = false };
        _site.SourceFolders.Returns(["folder1", "folder2"]);
        _fileSystem.FileExists(Arg.Any<string>()).Returns(false);

        var newSiteCommand = new NewSiteCommand(options, _logger, _fileSystem, _site);

        // Act
        newSiteCommand.Run();

        // Assert
        _fileSystem.Received(2).DirectoryCreateDirectory(Arg.Any<string>());
    }
}
