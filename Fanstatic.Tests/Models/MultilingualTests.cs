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

    /// <summary>Initializes the fixture and builds the multilingual sample site.</summary>
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

    /// <summary>Confirms that configured languages are resolved with their codes, names and default flags.</summary>
    [Fact]
    public void LanguagesShouldBeResolvedFromConfig()
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

    /// <summary>Confirms that pages in the default language are generated without a language prefix.</summary>
    [Fact]
    public void DefaultLanguagePageShouldHaveNoLanguagePrefix()
    {
        var about = Page("about.md");

        Assert.Equal("en", about.Language.Code);
        Assert.True(about.IsDefaultLanguage);
        Assert.Equal("/about/index.html", about.RelPermalink.ToString());
    }

    /// <summary>Confirms that pages in a non-default language are generated under a language prefix.</summary>
    [Fact]
    public void NonDefaultLanguagePageShouldBePrefixed()
    {
        var about = Page("about.pt-br.md");

        Assert.Equal("pt-br", about.Language.Code);
        Assert.False(about.IsDefaultLanguage);
        Assert.StartsWith("/pt-br/", about.RelPermalink.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Confirms that the language suffix is stripped from the generated URL and logical file name.</summary>
    [Fact]
    public void LanguageSuffixShouldNotLeakIntoUrl()
    {
        var about = Page("about.pt-br.md");
        Assert.DoesNotContain("pt-br/about.pt-br", about.RelPermalink.ToString(), StringComparison.Ordinal);
        Assert.Equal("about", about.ContentSource.LogicalFileNameWithoutExtension);
    }

    /// <summary>Confirms that translations of the same logical page reference each other.</summary>
    [Fact]
    public void TranslationsShouldLinkTheSameLogicalPage()
    {
        var helloEn = Page("posts/hello.md");
        var helloPt = Page("posts/hello.pt-br.md");

        Assert.Equal(2, helloEn.AllTranslations.Count());
        Assert.Contains(helloPt, helloEn.Translations);
        Assert.DoesNotContain(helloEn, helloEn.Translations);
        Assert.Contains(helloEn.Translations, page => page.Language.Code == "pt-br");
    }

    /// <summary>Confirms that a page without translations has only itself in its translation set.</summary>
    [Fact]
    public void UntranslatedPageShouldHaveOnlyItself()
    {
        var onlyEn = Page("posts/only-en.md");

        Assert.Single(onlyEn.AllTranslations);
        Assert.Empty(onlyEn.Translations);
    }

    /// <summary>Confirms that alternative output formats share the page's language.</summary>
    [Fact]
    public void AlternativeOutputFormatsShouldExposeSameLanguageVariants()
    {
        var helloEn = Page("posts/hello.md");

        var formats = helloEn.AlternativeOutputFormats.ToList();
        Assert.Contains(formats, page => page.OutputFormat == "html");
        Assert.Contains(formats, page => page.OutputFormat == "rss");
        Assert.All(formats, page => Assert.Equal("en", page.Language.Code));
    }

    /// <summary>Confirms that variants combine every language and output format of a page.</summary>
    [Fact]
    public void VariantsShouldSpanLanguagesAndFormats()
    {
        var helloEn = Page("posts/hello.md");

        var variants = helloEn.Variants.ToList();
        // en(html,rss) + pt-br(html,rss)
        Assert.Contains(variants, page => page is { OutputFormat: "html" } && page.Language.Code == "en");
        Assert.Contains(variants, page => page is { OutputFormat: "rss" } && page.Language.Code == "pt-br");
    }

    /// <summary>Confirms that a section lists only pages of its own language.</summary>
    [Fact]
    public void PagesShouldOnlyIncludeSameLanguage()
    {
        var postsEn = Page("posts/_index.md");

        Assert.NotEmpty(postsEn.Pages);
        Assert.All(postsEn.Pages, page => Assert.Equal("en", page.Language.Code));
        Assert.DoesNotContain(postsEn.Pages,
            page => page.RelPermalink.ToString().StartsWith("/pt-br/", StringComparison.Ordinal));
    }

    /// <summary>Confirms that translations are indexed by language code.</summary>
    [Fact]
    public void TranslationsByLanguageShouldMapEveryTranslation()
    {
        var helloEn = Page("posts/hello.md");

        Assert.Equal(2, helloEn.TranslationsByLanguage.Count);
        Assert.Equal(helloEn, helloEn.TranslationsByLanguage["en"]);
        Assert.Equal("pt-br", helloEn.TranslationsByLanguage["pt-br"].Language.Code);
    }

    /// <summary>Confirms that languages without a translation are omitted and fall back to the language home.</summary>
    [Fact]
    public void TranslationsByLanguageShouldOmitMissingTranslations()
    {
        var onlyEn = Page("posts/only-en.md");

        Assert.Single(onlyEn.TranslationsByLanguage);
        Assert.False(onlyEn.TranslationsByLanguage.ContainsKey("pt-br"));

        // The language-home fallback used by switchers.
        Assert.Equal("/pt-br/", _site.GetLanguage("pt-br").RelPermalink.ToString());
        Assert.Equal("/", _site.DefaultLanguageObj.RelPermalink.ToString());
    }

    /// <summary>Confirms that translated home pages are generated under their language prefix.</summary>
    [Fact]
    public void TranslatedHomeShouldBePrefixed()
    {
        Assert.True(
            _site.OutputReferences.ContainsKey(new Uri("/pt-br/index.html", UriKind.Relative)),
            "The Portuguese home page should be generated at /pt-br/index.html");
        Assert.True(
            _site.OutputReferences.ContainsKey(new Uri("/index.html", UriKind.Relative)),
            "The English home page should be generated at /index.html");
    }

    /// <summary>Confirms that auto-generated sections exist for every configured language.</summary>
    [Fact]
    public void AutoGeneratedSectionShouldExistForAllLanguages()
    {
        Assert.True(
            _site.OutputReferences.ContainsKey(new Uri("/projects/index.html", UriKind.Relative)),
            "The English projects section should be generated at /projects/index.html");
        Assert.True(
            _site.OutputReferences.ContainsKey(new Uri("/pt-br/projects/index.html", UriKind.Relative)),
            "The Portuguese projects section should be generated at /pt-br/projects/index.html");
    }

    /// <summary>Confirms that a page in an auto-generated section gets the expected URL.</summary>
    [Fact]
    public void PageInAutoGeneratedSectionShouldHaveCorrectUrl()
    {
        var work = Page("projects/work.md");
        Assert.Equal("/projects/work/index.html", work.RelPermalink.ToString());
    }

    /// <summary>Confirms that translated pages in an auto-generated section are language-prefixed.</summary>
    [Fact]
    public void TranslatedPageInAutoGeneratedSectionShouldBePrefixed()
    {
        var workPt = Page("projects/work.pt-br.md");
        Assert.StartsWith("/pt-br/projects/", workPt.RelPermalink.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Confirms that an auto-generated section exposes its translations.</summary>
    [Fact]
    public void AutoGeneratedSectionShouldHaveTranslations()
    {
        var sectionEn = Page("projects/_index.md");
        Assert.Equal(2, sectionEn.AllTranslations.Count());
        Assert.Contains(sectionEn.AllTranslations, page => page.Language.Code == "pt-br");
    }

    /// <summary>Confirms that i18n translation files are loaded per language.</summary>
    [Fact]
    public void I18nTranslationsShouldBeLoaded()
    {
        Assert.Contains("en", _site.I18N.Keys);
        Assert.Contains("pt-br", _site.I18N.Keys);

        Assert.Equal("Read more", _site.I18N["en"]["read_more"].Other);
        Assert.Equal("Leia mais", _site.I18N["pt-br"]["read_more"].Other);
    }

    /// <summary>Confirms that loaded i18n entries expose singular and plural forms.</summary>
    [Fact]
    public void I18nTranslationsShouldSupportPluralization()
    {
        Assert.True(_site.I18N["en"].ContainsKey("posts"));
        Assert.NotNull(_site.I18N["en"]["posts"].One);
        Assert.NotNull(_site.I18N["en"]["posts"].Other);
    }

    /// <summary>Confirms that auto-generated section pages carry the correct language.</summary>
    [Fact]
    public void AutoGeneratedSectionPagesShouldBeInCorrectLanguage()
    {
        var sectionEn = Page("projects/_index.md");
        Assert.Equal("en", sectionEn.Language.Code);
        Assert.True(sectionEn.IsDefaultLanguage);

        var sectionPt = Page("projects/_index.pt-br.md");
        Assert.Equal("pt-br", sectionPt.Language.Code);
        Assert.False(sectionPt.IsDefaultLanguage);
    }

    /// <summary>Confirms that pages in an auto-generated section are linked to that section.</summary>
    [Fact]
    public void PagesInAutoGeneratedSectionShouldBeLinkedToSection()
    {
        var sectionEn = Page("projects/_index.md");
        Assert.NotEmpty(sectionEn.Pages);
        Assert.All(sectionEn.Pages, page => Assert.Equal("en", page.Language.Code));
    }

    /// <summary>Confirms that taxonomy terms are generated and linked per language.</summary>
    [Fact]
    public void TaxonomyTermsShouldBeGeneratedAndLinkedPerLanguage()
    {
        var helloEn = Page("posts/hello.md");
        var helloPt = Page("posts/hello.pt-br.md");

        Assert.Equal("en", Assert.Single(helloEn.Taxonomies["tags"]).Language.Code);
        Assert.Equal("pt-br", Assert.Single(helloPt.Taxonomies["tags"]).Language.Code);
        Assert.Equal("pt-br", Assert.Single(helloPt.Taxonomies["series"]).Language.Code);
        Assert.True(_site.OutputReferences.ContainsKey(
            new Uri("/pt-br/series/hello/index.html", UriKind.Relative)));
        Assert.Single(helloPt.Site.Taxonomies["series"]);
        Assert.All(helloPt.Site.Taxonomies["series"].Single().Pages,
            page => Assert.Equal("pt-br", page.Language.Code));
    }
}
