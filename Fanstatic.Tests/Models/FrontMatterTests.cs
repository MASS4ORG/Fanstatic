using Fanstatic.Models;
using Xunit;

namespace Fanstatic.Test.Models;

/// <summary>Tests for front matter property assignment.</summary>
public class FrontMatterTests : TestSetup
{
    /// <summary>Confirms that the constructor stores the assigned front matter properties.</summary>
    [Theory]
    [InlineData("Title1", "Section1", "Type1", "URL1")]
    [InlineData("Title2", "Section2", "Type2", "URL2")]
    [InlineData("Title3", "Section3", "Type3", "URL3")]
    public void ConstructorSetsPropertiesCorrectly(string title, string section, string type, string url)
    {
        // Act
        var basicContent = new FrontMatter
        {
            Title = title,
            Section = section,
            Type = type,
            Url = url
        };

        // Assert
        Assert.Equal(title, basicContent.Title);
        Assert.Equal(section, basicContent.Section);
        Assert.Equal(type, basicContent.Type);
        Assert.Equal(url, basicContent.Url);
    }
}
