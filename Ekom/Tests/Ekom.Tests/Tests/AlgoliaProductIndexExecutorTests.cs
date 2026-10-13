using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Ekom.Algolia;
using Ekom.Algolia.Indexing;
using Ekom.Algolia.Mappers;
using Ekom.Algolia.Models.Indexing;
using Ekom.Algolia.Services;
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
                IgnorePluralsLanguages = ["is"],
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
        Assert.Equal([SupportedLanguage.Is], settings.IgnorePlurals!.AsListSupportedLanguage());
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
                IgnorePlurals = true,
                IgnorePluralsLanguages = ["is"],
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
        Assert.Equal([SupportedLanguage.Is], settings.IgnorePlurals!.AsListSupportedLanguage());
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
    public void Applies_Ignore_Plurals_To_Configured_Languages()
    {
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            LanguageSettings = new AlgoliaLanguageSettingsOptions
            {
                IgnorePlurals = true,
                IgnorePluralsLanguages = ["is", "en"],
            },
        };
        var settings = new IndexSettings();

        AlgoliaProductIndexExecutor.ApplyLanguageSettings(settings, store);

        Assert.Equal(
            [SupportedLanguage.Is, SupportedLanguage.En],
            settings.IgnorePlurals!.AsListSupportedLanguage());
    }

    [Fact]
    public void Applies_Ignore_Plurals_Boolean_When_No_Languages_Are_Configured()
    {
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            LanguageSettings = new AlgoliaLanguageSettingsOptions
            {
                IgnorePlurals = true,
            },
        };
        var settings = new IndexSettings();

        AlgoliaProductIndexExecutor.ApplyLanguageSettings(settings, store);

        Assert.True(settings.IgnorePlurals!.AsBool());
    }

    [Fact]
    public void Disabled_Ignore_Plurals_Does_Not_Apply_Configured_Languages()
    {
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            LanguageSettings = new AlgoliaLanguageSettingsOptions
            {
                IgnorePlurals = false,
                IgnorePluralsLanguages = ["is"],
            },
        };
        var settings = new IndexSettings();

        AlgoliaProductIndexExecutor.ApplyLanguageSettings(settings, store);

        Assert.False(settings.IgnorePlurals!.AsBool());
    }

    [Fact]
    public void Omits_Ignore_Plurals_When_Toggle_Is_Not_Configured()
    {
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            LanguageSettings = new AlgoliaLanguageSettingsOptions
            {
                IgnorePluralsLanguages = ["is"],
            },
        };
        var settings = new IndexSettings();

        AlgoliaProductIndexExecutor.ApplyLanguageSettings(settings, store);

        Assert.Null(settings.IgnorePlurals);
        Assert.False(AlgoliaProductIndexExecutor.HasLanguageSettings(store.LanguageSettings));
    }

    [Fact]
    public void Rejects_Unsupported_Ignore_Plurals_Language_When_Enabled()
    {
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            LanguageSettings = new AlgoliaLanguageSettingsOptions
            {
                IgnorePlurals = true,
                IgnorePluralsLanguages = ["invalid"],
            },
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AlgoliaProductIndexExecutor.ApplyLanguageSettings(new IndexSettings(), store));

        Assert.Contains("invalid", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Store", exception.Message, StringComparison.Ordinal);
        Assert.Contains("IgnorePluralsLanguages", exception.Message, StringComparison.Ordinal);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deletes_Filter_Rejected_Products_Even_When_No_Records_Are_Saved(bool variants)
    {
        var client = new Mock<ISearchClient>();
        client.Setup(x => x.DeleteByAsync(
                It.IsAny<string>(), It.IsAny<DeleteByParams>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdatedAtResponse { TaskID = 123 });
        var mapper = new Mock<IAlgoliaProductIndexMapper>();
        var versions = new AlgoliaSearchCacheVersionProvider();
        var executor = CreateExecutor(client.Object, mapper.Object, [new ProductIndexFilter(false)], versions);
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            Indexing = new AlgoliaIndexingOptions { Variants = variants, BatchSize = 100 },
        };
        var product = CreateProduct();
        using var cts = new CancellationTokenSource();

        await executor.UpsertProductsAsync(store, [product], cts.Token);

        var indexName = CreateIndexName(store);
        if (variants)
        {
            var deletion = Assert.Single(client.Invocations, x => x.Method.Name == "DeleteByAsync");
            Assert.Equal(indexName, deletion.Arguments[0]);
            Assert.Equal($"ProductId:{product.Key}", Assert.IsType<DeleteByParams>(deletion.Arguments[1]).Filters);
            Assert.Equal(cts.Token, deletion.Arguments[3]);
            Assert.Single(client.Invocations, x => x.Method.Name == "WaitForTaskAsync");
            Assert.DoesNotContain(client.Invocations, x => x.Method.Name == "DeleteObjectsAsync");
        }
        else
        {
            var deletion = Assert.Single(client.Invocations, x => x.Method.Name == "DeleteObjectsAsync");
            Assert.Equal(indexName, deletion.Arguments[0]);
            Assert.Equal([product.Key.ToString()], Assert.IsAssignableFrom<IEnumerable<string>>(deletion.Arguments[1]));
            Assert.Equal(true, deletion.Arguments[2]);
            Assert.Equal(100, deletion.Arguments[3]);
            Assert.Equal(cts.Token, deletion.Arguments[5]);
            Assert.DoesNotContain(client.Invocations, x => x.Method.Name == "DeleteByAsync");
        }
        Assert.DoesNotContain(client.Invocations, x => x.Method.Name.StartsWith("SaveObjects", StringComparison.Ordinal));
        Assert.Empty(mapper.Invocations);
        Assert.Equal(1, versions.GetVersion(store.Alias));
    }

    [Fact]
    public async Task Deletes_Rejected_Products_And_Saves_Accepted_Products_In_The_Same_Batch()
    {
        var client = new Mock<ISearchClient>();
        var mapper = new Mock<IAlgoliaProductIndexMapper>();
        var accepted = CreateProduct();
        var rejected = CreateProduct();
        var record = new AlgoliaProductRecord { ObjectId = accepted.Key.ToString(), Title = "Accepted" };
        mapper.Setup(x => x.MapRecords(accepted, It.IsAny<AlgoliaResolvedStore>(), It.IsAny<string>()))
            .Returns([record]);
        var filter = new Mock<IAlgoliaProductIndexFilter>();
        filter.Setup(x => x.ShouldIndex(accepted, It.IsAny<AlgoliaResolvedStore>())).Returns(true);
        var executor = CreateExecutor(client.Object, mapper.Object, [filter.Object], new AlgoliaSearchCacheVersionProvider());

        await executor.UpsertProductsAsync(new AlgoliaResolvedStore { Alias = "Store" }, [accepted, rejected], CancellationToken.None);

        var deletion = Assert.Single(client.Invocations, x => x.Method.Name == "DeleteObjectsAsync");
        Assert.Equal([rejected.Key.ToString()], Assert.IsAssignableFrom<IEnumerable<string>>(deletion.Arguments[1]));
        var save = Assert.Single(client.Invocations, x => x.Method.Name == "SaveObjectsAsync");
        Assert.Equal([record], Assert.IsAssignableFrom<IEnumerable<AlgoliaProductRecord>>(save.Arguments[1]));
        mapper.Verify(x => x.MapRecords(rejected, It.IsAny<AlgoliaResolvedStore>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Deletes_Only_From_The_Locale_And_Currency_Target_That_Rejects_The_Product()
    {
        var client = new Mock<ISearchClient>();
        var mapper = new Mock<IAlgoliaProductIndexMapper>();
        var product = CreateProduct();
        mapper.Setup(x => x.MapRecords(product, It.IsAny<AlgoliaResolvedStore>(), It.IsAny<string>()))
            .Returns([new AlgoliaProductRecord { ObjectId = product.Key.ToString(), Title = "Product" }]);
        var filter = new Mock<IAlgoliaProductIndexFilter>();
        filter.Setup(x => x.ShouldIndex(product, It.IsAny<AlgoliaResolvedStore>()))
            .Returns((IProduct _, AlgoliaResolvedStore target) => target.Locale != "is-IS" || target.Currency != "ISK");
        var store = new AlgoliaResolvedStore
        {
            Alias = "Store",
            Locales = ["is-IS", "en-US"],
            Currencies = ["ISK", "USD"],
        };
        var executor = CreateExecutor(client.Object, mapper.Object, [filter.Object], new AlgoliaSearchCacheVersionProvider());

        await executor.UpsertProductsAsync(store, [product], CancellationToken.None);

        var rejectedIndex = CreateIndexName(store.WithSelection("is-IS", "ISK"));
        var deletion = Assert.Single(client.Invocations, x => x.Method.Name == "DeleteObjectsAsync");
        Assert.Equal(rejectedIndex, deletion.Arguments[0]);
        var saves = client.Invocations.Where(x => x.Method.Name == "SaveObjectsAsync").ToList();
        Assert.Equal(3, saves.Count);
        Assert.DoesNotContain(saves, x => Equals(x.Arguments[0], rejectedIndex));
    }

    [Fact]
    public async Task Does_Not_Delete_Accepted_Products_When_The_Mapper_Returns_No_Records()
    {
        var client = new Mock<ISearchClient>();
        var mapper = new Mock<IAlgoliaProductIndexMapper>();
        mapper.Setup(x => x.MapRecords(It.IsAny<IProduct>(), It.IsAny<AlgoliaResolvedStore>(), It.IsAny<string>()))
            .Returns(Array.Empty<AlgoliaProductRecord>());
        var executor = CreateExecutor(client.Object, mapper.Object, [new ProductIndexFilter(true)], new AlgoliaSearchCacheVersionProvider());

        await executor.UpsertProductsAsync(new AlgoliaResolvedStore { Alias = "Store" }, [CreateProduct()], CancellationToken.None);

        Assert.DoesNotContain(client.Invocations, x => x.Method.Name is "DeleteObjectsAsync" or "DeleteByAsync" or "SaveObjectsAsync");
    }

    [Fact]
    public async Task Propagates_Filter_Rejection_Delete_Failures_Without_Saving()
    {
        var client = new Mock<ISearchClient>();
        var failure = new InvalidOperationException("Delete failed");
        client.Setup(x => x.DeleteObjectsAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), true, It.IsAny<int>(), null, CancellationToken.None, null))
            .ThrowsAsync(failure);
        var executor = CreateExecutor(
            client.Object, Mock.Of<IAlgoliaProductIndexMapper>(), [new ProductIndexFilter(false)], new AlgoliaSearchCacheVersionProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.UpsertProductsAsync(new AlgoliaResolvedStore { Alias = "Store" }, [CreateProduct()], CancellationToken.None));

        Assert.Same(failure, exception);
        Assert.DoesNotContain(client.Invocations, x => x.Method.Name.StartsWith("SaveObjects", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancelled_Upserts_Do_Not_Submit_Deletions_Or_Saves()
    {
        var client = new Mock<ISearchClient>();
        var executor = CreateExecutor(
            client.Object, Mock.Of<IAlgoliaProductIndexMapper>(), [new ProductIndexFilter(false)], new AlgoliaSearchCacheVersionProvider());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.UpsertProductsAsync(new AlgoliaResolvedStore { Alias = "Store" }, [CreateProduct()], cts.Token));

        Assert.Empty(client.Invocations);
    }

    private static IProduct CreateProduct()
    {
        var product = new Mock<IProduct>();
        product.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        return product.Object;
    }

    private static AlgoliaProductIndexExecutor CreateExecutor(
        ISearchClient client,
        IAlgoliaProductIndexMapper mapper,
        IEnumerable<IAlgoliaProductIndexFilter> filters,
        AlgoliaSearchCacheVersionProvider versions)
    {
        var options = CreateOptions();
        var transformationWrites = new AlgoliaTransformationWriteService(
            client, options, NullLogger<AlgoliaTransformationWriteService>.Instance);
        return new AlgoliaProductIndexExecutor(
            client,
            new AlgoliaIndexReplacementService(client, transformationWrites, options, NullLogger<AlgoliaIndexReplacementService>.Instance),
            transformationWrites,
            options,
            new AlgoliaStoreResolver(options, Mock.Of<IServiceProvider>(), NullLogger<AlgoliaStoreResolver>.Instance),
            new IndexNameBuilder(options),
            new NoOpQuerySuggestionsConfigurator(),
            versions,
            mapper,
            new EmptyFacetAttributeSelector(),
            filters,
            NullLogger<AlgoliaProductIndexExecutor>.Instance);
    }

    private static string CreateIndexName(AlgoliaResolvedStore store)
        => new IndexNameBuilder(CreateOptions()).BuildPrimary("products", store);

    private static IOptions<AlgoliaOptions> CreateOptions()
        => Options.Create(new AlgoliaOptions
        {
            ApplicationId = "app-id",
            AdminApiKey = "admin-key",
            SearchApiKey = "search-key",
        });

    private sealed class NoOpQuerySuggestionsConfigurator : IAlgoliaQuerySuggestionsConfigurator
    {
        public Task EnsureConfiguredAsync(AlgoliaResolvedStore store, string primaryIndexName, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class EmptyFacetAttributeSelector : IAlgoliaFacetAttributeSelector
    {
        public IReadOnlyCollection<string> GetFacetAttributes(AlgoliaIndexingOptions indexing)
            => Array.Empty<string>();
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
