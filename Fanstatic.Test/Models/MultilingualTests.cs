using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Xunit;

namespace Fanstatic.Test.Models;

/// <summary>
/// Tests for multi-language (i18n) content, translations and alternate variants.
/// </summary>
public class MultilingualTests : TestSetup
{
    const string MultilangSitePath = ".TestSites/13-multilang";

    readonly Site _site;

    public MultilingualTests()
    {
        var fs = new FileSystem();
        var sitePath = Path.GetFullPath(Path.Combine(TestSitesPath, MultilangSitePath));
        var options = new GenerateOptions { SourceArgument = sitePath };
        var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, FrontMatterParser, fs);

        _site = new Site(options, settings, FrontMatterParser, LoggerMock, SystemClockMock);
        _site.ResetCache();
        _site.ScanAndParseSourceFiles(fs, _site.SourceContentPath);
        _site.BuildTranslationGroups();
        _site.ProcessPages();
    }

    Page Page(string sourceRelativePath, string outputFormat = "html") =>
        _site.OutputReferences.Values
            .OfType<Page>()
            .First(page => page.SourceRelativePath == sourceRelativePath
                           && page.OutputFormat == outputFormat
                           && page.PageIndex == 1);

    [Fact]
    public void Languages_ShouldBeResolvedFromConfig()
    {
        Assert.Equal(2, _site.LanguageList.Count);
        Assert.Equal("en", _site.DefaultLanguageObj.Code);
        Assert.True(_site.DefaultLanguageObj.IsDefault);
        Assert.True(_site.IsMultilingual);

        var ptBr = _site.GetLanguage("pt-br");
        Assert.Equal("pt-br", ptBr.Code);
        Assert.Equal("Português", ptBr.LanguageName);
        Assert.Equal("Meu Site", ptBr.Title);
        Assert.False(ptBr.IsDefault);
    }

    [Fact]
    public void DefaultLanguagePage_ShouldHaveNoLanguagePrefix()
    {
        var about = Page("about.md");

        Assert.Equal("en", about.Language.Code);
        Assert.True(about.IsDefaultLanguage);
        Assert.Equal("/about/index.html", about.RelPermalink.ToString());
    }

    [Fact]
    public void NonDefaultLanguagePage_ShouldBePrefixed()
    {
        var about = Page("about.pt-br.md");

        Assert.Equal("pt-br", about.Language.Code);
        Assert.False(about.IsDefaultLanguage);
        Assert.StartsWith("/pt-br/", about.RelPermalink.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void LanguageSuffix_ShouldNotLeakIntoUrl()
    {
        var about = Page("about.pt-br.md");
        Assert.DoesNotContain("pt-br/about.pt-br", about.RelPermalink.ToString(), StringComparison.Ordinal);
        Assert.Equal("about", about.ContentSource.LogicalFileNameWithoutExtension);
    }

    [Fact]
    public void Translations_ShouldLinkTheSameLogicalPage()
    {
        var helloEn = Page("posts/hello.md");
        var helloPt = Page("posts/hello.pt-br.md");

        Assert.Equal(2, helloEn.AllTranslations.Count());
        Assert.Contains(helloPt, helloEn.Translations);
        Assert.DoesNotContain(helloEn, helloEn.Translations);
        Assert.Contains(helloEn.Translations, page => page.Language.Code == "pt-br");
    }

    [Fact]
    public void UntranslatedPage_ShouldHaveOnlyItself()
    {
        var onlyEn = Page("posts/only-en.md");

        Assert.Single(onlyEn.AllTranslations);
        Assert.Empty(onlyEn.Translations);
    }

    [Fact]
    public void AlternativeOutputFormats_ShouldExposeSameLanguageVariants()
    {
        var helloEn = Page("posts/hello.md");

        var formats = helloEn.AlternativeOutputFormats.ToList();
        Assert.Contains(formats, page => page.OutputFormat == "html");
        Assert.Contains(formats, page => page.OutputFormat == "rss");
        Assert.All(formats, page => Assert.Equal("en", page.Language.Code));
    }

    [Fact]
    public void Variants_ShouldSpanLanguagesAndFormats()
    {
        var helloEn = Page("posts/hello.md");

        var variants = helloEn.Variants.ToList();
        // en(html,rss) + pt-br(html,rss)
        Assert.Contains(variants, page => page is { OutputFormat: "html" } && page.Language.Code == "en");
        Assert.Contains(variants, page => page is { OutputFormat: "rss" } && page.Language.Code == "pt-br");
    }

    [Fact]
    public void Pages_ShouldOnlyIncludeSameLanguage()
    {
        var postsEn = Page("posts/_index.md");

        Assert.NotEmpty(postsEn.Pages);
        Assert.All(postsEn.Pages, page => Assert.Equal("en", page.Language.Code));
        Assert.DoesNotContain(postsEn.Pages,
            page => page.RelPermalink.ToString().StartsWith("/pt-br/", StringComparison.Ordinal));
    }

    [Fact]
    public void TranslationsByLanguage_ShouldMapEveryTranslation()
    {
        var helloEn = Page("posts/hello.md");

        Assert.Equal(2, helloEn.TranslationsByLanguage.Count);
        Assert.Equal(helloEn, helloEn.TranslationsByLanguage["en"]);
        Assert.Equal("pt-br", helloEn.TranslationsByLanguage["pt-br"].Language.Code);
    }

    [Fact]
    public void TranslationsByLanguage_ShouldOmitMissingTranslations()
    {
        var onlyEn = Page("posts/only-en.md");

        Assert.Single(onlyEn.TranslationsByLanguage);
        Assert.False(onlyEn.TranslationsByLanguage.ContainsKey("pt-br"));

        // The language-home fallback used by switchers.
        Assert.Equal("/pt-br/", _site.GetLanguage("pt-br").RelPermalink.ToString());
        Assert.Equal("/", _site.DefaultLanguageObj.RelPermalink.ToString());
    }

    [Fact]
    public void TranslatedHome_ShouldBePrefixed()
    {
        Assert.True(
            _site.OutputReferences.ContainsKey(new Uri("/pt-br/index.html", UriKind.Relative)),
            "The Portuguese home page should be generated at /pt-br/index.html");
        Assert.True(
            _site.OutputReferences.ContainsKey(new Uri("/index.html", UriKind.Relative)),
            "The English home page should be generated at /index.html");
    }

    [Fact]
    public void AutoGeneratedSection_ShouldExistForAllLanguages()
    {
        Assert.True(
            _site.OutputReferences.ContainsKey(new Uri("/projects/index.html", UriKind.Relative)),
            "The English projects section should be generated at /projects/index.html");
        Assert.True(
            _site.OutputReferences.ContainsKey(new Uri("/pt-br/projects/index.html", UriKind.Relative)),
            "The Portuguese projects section should be generated at /pt-br/projects/index.html");
    }

    [Fact]
    public void PageInAutoGeneratedSection_ShouldHaveCorrectUrl()
    {
        var work = Page("projects/work.md");
        Assert.Equal("/projects/work/index.html", work.RelPermalink.ToString());
    }

    [Fact]
    public void TranslatedPageInAutoGeneratedSection_ShouldBePrefixed()
    {
        var workPt = Page("projects/work.pt-br.md");
        Assert.StartsWith("/pt-br/projects/", workPt.RelPermalink.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AutoGeneratedSection_ShouldHaveTranslations()
    {
        var sectionEn = Page("projects/_index.md");
        Assert.Equal(2, sectionEn.AllTranslations.Count());
        Assert.Contains(sectionEn.AllTranslations, page => page.Language.Code == "pt-br");
    }

    [Fact]
    public void I18nTranslations_ShouldBeLoaded()
    {
        Assert.Contains("en", _site.I18N.Keys);
        Assert.Contains("pt-br", _site.I18N.Keys);

        Assert.Equal("Read more", _site.I18N["en"]["read_more"].Other);
        Assert.Equal("Leia mais", _site.I18N["pt-br"]["read_more"].Other);
    }

    [Fact]
    public void I18nTranslations_ShouldSupportPluralization()
    {
        Assert.True(_site.I18N["en"].ContainsKey("posts"));
        Assert.NotNull(_site.I18N["en"]["posts"].One);
        Assert.NotNull(_site.I18N["en"]["posts"].Other);
    }

    [Fact]
    public void AutoGeneratedSectionPages_ShouldBeInCorrectLanguage()
    {
        var sectionEn = Page("projects/_index.md");
        Assert.Equal("en", sectionEn.Language.Code);
        Assert.True(sectionEn.IsDefaultLanguage);

        var sectionPt = Page("projects/_index.pt-br.md");
        Assert.Equal("pt-br", sectionPt.Language.Code);
        Assert.False(sectionPt.IsDefaultLanguage);
    }

    [Fact]
    public void PagesInAutoGeneratedSection_ShouldBeLinkedToSection()
    {
        var sectionEn = Page("projects/_index.md");
        Assert.NotEmpty(sectionEn.Pages);
        Assert.All(sectionEn.Pages, page => Assert.Equal("en", page.Language.Code));
    }
}
