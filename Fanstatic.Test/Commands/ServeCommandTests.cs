using System.Net;
using NSubstitute;
using Serilog;
using Fanstatic.Commands.Serve;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Xunit;

namespace Fanstatic.Test.Commands;

public class ServeCommandTests : TestSetup
{
    readonly IFileSystem
        _mockFileSystem = Substitute.For<IFileSystem>();

    readonly IFileWatcher _mockFileWatcher =
        Substitute.For<IFileWatcher>();

    readonly IPortSelector _mockPortSelector =
        Substitute.For<IPortSelector>();

    readonly ServeOptions _serverOptionsMock = new()
    {
        SourceArgument =
            Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst01))
    };

    public ServeCommandTests()
    {
        // Create a mock Site with minimal setup
        Site = new Site(
            GenerateOptionsMock,
            SiteSettingsMock,
            FrontMatterParserMock,
            LoggerMock,
            SystemClockMock);

        // Mock file system methods to prevent exceptions
        _mockFileSystem.FileExists(Arg.Any<string>()).Returns(true);
        _mockFileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);
        _mockFileSystem.FileReadAllText(Arg.Any<string>())
            .Returns("Title: test");
        _mockFileSystem.DirectoryGetDirectories(Arg.Any<string>())
            .Returns([]);
        _mockFileSystem.DirectoryGetFiles(Arg.Any<string>())
            .Returns([]);

        FrontMatterParserMock.Parse<SiteSettings>(Arg.Any<string>())
            .Returns(SiteSettingsMock);

        _mockPortSelector.SelectAvailablePort(ServeCommand.BaseUrlDefault, ServeCommand.PortDefault,
                ServeCommand.MaxPortTries)
            .Returns(2441);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        using var serveCommand = new ServeCommand(
            _serverOptionsMock,
            LoggerMock,
            _mockFileWatcher,
            _mockFileSystem,
            _mockPortSelector
        );

        // Assert
        _mockFileWatcher.Received(1).Start(
            Arg.Is<string>(path =>
                path == Path.GetFullPath(_serverOptionsMock.Source)),
            Arg.Any<Action<object, FileSystemEventArgs>>()
        );
    }

    [Fact]
    public void StartServer_ShouldUseSelectedPort()
    {
        // Arrange
        const int expectedPort = 1234;
        _mockPortSelector
            .SelectAvailablePort(Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<int>())
            .Returns(expectedPort);

        using var serveCommand = CreateServeCommand();

        // Act
        serveCommand.StartServer();

        // Assert
        _mockPortSelector.Received(1).SelectAvailablePort(
            Arg.Is(ServeCommand.BaseUrlDefault),
            Arg.Is(ServeCommand.PortDefault),
            Arg.Is(ServeCommand.MaxPortTries)
        );
        Assert.Equal(expectedPort, serveCommand.PortUsed);
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var serveCommand = CreateServeCommand();

        // Act
        serveCommand.StartServer();
        serveCommand.Dispose();

        // Assert
        _mockFileWatcher.Received(1).Stop();
    }

    ServeCommand CreateServeCommand()
    {
        // Helper method to create a ServeCommand with mocked dependencies
        return new ServeCommand(
            _serverOptionsMock,
            LoggerMock,
            _mockFileWatcher,
            _mockFileSystem,
            _mockPortSelector
        );
    }
}

public class DefaultPortSelectorTests
{
    readonly ILogger _mockLogger = Substitute.For<ILogger>();

    [Fact]
    public void SelectAvailablePort_WhenInitialPortAvailable_ReturnsSamePort()
    {
        // Arrange
        const int initialPort = 5000;
        var portSelector = new DefaultPortSelector(_mockLogger);

        // Act & Assert
        var selectedPort =
            portSelector.SelectAvailablePort("http://localhost", initialPort,
                10);
        Assert.Equal(initialPort, selectedPort);
    }

    [Fact]
    public void
        SelectAvailablePort_WhenInitialPortInUse_ReturnsNextAvailablePort()
    {
        // Arrange
        var portSelector = new DefaultPortSelector(_mockLogger);

        // Block only the initial port; the next one stays free. A free base port
        // is discovered at runtime so the test never collides with a hard-coded
        // port that another process (or another test) may already hold.
        var blockingListeners = BlockConsecutivePorts(1, out var initialPort);

        try
        {
            // Act
            var selectedPort =
                portSelector.SelectAvailablePort("http://localhost",
                    initialPort, 10);

            // Assert
            Assert.NotEqual(initialPort, selectedPort);
            Assert.True(selectedPort > initialPort);
        }
        finally
        {
            ReleaseListeners(blockingListeners);
        }
    }

    [Fact]
    public void SelectAvailablePort_WhenNoPortAvailable_ThrowsException()
    {
        // Arrange
        const int maxTries = 10;
        var portSelector = new DefaultPortSelector(_mockLogger);

        // Block the whole range the selector will probe.
        var blockingListeners =
            BlockConsecutivePorts(maxTries, out var initialPort);

        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                portSelector.SelectAvailablePort("http://localhost",
                    initialPort, maxTries)
            );
        }
        finally
        {
            ReleaseListeners(blockingListeners);
        }
    }

    /// <summary>
    /// Finds and binds <paramref name="count"/> consecutive free ports, returning
    /// the live listeners and the first port of the range. Retries from a new
    /// random base if a range is partially taken, so the test does not depend on
    /// any fixed port being available in the environment.
    /// </summary>
    static List<HttpListener> BlockConsecutivePorts(int count,
        out int basePort)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var start = Random.Shared.Next(20000, 50000);
            var listeners = new List<HttpListener>();
            var success = true;

            for (var i = 0; i < count; i++)
            {
                HttpListener? listener = new();
                try
                {
                    listener.Prefixes.Add($"http://localhost:{start + i}/");
                    listener.Start();
                    listeners.Add(listener);
                    listener = null; // ownership transferred to the list
                }
                catch (HttpListenerException)
                {
                    success = false;
                    break;
                }
                finally
                {
                    listener?.Close();
                }
            }

            if (success)
            {
                basePort = start;
                return listeners;
            }

            ReleaseListeners(listeners);
        }

        throw new InvalidOperationException(
            $"Could not reserve {count} consecutive free ports for the test.");
    }

    static void ReleaseListeners(List<HttpListener> listeners)
    {
        foreach (var listener in listeners)
        {
            listener.Stop();
            listener.Close();
        }
    }
}
