using Algolia.Search.Models.Search;
using Ekom.Algolia;
using Ekom.Algolia.Indexing;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaProductIndexExecutorTests
{
    [Fact]
    public void Configures_Product_Facets_Without_Distinct_When_Variants_Are_Disabled()
    {
        var options = new AlgoliaIndexingOptions
        {
            FacetAttributes = ["brand", "metafield:material"],
        };

        var attributes = AlgoliaProductIndexExecutor.BuildAttributesForFaceting(options);

        Assert.Equal(["attributes.brand", "attributes.material"], attributes);
    }

    [Fact]
    public void Configures_Facets_After_Distinct_When_Variants_Are_Enabled()
    {
        var options = new AlgoliaIndexingOptions
        {
            Variants = true,
            FacetAttributes = ["brand"],
            VariantFacetAttributes = new Dictionary<string, string>
            {
                ["color"] = "variantGroup:title",
                ["size"] = "variant:title",
            },
        };

        var attributes = AlgoliaProductIndexExecutor.BuildAttributesForFaceting(options);

        Assert.Equal(
            [
                "filterOnly(ProductId)",
                "filterOnly(categoryPageId)",
                "afterDistinct(attributes.brand)",
                "afterDistinct(attributes.color)",
                "afterDistinct(attributes.size)",
            ],
            attributes);
    }

    [Fact]
    public void Applies_Store_Language_Settings_To_Index_Settings()
    {
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            LanguageSettings = new AlgoliaLanguageSettingsOptions
            {
                QueryLanguages = ["is", "en"],
                IndexLanguages = ["is"],
                RemoveStopWords = true,
                IgnorePlurals = false,
            },
        };
        var settings = new IndexSettings();

        AlgoliaProductIndexExecutor.ApplyLanguageSettings(settings, store);

        Assert.Equal([SupportedLanguage.Is, SupportedLanguage.En], settings.QueryLanguages);
        Assert.Equal([SupportedLanguage.Is], settings.IndexLanguages);
        Assert.True(settings.RemoveStopWords!.AsBool());
        Assert.False(settings.IgnorePlurals!.AsBool());
    }

    [Fact]
    public void Rejects_Unsupported_Store_Language()
    {
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            LanguageSettings = new AlgoliaLanguageSettingsOptions
            {
                QueryLanguages = ["invalid"],
            },
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AlgoliaProductIndexExecutor.ApplyLanguageSettings(new IndexSettings(), store));

        Assert.Contains("invalid", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Store", exception.Message, StringComparison.Ordinal);
        Assert.Contains("QueryLanguages", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Detects_Explicitly_Disabled_Language_Processing_As_Configured()
    {
        var settings = new AlgoliaLanguageSettingsOptions
        {
            RemoveStopWords = false,
            IgnorePlurals = false,
        };

        Assert.True(AlgoliaProductIndexExecutor.HasLanguageSettings(settings));
    }
}
