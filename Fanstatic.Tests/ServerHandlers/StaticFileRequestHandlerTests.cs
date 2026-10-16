using Fanstatic.ServerHandlers;
using NSubstitute;
using Xunit;

namespace Fanstatic.Test.ServerHandlers;

/// <summary>Unit tests for the StaticFileRequest class.</summary>
public class StaticFileRequestHandlerTests : TestSetup, IDisposable
{
    readonly string _tempFilePath;

    /// <summary>Creates a temporary file that the tests read and clean up.</summary>
    public StaticFileRequestHandlerTests()
    {
        // Creating a temporary file for testing purposes
        _tempFilePath = Path.GetTempFileName();
        File.WriteAllText(_tempFilePath, "test");
    }

    /// <summary>Confirms that Check returns true when the requested file exists under the base path.</summary>
    [Fact]
    public void CheckReturnsTrueForExistingFile()
    {
        // Arrange
        var requestPath = Path.GetFileName(_tempFilePath);
        var basePath = Path.GetDirectoryName(_tempFilePath)
                       ?? throw new InvalidOperationException(
                           "Unable to determine directory of temporary file.");

        var staticFileRequest = new StaticFileRequest(basePath, false);

        // Act
        var result = staticFileRequest.Check(requestPath: new(requestPath,
            UriKind.Relative));

        // Assert
        Assert.True(result);
    }

    /// <summary>Confirms that Handle writes the file content to the response stream and returns the expected content code.</summary>
    [Fact]
    public async Task HandleReturnsExpectedContent()
    {
        // Arrange
        var requestPath = Path.GetFileName(_tempFilePath);
        var basePath = Path.GetDirectoryName(_tempFilePath)
                       ?? throw new InvalidOperationException(
                           "Unable to determine directory of temporary file.");
        var staticFileRequest = new StaticFileRequest(basePath, true);


        var response = Substitute.For<IHttpListenerResponse>();
        var stream = new MemoryStream();
        _ = response.OutputStream.Returns(stream);

        // Act
        _ = staticFileRequest.Check(
            new(requestPath, UriKind.RelativeOrAbsolute));
        var code = await staticFileRequest
            .Handle(response, new(requestPath, UriKind.RelativeOrAbsolute),
                DateTime.Now)
            .ConfigureAwait(true);

        // Assert
        _ = stream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal("test", content);

        Assert.Equal("themeSt", code);
    }

    /// <summary>Deletes the temporary file created for the tests.</summary>
    public void Dispose()
    {
        // Cleaning up the temporary file after tests run
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }

        GC.SuppressFinalize(this);
    }
}
