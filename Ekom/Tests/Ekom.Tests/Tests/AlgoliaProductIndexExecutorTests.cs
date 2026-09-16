using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Ekom.Algolia;
using Ekom.Algolia.Indexing;
using Ekom.Algolia.Mappers;
using Ekom.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaProductIndexExecutorTests
{
    [Fact]
    public void Sorted_Replica_Type_Defaults_To_Virtual()
    {
        var replica = new AlgoliaSortedReplicaOptions { Attribute = "price" };

        Assert.Equal(AlgoliaReplicaType.Virtual, replica.Type);
    }

    [Theory]
    [InlineData(AlgoliaReplicaType.Virtual, "virtual(replica.products.price)")]
    [InlineData(AlgoliaReplicaType.Standard, "replica.products.price")]
    public void Formats_Replica_Reference_For_Configured_Type(AlgoliaReplicaType type, string expected)
    {
        var replica = new AlgoliaSortedReplicaOptions
        {
            Attribute = "price",
            Type = type,
        };

        var reference = AlgoliaProductIndexExecutor.BuildReplicaReference("replica.products.price", replica);

        Assert.Equal(expected, reference);
    }

    [Fact]
    public void Configures_Virtual_Replica_With_Custom_Ranking_And_Supported_Language_Settings()
    {
        var replica = new AlgoliaSortedReplicaOptions
        {
            Attribute = "price",
            Direction = AlgoliaSortDirection.Desc,
        };
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            LanguageSettings = new AlgoliaLanguageSettingsOptions
            {
                QueryLanguages = ["is"],
                IndexLanguages = ["is"],
                RemoveStopWords = true,
                IgnorePlurals = true,
            },
        };

        var settings = AlgoliaProductIndexExecutor.BuildReplicaSettings(
            replica,
            variants: true,
            attributesForFaceting: ["filterOnly(categoryPageId)"],
            store: store,
            searchableAttributes: ["Title", "Sku"]);

        Assert.Equal(["desc(price)"], settings.CustomRanking);
        Assert.Null(settings.Ranking);
        Assert.Null(settings.AttributeForDistinct);
        Assert.Null(settings.AttributesForFaceting);
        Assert.Null(settings.SearchableAttributes);
        Assert.Equal([SupportedLanguage.Is], settings.QueryLanguages);
        Assert.Null(settings.IndexLanguages);
        Assert.True(settings.RemoveStopWords!.AsBool());
        Assert.True(settings.IgnorePlurals!.AsBool());
    }

    [Fact]
    public void Configures_Standard_Replica_With_Exhaustive_Ranking_And_Index_Settings()
    {
        var replica = new AlgoliaSortedReplicaOptions
        {
            Attribute = "price",
            Direction = AlgoliaSortDirection.Asc,
            Type = AlgoliaReplicaType.Standard,
        };
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            LanguageSettings = new AlgoliaLanguageSettingsOptions
            {
                QueryLanguages = ["is"],
                IndexLanguages = ["is"],
            },
        };

        var settings = AlgoliaProductIndexExecutor.BuildReplicaSettings(
            replica,
            variants: true,
            attributesForFaceting: ["filterOnly(categoryPageId)"],
            store: store,
            searchableAttributes: ["Title", "Sku"]);

        Assert.Equal("asc(price)", settings.Ranking![0]);
        Assert.Null(settings.CustomRanking);
        Assert.Equal("ProductId", settings.AttributeForDistinct);
        Assert.Equal(["filterOnly(categoryPageId)"], settings.AttributesForFaceting);
        Assert.Equal(["Title", "Sku"], settings.SearchableAttributes);
        Assert.Equal([SupportedLanguage.Is], settings.QueryLanguages);
        Assert.Equal([SupportedLanguage.Is], settings.IndexLanguages);
    }

    [Fact]
    public void Normalizes_Configured_Searchable_Attributes_Without_Changing_Order()
    {
        var attributes = AlgoliaProductIndexExecutor.BuildSearchableAttributes(
            [" Title ", "", "Sku", "Title", "  ", "unordered(Summary)"]);

        Assert.Equal(["Title", "Sku", "unordered(Summary)"], attributes);
    }

    [Theory]
    [InlineData()]
    [InlineData("", " ")]
    public void Omits_Searchable_Attributes_When_No_Values_Are_Configured(params string[] attributes)
    {
        var result = AlgoliaProductIndexExecutor.BuildSearchableAttributes(attributes);

        Assert.Null(result);
    }

    [Fact]
    public void Carries_Searchable_Attributes_To_Each_Store_Index_Target()
    {
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            Indexing = new AlgoliaIndexingOptions { BatchSize = 250 },
            HasIndexingOverride = true,
            SearchableAttributes = ["Title", "Sku"],
            Collections = new AlgoliaCollectionsOptions { Enabled = true },
        };

        var target = store.WithSelection("is-IS", "ISK");

        Assert.Equal(["Title", "Sku"], target.SearchableAttributes);
        Assert.True(target.Collections.Enabled);
        Assert.Equal(250, target.Indexing.BatchSize);
        Assert.True(target.HasIndexingOverride);
    }

    [Fact]
    public void Adds_Collections_Facet_When_Not_Configured()
    {
        var attributes = new List<string> { "filterOnly(ProductId)" };

        AlgoliaProductIndexExecutor.EnsureCollectionsFacet(attributes);

        Assert.Equal(["filterOnly(ProductId)", "_collections"], attributes);
    }

    [Theory]
    [InlineData("_collections")]
    [InlineData("filterOnly(_collections)")]
    [InlineData("afterDistinct(searchable(_collections))")]
    public void Does_Not_Duplicate_Configured_Collections_Facet(string configured)
    {
        var attributes = new List<string> { configured };

        AlgoliaProductIndexExecutor.EnsureCollectionsFacet(attributes);

        Assert.Equal([configured], attributes);
    }

    [Fact]
    public void Adds_Exact_Collections_Facet_When_Configured_Casing_Differs()
    {
        var attributes = new List<string> { "_Collections" };

        AlgoliaProductIndexExecutor.EnsureCollectionsFacet(attributes);

        Assert.Equal(["_Collections", "_collections"], attributes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Uses_Configured_Product_Write_Path(bool useTransformation)
    {
        var client = new Mock<ISearchClient>();
        var transformationWrites = new AlgoliaTransformationWriteService(
            client.Object,
            Options.Create(new AlgoliaOptions
            {
                ApplicationId = "app-id",
                AdminApiKey = "admin-key",
                SearchApiKey = "search-key",
            }),
            NullLogger<AlgoliaTransformationWriteService>.Instance);
        var records = new[] { new TestRecord() };
        using var cts = new CancellationTokenSource();

        await AlgoliaProductIndexExecutor.SaveProductRecordsAsync(
            client.Object,
            transformationWrites,
            "products",
            records,
            100,
            useTransformation,
            cts.Token);

        client.Verify(
            x => x.SaveObjectsWithTransformationAsync(
                "products",
                It.Is<IEnumerable<object>>(objects => objects.Single() is TestRecord),
                true,
                100,
                null,
                cts.Token,
                null),
            useTransformation ? Times.Once : Times.Never);
        client.Verify(
            x => x.SaveObjectsAsync(
                "products",
                records,
                true,
                100,
                null,
                cts.Token,
                null),
            useTransformation ? Times.Never : Times.Once);
    }

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
    public void Configures_Top_Level_Facets_When_Variants_Are_Disabled()
    {
        var options = new AlgoliaIndexingOptions
        {
            AttributesForFaceting = ["filterOnly(categoryPageId)"],
        };

        var attributes = AlgoliaProductIndexExecutor.BuildAttributesForFaceting(options);

        Assert.Equal(["filterOnly(categoryPageId)"], attributes);
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
    public void Merges_Configured_And_Generated_Facet_Expressions()
    {
        var options = new AlgoliaIndexingOptions
        {
            Variants = true,
            AttributesForFaceting =
            [
                " filterOnly(categoryPageId) ",
                "searchable(brand)",
                "FILTERONLY(CATEGORYPAGEID)",
                " ",
            ],
            FacetAttributes = ["brand"],
        };

        var attributes = AlgoliaProductIndexExecutor.BuildAttributesForFaceting(options);

        Assert.Equal(
            [
                "filterOnly(categoryPageId)",
                "searchable(brand)",
                "filterOnly(ProductId)",
                "afterDistinct(attributes.brand)",
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

    [Fact]
    public void Indexes_Product_When_All_Filters_Accept_It()
    {
        var product = new Mock<IProduct>().Object;
        var store = new AlgoliaResolvedStore { Alias = "Store" };
        var filters = new IAlgoliaProductIndexFilter[]
        {
            new ProductIndexFilter(true),
            new ProductIndexFilter(true),
        };

        var result = AlgoliaProductIndexExecutor.ShouldIndex(product, store, filters);

        Assert.True(result);
    }

    [Fact]
    public void Indexes_Product_When_No_Filters_Are_Registered()
    {
        var product = new Mock<IProduct>().Object;
        var store = new AlgoliaResolvedStore { Alias = "Store" };

        var result = AlgoliaProductIndexExecutor.ShouldIndex(product, store, []);

        Assert.True(result);
    }

    [Fact]
    public void Does_Not_Index_Product_When_A_Filter_Rejects_It()
    {
        var product = new Mock<IProduct>().Object;
        var store = new AlgoliaResolvedStore { Alias = "Store" };
        var filters = new IAlgoliaProductIndexFilter[]
        {
            new ProductIndexFilter(true),
            new ProductIndexFilter(false),
        };

        var result = AlgoliaProductIndexExecutor.ShouldIndex(product, store, filters);

        Assert.False(result);
    }

    private sealed class ProductIndexFilter(bool shouldIndex) : IAlgoliaProductIndexFilter
    {
        public bool ShouldIndex(IProduct product, AlgoliaResolvedStore store)
            => shouldIndex;
    }

    private sealed class TestRecord
    {
    }
}
