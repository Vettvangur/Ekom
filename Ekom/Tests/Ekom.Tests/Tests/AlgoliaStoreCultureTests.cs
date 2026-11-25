using Ekom.Algolia;
using Ekom.Algolia.Indexing;
using Ekom.Algolia.Services;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaStoreCultureTests
{
    private const string Icelandic = "is-IS";
    private const string English = "en-US";

    [Fact]
    public void Invariant_Content_Is_Only_Indexed_For_Store_Cultures()
    {
        var content = CreateInvariantContent(published: true);

        var (upserts, deletes) = AlgoliaContentIndexExecutor.BuildPerCultureBuckets([content], [Icelandic], storeCulturesOnly: true);

        Assert.Equal([Icelandic], upserts.Keys);
        Assert.Empty(deletes);
    }

    [Fact]
    public void Unpublished_Invariant_Content_Is_Only_Deleted_From_Store_Cultures()
    {
        var content = CreateInvariantContent(published: false);

        var (upserts, deletes) = AlgoliaContentIndexExecutor.BuildPerCultureBuckets([content], [Icelandic], storeCulturesOnly: true);

        Assert.Empty(upserts);
        Assert.Equal([Icelandic], deletes.Keys);
    }

    [Fact]
    public void Variant_Content_Skips_Cultures_The_Store_Does_Not_Have()
    {
        var content = CreateVariantContent(publishedCultures: [Icelandic, English], unpublishedCultures: []);

        var (upserts, deletes) = AlgoliaContentIndexExecutor.BuildPerCultureBuckets([content], [Icelandic], storeCulturesOnly: true);

        Assert.Equal([Icelandic], upserts.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(deletes);
    }

    [Fact]
    public void Unpublished_Variant_Cultures_Outside_The_Store_Are_Not_Deleted()
    {
        var content = CreateVariantContent(publishedCultures: [], unpublishedCultures: [Icelandic, English]);

        var (upserts, deletes) = AlgoliaContentIndexExecutor.BuildPerCultureBuckets([content], [Icelandic], storeCulturesOnly: true);

        Assert.Empty(upserts);
        Assert.Equal([Icelandic], deletes.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Content_Without_A_Store_Keeps_Using_Every_Language()
    {
        var invariant = CreateInvariantContent(published: true);
        var variant = CreateVariantContent(publishedCultures: [English], unpublishedCultures: []);

        var (invariantUpserts, _) = AlgoliaContentIndexExecutor.BuildPerCultureBuckets([invariant], [Icelandic, English]);
        var (variantUpserts, _) = AlgoliaContentIndexExecutor.BuildPerCultureBuckets([variant], [Icelandic]);

        Assert.Equal([Icelandic, English], invariantUpserts.Keys);
        Assert.Equal([English], variantUpserts.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(English)]
    [InlineData("en")]
    [InlineData("da-DK")]
    public void Unsupported_Locale_Falls_Back_To_The_Store_Locale(string requestedLocale)
    {
        var store = CreateIcelandicStore();

        Assert.Equal(Icelandic, store.ResolveLocale(requestedLocale));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("is-is")]
    [InlineData(" IS-IS ")]
    public void Supported_Or_Missing_Locale_Resolves_To_The_Store_Locale(string? requestedLocale)
    {
        var store = CreateIcelandicStore();

        Assert.Equal(Icelandic, store.ResolveLocale(requestedLocale));
    }

    [Fact]
    public void Requested_Locale_Is_Kept_When_Store_Cultures_Are_Unknown()
    {
        var store = new AlgoliaResolvedStore { Alias = "Stod" };

        Assert.Equal(English, store.ResolveLocale(English));
    }

    [Fact]
    public void Requested_Locale_Is_Used_When_The_Store_Supports_It()
    {
        var store = new AlgoliaResolvedStore
        {
            Alias = "Stod",
            Locale = Icelandic,
            Locales = [Icelandic, English],
        };

        Assert.Equal(English, store.ResolveLocale("en-us"));
    }

    [Fact]
    public void Icelandic_Store_Never_Resolves_English_Index_Names()
    {
        var builder = new IndexNameBuilder(Options.Create(new AlgoliaOptions
        {
            ApplicationId = "app",
            AdminApiKey = "admin",
            SearchApiKey = "search",
            Environment = "dev",
        }));
        var store = CreateIcelandicStore();

        var indexed = store.ExpandIndexTargets()
            .Select(target => builder.BuildPrimary("products", target))
            .ToList();
        var searchedProducts = builder.BuildPrimary(
            "products",
            store.WithSelection(store.ResolveLocale(English), store.Currency));
        var searchedCategories = builder.BuildPrimary(
            "categories",
            store.WithSelection(store.ResolveLocale(English), currency: null),
            currencyOverride: string.Empty);
        var tracked = builder.Build(
            AlgoliaIndexKind.Primary,
            "products",
            store,
            replica: null,
            localeOverride: store.ResolveLocale(English),
            currencyOverride: store.Currency);

        Assert.Equal(["primary.dev.stod.products.is-is.is-is"], indexed);
        Assert.Equal("primary.dev.stod.products.is-is.is-is", searchedProducts);
        Assert.Equal("primary.dev.stod.categories.is-is", searchedCategories);
        Assert.Equal("primary.dev.stod.products.is-is.is-is", tracked);
    }

    [Theory]
    [InlineData(English)]
    [InlineData("")]
    [InlineData(null)]
    public void Default_Locale_Must_Be_One_Of_The_Store_Cultures(string? storeLocale)
    {
        Assert.Equal(Icelandic, AlgoliaStoreResolver.ResolveDefaultLocale(storeLocale, [Icelandic]));
    }

    [Fact]
    public void Default_Locale_Is_Kept_When_Store_Cultures_Are_Unknown()
    {
        Assert.Null(AlgoliaStoreResolver.ResolveDefaultLocale(null, []));
        Assert.Equal(English, AlgoliaStoreResolver.ResolveDefaultLocale(English, []));
    }

    private static AlgoliaResolvedStore CreateIcelandicStore()
        => new()
        {
            Alias = "Stod",
            Locale = Icelandic,
            Currency = Icelandic,
            Locales = [Icelandic],
            Currencies = [Icelandic],
        };

    private static IContent CreateInvariantContent(bool published)
    {
        var content = CreateContent(ContentVariation.Nothing);
        content.SetupGet(x => x.Published).Returns(published);
        return content.Object;
    }

    // Umbraco 13 stores culture codes in lower case, so variant assertions ignore casing.
    private static IContent CreateVariantContent(string[] publishedCultures, string[] unpublishedCultures)
    {
        var cultureInfos = new ContentCultureInfosCollection();
        foreach (var culture in publishedCultures.Concat(unpublishedCultures))
            cultureInfos.AddOrUpdate(culture, culture, DateTime.UtcNow);

        var content = CreateContent(ContentVariation.Culture);
        content.SetupGet(x => x.CultureInfos).Returns(cultureInfos);
        content.Setup(x => x.IsCulturePublished(It.IsAny<string>()))
            .Returns((string culture) => publishedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase));
        return content.Object;
    }

    private static Mock<IContent> CreateContent(ContentVariation variations)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(x => x.Variations).Returns(variations);

        var content = new Mock<IContent>();
        content.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        content.SetupGet(x => x.ContentType).Returns(contentType.Object);
        return content;
    }
}
