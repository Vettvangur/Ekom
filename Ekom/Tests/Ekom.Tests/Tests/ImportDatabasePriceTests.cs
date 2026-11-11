using Ekom.API;
using Ekom.Models.Import;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Umb.Services;
using LinqToDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class ImportDatabasePriceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PriceOnlyChangesUpdateTableWithoutSavingContent(bool variant, bool customComparer)
    {
        using var fixture = new Fixture();
        var import = CreateImport(variant);
        if (customComparer) import.Comparer = "unchanged-content";
        fixture.SetExistingComparer(import, ignorePrice: true);
        PriceList(import)[0].Price = 75;

        fixture.Save(import);

        if (import is ImportProduct product) Assert.Null(product.Exception);
        Assert.Equal(75, Assert.Single(await Prices.Instance.GetAsync(fixture.Key)).Price);
        Assert.Empty(fixture.ContentService.Invocations);
        Assert.DoesNotContain(fixture.Content.Invocations, x => x.Method.Name == "SetValue");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherContentChangesStillSaveWhilePricesUseTable(bool variant)
    {
        using var fixture = new Fixture();
        var import = CreateImport(variant);
        fixture.SetExistingComparer(import, ignorePrice: true);
        import.NodeName = "Renamed product";
        PriceList(import)[0].Price = 75;

        fixture.Save(import);

        if (import is ImportProduct product) Assert.Null(product.Exception);
        fixture.ContentService.Verify(x => x.Save(fixture.Content.Object, -1, null), Times.Once);
        Assert.Equal(75, Assert.Single(await Prices.Instance.GetAsync(fixture.Key)).Price);
        Assert.DoesNotContain(fixture.Content.Invocations, x => x.Method.Name == "SetValue" && Equals(x.Arguments[0], "price"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewNodesHavePricesBeforeTheirFirstSave(bool variant)
    {
        using var fixture = new Fixture();
        fixture.Content.Object.Id = 0;
        var import = CreateImport(variant);
        fixture.ContentService.Setup(x => x.Save(fixture.Content.Object, -1, null))
            .Callback(() =>
            {
                Assert.Equal(50, Assert.Single(Prices.Instance.Get(fixture.Key)).Price);
                fixture.Content.Object.Id = 100;
            })
            .Returns(OperationResult.Succeed(new EventMessages()));

        fixture.Save(import, create: true);

        if (import is ImportProduct product) Assert.Null(product.Exception);
        fixture.ContentService.Verify(x => x.Save(fixture.Content.Object, -1, null), Times.Once);
        Assert.Equal(50, Assert.Single(await Prices.Instance.GetAsync(fixture.Key)).Price);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NodeModeKeepsPricesInContentAndInChangeDetection(bool variant)
    {
        using var fixture = new Fixture(enabled: false);
        var import = CreateImport(variant);
        fixture.SetExistingComparer(import, ignorePrice: false);
        PriceList(import)[0].Price = 75;

        fixture.Save(import);

        if (import is ImportProduct product) Assert.Null(product.Exception);
        fixture.ContentService.Verify(x => x.Save(fixture.Content.Object, -1, null), Times.Once);
        Assert.Contains(fixture.Content.Invocations, x => x.Method.Name == "SetValue" && Equals(x.Arguments[0], "price"));
        Assert.Empty(await Prices.Instance.GetAsync(fixture.Key));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidNewPricePreventsContentSaveAndPartialPriceWrites(bool variant)
    {
        using var fixture = new Fixture();
        fixture.Content.Object.Id = 0;
        var import = CreateImport(variant);
        PriceList(import).Add(new ImportPrice { StoreAlias = "Web", Currency = "en-GB", Price = -1 });
        var failure = Record.Exception(() => fixture.Save(import, create: true));
        Assert.True(failure != null || (import is ImportProduct product && product.Exception != null));
        Assert.Empty(Prices.Instance.Get(fixture.Key));
        Assert.Empty(fixture.ContentService.Invocations);
    }

    [Fact]
    public void BulkSyncReadsChunkPricesOnceAndSkipsUnchangedPriceTransactions()
    {
        using var fixture = new Fixture();
        var import = (ImportProduct)CreateImport(variant: false);
        fixture.SetExistingComparer(import, ignorePrice: true);
        fixture.Save(import);
        Assert.Equal(50, Assert.Single(Prices.Instance.Get(fixture.Key)).Price);

        // A write from elsewhere after the chunk was read is left alone until the next chunk read.
        fixture.RefreshChunk(import);
        fixture.WriteExternally(99);
        fixture.Save(import);
        Prices.Instance.FillCache();
        Assert.Equal(99, Assert.Single(Prices.Instance.Get(fixture.Key)).Price);

        // The next chunk read sees the difference and the import writes its price again.
        fixture.RefreshChunk(import);
        fixture.Save(import);
        Prices.Instance.FillCache();
        Assert.Equal(50, Assert.Single(Prices.Instance.Get(fixture.Key)).Price);

        // A changed price is always written, and invalid prices are still rejected.
        fixture.RefreshChunk(import);
        import.Price[0].Price = 60;
        fixture.Save(import);
        Assert.Equal(60, Assert.Single(Prices.Instance.Get(fixture.Key)).Price);
        import.Price[0].Price = -1;
        fixture.Save(import);
        Assert.NotNull(import.Exception);
        Assert.Empty(fixture.ContentService.Invocations);
    }

    private static ImportBase CreateImport(bool variant)
    {
        ImportBase import = variant
            ? new ImportVariant { Identifier = "erp-variant", NodeName = "Variant", Title = [] }
            : new ImportProduct { Identifier = "erp-product", NodeName = "Product", Title = [] };
        import.PreservePublishStatus = true;
        PriceList(import).Add(new ImportPrice { StoreAlias = "Web", Currency = "en-US", Price = 50 });
        return import;
    }

    private static List<ImportPrice> PriceList(ImportBase import)
        => import is ImportProduct product ? product.Price : ((ImportVariant)import).Price;

    private sealed class Fixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"ekom-import-prices-{Guid.NewGuid():N}.db");
        private readonly ConfigurationScope _scope;
        private readonly ImportService _service;
        public Mock<IContentService> ContentService { get; } = new();
        public Mock<IContent> Content { get; } = new();
        public Guid Key { get; } = Guid.NewGuid();

        public Fixture(bool enabled = true)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={_path};Pooling=False",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            var host = new Mock<IHostEnvironment>();
            host.SetupGet(x => x.ContentRootPath).Returns(Path.GetTempPath());
            var factory = new DatabaseFactory(config, host.Object);
            new DatabaseService(factory, NullLogger<DatabaseService>.Instance, new StockReservationReadiness()).EnsurePriceTable();
            _scope = new ConfigurationScope(overrides: new Dictionary<string, string?> { ["Ekom:UseDatabasePrices"] = enabled.ToString() },
                addServices: services =>
                {
                    services.AddSingleton(factory);
                    services.AddSingleton<Prices>();
                    services.AddMemoryCache();
                    var dataTypes = new Mock<IDataTypeService>();
                    dataTypes.Setup(x => x.GetByEditorAlias(It.IsAny<string>())).Returns([Mock.Of<IDataType>()]);
                    services.AddSingleton(dataTypes.Object);
                });
            Content.SetupProperty(x => x.Id, 100);
            Content.SetupGet(x => x.Key).Returns(Key);
            Content.SetupGet(x => x.ContentType).Returns(Mock.Of<ISimpleContentType>(x => x.Alias == "ekmProduct"));
            var propertyType = Mock.Of<IPropertyType>(x => x.PropertyEditorAlias == "Ekom.Property");
            var title = Mock.Of<IProperty>(x => x.Alias == "title" && x.PropertyType == propertyType);
            var description = Mock.Of<IProperty>(x => x.Alias == "description" && x.PropertyType == propertyType);
            Content.SetupGet(x => x.Properties).Returns(new PropertyCollection([title, description]));
            Content.Setup(x => x.HasProperty("price")).Returns(true);
            ContentService.Setup(x => x.Save(Content.Object, -1, null)).Returns(OperationResult.Succeed(new EventMessages()));
#if NET8_0
            _service = new ImportService(null!, ContentService.Object, null!, null!, null!,
                NullLogger<ImportService>.Instance, null!, null!, null!);
#else
            _service = new ImportService(null!, ContentService.Object, null!, null!, null!,
                NullLogger<ImportService>.Instance, null!, null!, null!, null!);
#endif
            foreach (var (name, value) in new (string, object)[]
            {
                ("productsSaved", new List<ImportProduct>()),
                ("variantsSaved", new List<ImportVariant>()),
            }) typeof(ImportService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_service, value);
        }

        public void SetExistingComparer(ImportBase import, bool ignorePrice)
        {
            var ignored = new List<string> { "Images", "EventProperties", "Files", "Stock" };
#if !NET8_0
            ignored.Add("WarehouseStock");
#endif
            if (import is ImportProduct) ignored.AddRange(["VariantGroups", "UpdateSlug"]);
            if (ignorePrice) ignored.Add("Price");
            var hash = import.Comparer ?? (string)Invoke("ComputeSha256Hash", [import, ignored.ToArray()])!;
            Content.Setup(x => x.GetValue<string>("comparer", null, null, false)).Returns(hash);
        }

        public void RefreshChunk(ImportProduct import)
            => Invoke("RefreshChunkPrices", [new List<ImportProduct> { import }, 0,
                new Dictionary<string, IContent> { [import.Identifier] = Content.Object }, new Dictionary<int, List<IContent>>()]);

        public void WriteExternally(decimal price)
        {
            using var db = Configuration.Resolver.GetRequiredService<DatabaseFactory>().GetDatabase();
            db.Prices.Where(row => row.NodeKey == Key).Set(row => row.Price, price).Update();
        }

        public void Save(ImportBase import, bool create = false)
        {
            if (import is ImportProduct)
                Invoke("SaveProduct", [Content.Object, import, null, null, null, create, -1, null, null, false]);
            else
                Invoke("SaveVariant", [Content.Object, import, null, null, create, -1, false, null, null, null]);
        }

        private object? Invoke(string method, object?[] args)
        {
            try { return typeof(ImportService).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_service, args); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        public void Dispose()
        {
            _scope.Dispose();
            System.IO.File.Delete(_path);
        }
    }
}
