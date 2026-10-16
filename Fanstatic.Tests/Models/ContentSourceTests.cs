using System.Globalization;
using Fanstatic.Models;
using Xunit;

namespace Fanstatic.Test.Models;

/// <summary>Tests for content source file and path resolution.</summary>
public class ContentSourceTests : TestSetup
{
    /// <summary>Confirms that the source file name without extension is derived from the path.</summary>
    [Theory]
    [InlineData("C:/Test/Document.txt", "Document")]
    [InlineData("C:/Test/SubFolder/Document.txt", "Document")]
    [InlineData("Document.txt", "Document")]
    public void SourceFileNameWithoutExtensionReturnsCorrectFileName(string sourcePath, string expectedFileName)
    {
        // Arrange
        FrontMatter frontMatter = new("Title");
        ContentSource contentSource = new(sourcePath, frontMatter, string.Empty);

        // Act
        var actualFileName = (contentSource as IFile).SourceFileNameWithoutExtension;

        // Assert
        Assert.Equal(expectedFileName, actualFileName);
    }

    /// <summary>Confirms that the source directory is derived from the path.</summary>
    [Theory]
    [InlineData("C:/Test/Document.txt", "C:/Test")]
    [InlineData("C:/Test/SubFolder/Document.txt", "C:/Test/SubFolder")]
    [InlineData("/home/Test/Document.txt", "/home/Test")]
    [InlineData("/Test/SubFolder/Document.txt", "/Test/SubFolder")]
    [InlineData("Document.txt", "")]
    public void SourcePathDirectoryReturnsCorrectDirectory(string sourcePath,
        string expectedDirectory)
    {
        ArgumentNullException.ThrowIfNull(expectedDirectory);

        // Arrange
        var frontMatter = new FrontMatter("Title");
        ContentSource contentSource = new(sourcePath, frontMatter, string.Empty);

        // Act
        var actualDirectory = (contentSource as IFile).SourceRelativePathDirectory;
        // Normalize the expected directory path to use the correct separator for the current OS
        expectedDirectory = expectedDirectory.Replace('/', Path.DirectorySeparatorChar);

        // Assert
        Assert.Equal(expectedDirectory, actualDirectory);
    }

    /// <summary>Confirms that the publish date falls back to the date when absent.</summary>
    [Theory]
    [InlineData("2023-07-11T00:00:00", "2023-07-12T00:00:00", "2023-07-12T00:00:00")]
    [InlineData("2023-07-11T00:00:00", null, "2023-07-11T00:00:00")]
    [InlineData("2023-07-11", "2023-07-12", "2023-07-12")]
    [InlineData("2023-07-11", null, "2023-07-11")]
    [InlineData(null, null, null)]
    public void GetPublishDateReturnsPublishDateIfNotNullOtherwiseDate(
        string? dateString, string? publishDateString,
        string? expectedDateString)
    {
        // Arrange
        var date = string.IsNullOrEmpty(dateString)
            ? (DateTime?)null
            : DateTime.Parse(dateString, CultureInfo.InvariantCulture);
        var publishDate = string.IsNullOrEmpty(publishDateString)
            ? (DateTime?)null
            : DateTime.Parse(publishDateString, CultureInfo.InvariantCulture);
        var expectedDate = string.IsNullOrEmpty(expectedDateString)
            ? (DateTime?)null
            : DateTime.Parse(expectedDateString, CultureInfo.InvariantCulture);

        // Act
        FrontMatter frontMatter = new() { Date = date, PublishDate = publishDate };
        ContentSource contentSource = new(string.Empty, frontMatter, string.Empty);

        // Assert
        Assert.Equal(expectedDate, (contentSource as IContentSource).GetPublishDate);
        Assert.Equal(expectedDate, (contentSource as IContentSource).GetPublishDate);
    }
}
