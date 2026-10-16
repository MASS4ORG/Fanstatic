using System.Globalization;
using Fanstatic.Commands;
using Fanstatic.Models;
using Fanstatic.Parsers;
using NSubstitute;
using Serilog;

namespace Fanstatic.Test;

/// <summary>Shared base for tests: fixture paths, mocks, and a preconfigured <see cref="ISite"/>.</summary>
public class TestSetup
{
    /// <summary>Fixed title reused as front matter by page-related tests.</summary>
    protected const string TitleConst = "Test Title";
    /// <summary>Placeholder markdown source path for content-source tests.</summary>
    protected const string SourcePathConst = "/path/to/file.md";
    /// <summary>Pinned "today" (2023-04-01) supplied through the system clock mock.</summary>
    protected readonly DateTime TodayDate = DateTime.Parse("2023-04-01", CultureInfo.InvariantCulture);
    /// <summary>Pinned future date (2023-07-01) for expiry and scheduling assertions.</summary>
    protected readonly DateTime FutureDate = DateTime.Parse("2023-07-01", CultureInfo.InvariantCulture);

    /// <summary>Path to the base "01" fixture test site.</summary>
    protected const string TestSitePathConst01 = ".TestSites/01";
    /// <summary>Path to the fixture test site that contains an index page.</summary>
    protected const string TestSitePathConst02 = ".TestSites/02-have-index";
    /// <summary>Path to the fixture test site that defines a section.</summary>
    protected const string TestSitePathConst03 = ".TestSites/03-section";
    /// <summary>Path to the fixture test site that defines tags.</summary>
    protected const string TestSitePathConst04 = ".TestSites/04-tags";
    /// <summary>Path to the fixture test site whose theme lacks a baseof template.</summary>
    protected const string TestSitePathConst05 = ".TestSites/05-theme-no-baseof";
    /// <summary>Path to the fixture test site with a working theme.</summary>
    protected const string TestSitePathConst06 = ".TestSites/06-theme";
    /// <summary>Path to the fixture test site where a missing baseof is an error case.</summary>
    protected const string TestSitePathConst07 = ".TestSites/07-theme-no-baseof-error";
    /// <summary>Path to the fixture test site whose theme uses an HTML template.</summary>
    protected const string TestSitePathConst08 = ".TestSites/08-theme-html";
    /// <summary>Path to the fixture test site exercising cascade configuration.</summary>
    protected const string TestSitePathConst09 = ".TestSites/09-cascade";
    /// <summary>Path to the fixture test site with page resources.</summary>
    protected const string TestSitePathConst11 = ".TestSites/11-page-resources";
    /// <summary>Path to the fixture test site referencing a missing partial.</summary>
    protected const string TestSitePathConst15 = ".TestSites/15-missing-partial";
    /// <summary>Path to the fixture test site with nested static files.</summary>
    protected const string TestSitePathConst16 = ".TestSites/16-static-nested";

    /// <summary>Real YAML front matter parser used when a test needs genuine parsing.</summary>
    protected readonly IFrontMatterParser FrontMatterParser = new YamlParser();
    /// <summary>NSubstitute stand-in for the front matter parser.</summary>
    protected readonly IFrontMatterParser FrontMatterParserMock = Substitute.For<IFrontMatterParser>();
    /// <summary>NSubstitute mock of generate options shared by tests.</summary>
    protected readonly IGenerateOptions GenerateOptionsMock = Substitute.For<IGenerateOptions>();
    /// <summary>NSubstitute mock of site settings shared by tests.</summary>
    protected readonly SiteSettings SiteSettingsMock = Substitute.For<SiteSettings>();
    /// <summary>NSubstitute mock of the logger so tests stay silent.</summary>
    protected readonly ILogger LoggerMock = Substitute.For<ILogger>();
    /// <summary>NSubstitute mock of the system clock so tests can pin "now".</summary>
    protected readonly ISystemClock SystemClockMock = Substitute.For<ISystemClock>();

    /// <summary>Content source backed by <see cref="SourcePathConst"/> and <see cref="TitleConst"/>.</summary>
    protected readonly ContentSource ContentSourceMock = new(SourcePathConst, new FrontMatter
    {
        Title = TitleConst,
    }, string.Empty);

    /// <summary>Site under test, built in the constructor with the shared mocks.</summary>
    protected ISite Site;

    // based on the compiled test.dll path
    // that is typically "bin/Debug/netX.0/test.dll"
    /// <summary>Repository-relative path from the test binary to the fixture test sites.</summary>
    protected const string TestSitesPath = "../../..";

    /// <summary>Initializes the system clock and a site using the default <see cref="SiteSettingsMock"/>.</summary>
    protected TestSetup()
    {
        _ = SystemClockMock.Now.Returns(TodayDate);
        Site = new Site(GenerateOptionsMock, SiteSettingsMock, FrontMatterParser, LoggerMock, SystemClockMock);
    }

    /// <summary>Initializes the system clock and a site using the given <paramref name="siteSettings"/>.</summary>
    public TestSetup(SiteSettings siteSettings)
    {
        _ = SystemClockMock.Now.Returns(TodayDate);
        Site = new Site(GenerateOptionsMock, siteSettings, FrontMatterParser, LoggerMock, SystemClockMock);
    }
}
