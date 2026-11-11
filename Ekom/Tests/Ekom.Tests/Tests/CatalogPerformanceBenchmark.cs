using Ekom.API;
using Ekom.Cache;
using Ekom.Events;
using Ekom.Factories;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Ekom.Tests.Tests;

/// <summary>
/// Manual benchmark for cache fill and product responses over a synthetic catalog.
/// Skipped unless EKOM_BENCH=1. Sizes: EKOM_BENCH_PRODUCTS, EKOM_BENCH_VARIANTS, EKOM_BENCH_STORES.
/// Umbraco itself is replaced by in-memory fakes, so only Ekom.Core work is measured.
/// </summary>
[Trait("Category", "Benchmark")]
public class CatalogPerformanceBenchmark
{
    private readonly ITestOutputHelper _output;
    private readonly StringBuilder _report = new();

    public CatalogPerformanceBenchmark(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Measure()
    {
        if (Environment.GetEnvironmentVariable("EKOM_BENCH") != "1")
            return;

        var productCount = EnvInt("EKOM_BENCH_PRODUCTS", 5000);
        var variantsPerProduct = EnvInt("EKOM_BENCH_VARIANTS", 3);
        var storeCount = EnvInt("EKOM_BENCH_STORES", 2);

        var stores = Enumerable.Range(0, storeCount)
            .Select(i => new BenchStore(i == 0 ? "IS" : "S" + i, 100 + i))
            .ToList();
        var tree = new SyntheticTree(productCount, variantsPerProduct, stores.Select(x => x.Alias).ToArray());
        using var memory = new MemoryCache(new MemoryCacheOptions());
        PriceCache.SetCache(memory);

        using var scope = new ConfigurationScope(addServices: services =>
        {
            services.AddLogging();
            services.AddSingleton<IMemoryCache>(memory);
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<INodeService>(tree);
            services.AddSingleton<IUrlService, BenchUrlService>();
            services.AddSingleton<DiscountEvents>();
            services.AddSingleton(Mock.Of<IStoreDomainCache>());
            services.AddSingleton<IBaseCache<IStore>>(sp => new StoreCache(
                sp.GetRequiredService<Configuration>(),
                sp.GetRequiredService<ILogger<BaseCache<IStore>>>(), null!, sp));
            services.AddSingleton<IBaseCache<StockData>>(sp => new StockCache(
                sp.GetRequiredService<Configuration>(),
                sp.GetRequiredService<ILogger<BaseCache<StockData>>>(), null!, sp));
            services.AddSingleton<IPerStoreFactory<ICategory>, CategoryFactory>();
            services.AddSingleton<IPerStoreFactory<IProduct>, ProductFactory>();
            services.AddSingleton<IPerStoreFactory<IVariant>, VariantFactory>();
            services.AddSingleton<IPerStoreFactory<IVariantGroup>, VariantGroupFactory>();
            services.AddSingleton<IPerStoreFactory<IProductDiscount>, ProductDiscountFactory>();
            services.AddSingleton<IPerStoreIndexedCache<ICategory>, CategoryCache>();
            services.AddSingleton<IPerStoreIndexedCache<IProduct>, ProductCache>();
            services.AddSingleton<IPerStoreIndexedCache<IVariant>, VariantCache>();
            services.AddSingleton<IPerStoreIndexedCache<IVariantGroup>, VariantGroupCache>();
            services.AddSingleton<IPerStoreCache<IProductDiscount>, ProductDiscountCache>();
            services.AddTransient<IStoreService, StoreService>();
            services.AddTransient<IProductFilterService, ProductFilterService>();
            services.AddTransient<IMetafieldService, EkomCore.Services.MetafieldService>();
            services.AddTransient(sp => new ProductDiscountService(
                sp.GetRequiredService<IPerStoreCache<IProductDiscount>>(),
                sp.GetRequiredService<DiscountEvents>()));
            services.AddTransient(sp => new Stock(
                sp.GetRequiredService<Configuration>(),
                sp.GetRequiredService<ILogger<Stock>>(),
                sp.GetRequiredService<IBaseCache<StockData>>(),
                null!, null!, sp.GetRequiredService<IStoreService>(), null!, null!, null!));
            services.AddTransient(sp => new Catalog(
                sp.GetRequiredService<ILogger<Catalog>>(),
                sp.GetRequiredService<Configuration>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IPerStoreIndexedCache<IProduct>>(),
                sp.GetRequiredService<IPerStoreIndexedCache<ICategory>>(),
                sp.GetRequiredService<IPerStoreCache<IProductDiscount>>(),
                sp.GetRequiredService<IPerStoreIndexedCache<IVariant>>(),
                sp.GetRequiredService<IPerStoreIndexedCache<IVariantGroup>>(),
                sp.GetRequiredService<IStoreService>(),
                sp.GetRequiredService<IHttpContextAccessor>(),
                sp.GetRequiredService<IProductFilterService>()));
        });

        var sp = Configuration.Resolver;
        var config = scope.Instance;
        var storeCache = (StoreCache)sp.GetRequiredService<IBaseCache<IStore>>();
        foreach (var store in stores)
            storeCache[store.Key] = store;

        var caches = new (string Name, ICache Cache)[]
        {
            ("category", (ICache)sp.GetRequiredService<IPerStoreIndexedCache<ICategory>>()),
            ("productDiscount", (ICache)sp.GetRequiredService<IPerStoreCache<IProductDiscount>>()),
            ("product", (ICache)sp.GetRequiredService<IPerStoreIndexedCache<IProduct>>()),
            ("variant", (ICache)sp.GetRequiredService<IPerStoreIndexedCache<IVariant>>()),
            ("variantGroup", (ICache)sp.GetRequiredService<IPerStoreIndexedCache<IVariantGroup>>()),
        };

        Line($"# Ekom benchmark  {DateTime.Now:yyyy-MM-dd HH:mm}  net{Environment.Version.Major}  cores={Environment.ProcessorCount}");
        Line($"catalog: {tree.CategoryCount} categories, {productCount} products, {productCount} variant groups, {productCount * variantsPerProduct} variants, {storeCount} stores");
        Line("");
        Line("## Cache fill (ms per full rebuild)");

        const int fillRuns = 9;
        var fillTimes = caches.ToDictionary(x => x.Name, _ => new List<double>());
        var totals = new List<double>();
        var fillAlloc = 0L;
        for (var run = 0; run < fillRuns; run++)
        {
            var allocBefore = GC.GetTotalAllocatedBytes(true);
            var total = Stopwatch.StartNew();
            using (var rebuild = config.CacheRebuild.Begin())
            using (CacheInitializationScope.Begin())
            using (PriceCache.BeginBulkInvalidation())
            {
                foreach (var (name, cache) in caches)
                {
                    ((IClearableCache)cache).ClearCache();
                }

                // The rebuild context starts empty, so stores are seeded inside it like StoreCache.FillCache does.
                storeCache.ClearCache();
                foreach (var store in stores)
                    storeCache[store.Key] = store;

                foreach (var (name, cache) in caches)
                {
                    var sw = Stopwatch.StartNew();
                    cache.FillCache();
                    fillTimes[name].Add(sw.Elapsed.TotalMilliseconds);
                }

                rebuild.Commit();
            }

            PriceCache.InvalidateAll();
            totals.Add(total.Elapsed.TotalMilliseconds);
            fillAlloc = GC.GetTotalAllocatedBytes(true) - allocBefore;
        }

        foreach (var (name, _) in caches)
            Line($"{name,-18} {Median(fillTimes[name]),10:F1}   (runs: {string.Join(", ", fillTimes[name].Select(x => x.ToString("F0", CultureInfo.InvariantCulture)))})");
        Line($"{"TOTAL",-18} {Median(totals),10:F1}   min {totals.Min():F1}   alloc last run: {fillAlloc / 1024 / 1024} MB");

        // Same fill without the isolated rebuild context, to separate coordinator overhead from mapping cost.
        var liveTimes = new List<string>();
        var gc0 = GC.CollectionCount(0);
        var gc2 = GC.CollectionCount(2);
        var pause = GC.GetTotalPauseDuration();
        var live = Stopwatch.StartNew();
        using (CacheInitializationScope.Begin())
        using (PriceCache.BeginBulkInvalidation())
        {
            foreach (var (name, cache) in caches)
            {
                var sw = Stopwatch.StartNew();
                cache.FillCache();
                liveTimes.Add($"{name} {sw.Elapsed.TotalMilliseconds:F0}");
            }
        }

        Line($"live fill (no rebuild context): {live.Elapsed.TotalMilliseconds:F1} ms  [{string.Join(", ", liveTimes)}]");
        Line($"  GC during live fill: gen0 {GC.CollectionCount(0) - gc0}, gen2 {GC.CollectionCount(2) - gc2}, pause {(GC.GetTotalPauseDuration() - pause).TotalMilliseconds:F0} ms, server GC: {System.Runtime.GCSettings.IsServerGC}");
        PriceCache.InvalidateAll();

        var storeAlias = stores[0].Alias;
        var productCache = (ProductCache)sp.GetRequiredService<IPerStoreIndexedCache<IProduct>>();
        Assert.Equal(productCount, productCache.Cache[storeAlias].Count);
        var all = productCache.Cache[storeAlias].Values.ToList();
        var topCategory = Catalog.Instance.GetCategory(tree.FirstTopCategoryId, storeAlias)!;
        Assert.NotNull(topCategory);

        Line("");
        Line("## Metafields");
        var coldAlloc = GC.GetAllocatedBytesForCurrentThread();
        var cold = Stopwatch.StartNew();
        var metavalues = all.Sum(p => p.Metafields.Count);
        cold.Stop();
        Line($"first Metafields read for {all.Count} products: {cold.Elapsed.TotalMilliseconds:F0} ms, {(GC.GetAllocatedBytesForCurrentThread() - coldAlloc) / 1024 / 1024} MB allocated, {metavalues} values");
        var metaBefore = GC.GetTotalMemory(true);
        var otherStore = productCache.Cache[stores[^1].Alias].Values.ToList();
        var warm = Stopwatch.StartNew();
        var otherValues = otherStore.Sum(p => p.Metafields.Count);
        Line($"first Metafields read, second store ({otherStore.Count} products): {warm.Elapsed.TotalMilliseconds:F0} ms, retained {(GC.GetTotalMemory(true) - metaBefore) / 1024 / 1024} MB, {otherValues} values");

        Line("");
        Line("## Product responses (store " + storeAlias + ")");
        Line($"{"scenario",-52} {"median ms",10} {"alloc KB",10}");

        await Scenario("GetAllProductsAsync, no query", 15, async () =>
            Count(await Catalog.Instance.GetAllProductsAsync(storeAlias)));
        await Scenario("GetAllProductsAsync, page 24, default order", 15, async () =>
            Count(await Catalog.Instance.GetAllProductsAsync(storeAlias, Page(24))));
        await Scenario("GetAllProductsAsync, page 24, TitleAsc", 15, async () =>
            Count(await Catalog.Instance.GetAllProductsAsync(storeAlias, Page(24, OrderBy.TitleAsc))));
        await Scenario("GetAllProductsAsync, page 24, PriceAsc", 15, async () =>
            Count(await Catalog.Instance.GetAllProductsAsync(storeAlias, Page(24, OrderBy.PriceAsc))));
        await Scenario("GetAllProductsAsync, page 24, DateDesc", 15, async () =>
            Count(await Catalog.Instance.GetAllProductsAsync(storeAlias, Page(24, OrderBy.DateDesc))));
        await Scenario("GetAllProductsAsync, page 24, filter zero price", 15, async () =>
        {
            var q = Page(24);
            q.FilterOutZeroPriceProducts = true;
            return Count(await Catalog.Instance.GetAllProductsAsync(storeAlias, q));
        });
        await Scenario("GetAllProductsAsync, page 24, property filter", 15, async () =>
        {
            var q = Page(24);
            q.PropertyFilters = new Dictionary<string, List<string>> { ["brand"] = ["Brand 3"] };
            return Count(await Catalog.Instance.GetAllProductsAsync(storeAlias, q));
        });
        await Scenario("GetAll, page 24 + enumerate facets", 15, async () =>
            (await Catalog.Instance.GetAllProductsAsync(storeAlias, Page(24))).Filters.Count());
        await Scenario("GetAll, page 24, meta filter color (1 value) + facets", 15, async () =>
            CountWithFacets(await Catalog.Instance.GetAllProductsAsync(storeAlias, Meta(("color", ["Color 3"])))));
        await Scenario("GetAll, page 24, meta filter color+brand+size + facets", 15, async () =>
            CountWithFacets(await Catalog.Instance.GetAllProductsAsync(storeAlias,
                Meta(("color", ["Color 3", "Color 5"]), ("brand", ["Brand 7", "Brand 8", "Brand 9"]), ("size", ["Size 2"])))));
        await Scenario("GetAll, page 24, meta filter features (all must match)", 15, async () =>
            CountWithFacets(await Catalog.Instance.GetAllProductsAsync(storeAlias, Meta(("features", ["Features 1", "Features 2"])))));
        await Scenario("GetAll, page 24, all filters visible + color filter", 15, async () =>
        {
            var q = Meta(("color", ["Color 3"]));
            q.AllFiltersVisible = true;
            return CountWithFacets(await Catalog.Instance.GetAllProductsAsync(storeAlias, q));
        });
        await Scenario("category recursive (500), color filter + facets", 30, async () =>
            CountWithFacets(await topCategory.ProductsRecursiveAsync(Meta(("color", ["Color 3"])))));
        await Scenario("category.FiltersAsync (500 products)", 30, async () =>
            (await topCategory.FiltersAsync()).Count());
        await Scenario($"category.ProductsRecursiveAsync, page 24 ({tree.ProductsPerTopCategory} products)", 30, async () =>
            Count(await topCategory.ProductsRecursiveAsync(Page(24))));
        await Scenario("GetProductsByIdsAsync, 24 ids", 200, async () =>
            Count(await Catalog.Instance.GetProductsByIdsAsync(storeAlias, new ProductQuery { Ids = tree.SomeProductIds(24) })));

        var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        var page24 = (await Catalog.Instance.GetAllProductsAsync(storeAlias, Page(24))).Products.ToList();
        var page200 = (await Catalog.Instance.GetAllProductsAsync(storeAlias, Page(200))).Products.ToList();
        await Scenario("Serialize 24 products (Newtonsoft camelCase)", 30, () =>
            Task.FromResult(JsonConvert.SerializeObject(page24, settings).Length));
        await Scenario("Serialize 200 products (Newtonsoft camelCase)", 10, () =>
            Task.FromResult(JsonConvert.SerializeObject(page200, settings).Length));
        Line($"serialized size, 24 products: {JsonConvert.SerializeObject(page24, settings).Length / 1024} KB");

        var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var response24 = await Catalog.Instance.GetAllProductsAsync(storeAlias, Page(24));
        var response200 = await Catalog.Instance.GetAllProductsAsync(storeAlias, Page(200));
        await Scenario("Serialize ProductResponse, 24 products (System.Text.Json)", 30, () =>
            Task.FromResult(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(response24, web).Length));
        await Scenario("Serialize ProductResponse, 200 products (System.Text.Json)", 10, () =>
            Task.FromResult(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(response200, web).Length));
        Line($"serialized size, 24 products (System.Text.Json): {System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(response24, web).Length / 1024} KB");

        Line("");
        Line("## Serialized product, per property (System.Text.Json, avg over 24 products)");
        Line($"{"property",-28} {"us",10} {"bytes",10}");
        var interfaces = new[] { typeof(IProduct) }.Concat(typeof(IProduct).GetInterfaces());
        var rows = new List<(string Name, double Us, long Bytes)>();
        foreach (var property in interfaces.SelectMany(x => x.GetProperties()).DistinctBy(x => x.Name))
        {
            if (property.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), true).Length > 0)
                continue;

            var best = double.MaxValue;
            long bytes = 0;
            for (var pass = 0; pass < 5; pass++)
            {
                bytes = 0;
                var sw = Stopwatch.StartNew();
                foreach (var p in page24)
                    bytes += System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(property.GetValue(p), property.PropertyType, web).Length;
                best = Math.Min(best, sw.Elapsed.TotalMicroseconds / page24.Count);
            }

            rows.Add((property.Name, best, bytes / page24.Count));
        }

        foreach (var row in rows.OrderByDescending(x => x.Us))
            Line($"{row.Name,-28} {row.Us,10:F1} {row.Bytes,10}");

        Line("");
        Line("## Fill breakdown (us per node, store " + storeAlias + ", outside a rebuild)");
        Line($"{"step",-44} {"us",10} {"alloc B",10}");
        var store0 = stores[0];
        var productNodes = tree.NodesByTypes("ekmProduct").ToList();
        var variantNodes = tree.NodesByTypes("ekmProductVariant").ToList();
        var groupNodes = tree.NodesByTypes("ekmProductVariantGroup").ToList();
        using (CacheInitializationScope.Begin())
        {
            Step("product: IsItemDisabled", productNodes, n => n.IsItemDisabled(store0) ? 1 : 0);
            Step("product: copy property dictionary", productNodes, n => n.Properties.ToDictionary(x => x.Key, x => x.Value).Count);
            Step("product: GetValue(price, store)", productNodes, n => n.GetValue("price", store0.Alias).Length);
            Step("product: GetValue(sku)", productNodes, n => n.Properties.GetPropertyValue("sku").Length);
            Step("product: GetValue(categories) (empty)", productNodes, n => n.Properties.GetPropertyValue("categories").Length);
            Step("product: new Product(node, store)", productNodes, n => new Product(n, store0).Id);
            Step("group:   new VariantGroup(node, store)", groupNodes, n => new VariantGroup(n, store0).Id);
            Step("variant: IsItemDisabled", variantNodes, n => n.IsItemDisabled(store0) ? 1 : 0);
            Step("variant: new Variant(node, store)", variantNodes, n => new Variant(n, store0).Id);
            var someVariants = ((VariantCache)sp.GetRequiredService<IPerStoreIndexedCache<IVariant>>()).Cache[storeAlias].Values.ToList();
            StepOf("variant.Product (Catalog lookup)", someVariants, v => v.Product == null ? 0 : 1);
            StepOf("variant.Vat", someVariants, v => v.Vat > 0 ? 1 : 0);
            StepOf("variant.Prices", someVariants, v => v.Prices.Count);
            StepOf("variant.Available", someVariants, v => v.Available ? 1 : 0);
            StepOf("variant.Title", someVariants, v => v.Title.Length);
        }

        Line("");
        Line("## Retained fill replica (us per node, all stores, objects kept alive like a real fill)");
        Line($"{"entity",-10} {"disabled",9} {"create",9} {"dict add",9} {"indexes",9} {"GC pause ms",12} {"total ms",9} {"retained MB",12}");
        using (CacheInitializationScope.Begin())
        {
            Replica("product", productNodes, stores, (n, s) => new Product(n, s), p => p.SKU);
            Replica("variant", variantNodes, stores, (n, s) => new Variant(n, s), v => v.SKU);
            Replica("group", groupNodes, stores, (n, s) => new VariantGroup(n, s), _ => null);
            Replica("dict only", variantNodes, stores, (n, s) => n, _ => null);
        }

        var emptyBefore = GC.GetAllocatedBytesForCurrentThread();
        var keep = new object[1000];
        for (var i = 0; i < keep.Length; i++)
            keep[i] = new System.Collections.Concurrent.ConcurrentDictionary<string, object>();
        Line($"empty ConcurrentDictionary<string, object>: {(GC.GetAllocatedBytesForCurrentThread() - emptyBefore) / keep.Length} B each");
        emptyBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < keep.Length; i++)
            keep[i] = new System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> { [Guid.NewGuid()] = 0 };
        Line($"ConcurrentDictionary<Guid, byte> with one entry: {(GC.GetAllocatedBytesForCurrentThread() - emptyBefore) / keep.Length} B each");
        GC.KeepAlive(keep);

        Line("");
        Line("## Per-access cost (ns per call, " + all.Count + " products per pass)");
        Micro("product.Key", all, p => p.Key.GetHashCode());
        Micro("product.SortOrder", all, p => p.SortOrder);
        Micro("product.CreateDate", all, p => p.CreateDate.Day);
        Micro("product.Title", all, p => p.Title.Length);
        Micro("product.Properties", all, p => p.Properties.Count);
        Micro("product.Price", all, p => p.Price.Value > 0 ? 1 : 0);
        Micro("product.Prices", all, p => p.Prices.Count);
        Micro("product.OriginalPrice", all, p => p.OriginalPrice.Value > 0 ? 1 : 0);
        Micro("product.VariantGroups", all, p => p.VariantGroups.Count());
        Micro("product.PrimaryVariantGroup", all, p => p.PrimaryVariantGroup == null ? 0 : 1);
        Micro("product.PrimaryVariant", all, p => p.PrimaryVariant == null ? 0 : 1);
        Micro("product.AllVariants", all, p => p.AllVariants.Count());
        Micro("product.Available", all, p => p.Available ? 1 : 0);
        Micro("product.Images", all, p => p.Images.Count());
        Micro("product.Url", all, p => p.Url.Length);
        Micro("product.Metafields", all, p => p.Metafields.Count);
        Micro("primaryVariant.Price", all, p => p.PrimaryVariant?.Price.Value > 0 ? 1 : 0);
        Micro("Catalog.Instance (resolve)", all, _ => Catalog.Instance == null ? 0 : 1);
        Micro("Catalog.Instance.GetCategory(id)", all, p => Catalog.Instance.GetCategory(p.ParentId, storeAlias, raiseEvent: false) == null ? 0 : 1);
        var prices = all.Select(p => p.Price).ToList();
        var index = 0;
        IPrice Next() => prices[index++ % prices.Count];
        Micro("price.Value", all, _ => Next().Value > 0 ? 1 : 0);
        Micro("price.WithVat.CurrencyString", all, _ => Next().WithVat.CurrencyString.Length);
        Micro("price.Currency.ISOCurrencySymbol", all, _ => Next().Currency.ISOCurrencySymbol.Length);
        Micro("price.Currency.CurrencySymbol", all, _ => Next().Currency.CurrencySymbol.Length);
        Micro("price.Currency.CurrencyDecimalDigits", all, _ => Next().Currency.CurrencyDecimalDigits);
        Micro("serialize IPrice (System.Text.Json)", all, _ => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(Next(), web).Length);
        Micro("Configuration.Instance.VatRoundingScope", all, _ => (int)Configuration.Instance.VatRoundingScope);
        Micro("Configuration.Instance.UseDatabasePrices", all, _ => Configuration.Instance.UseDatabasePrices ? 1 : 0);
        var categoryCache = sp.GetRequiredService<IPerStoreIndexedCache<ICategory>>();
        Micro("categoryCache.TryGetById", all, p => categoryCache.TryGetById(storeAlias, p.ParentId, out _) ? 1 : 0);
        Micro("productCache.TryGetByKey", all, p => productCache.TryGetByKey(storeAlias, p.Key, out _) ? 1 : 0);
        Micro("productCache.Cache (property)", all, _ => productCache.Cache.Count);

        var outPath = Environment.GetEnvironmentVariable("EKOM_BENCH_OUT");
        if (!string.IsNullOrWhiteSpace(outPath))
            await File.WriteAllTextAsync(outPath, _report.ToString());
    }

    private static int Count(ProductResponse response) => response.Products.Count();

    // The API serializes Filters, so a realistic request enumerates them too.
    private static int CountWithFacets(ProductResponse response)
        => response.Products.Count() + response.Filters.Sum(x => x.Values.Count);

    private static ProductQuery Meta(params (string Alias, string[] Values)[] filters)
    {
        var query = Page(24);
        query.MetaFilters = filters.ToDictionary(x => x.Alias, x => x.Values.ToList());
        return query;
    }

    private static ProductQuery Page(int size, OrderBy? orderBy = null)
    {
        var query = new ProductQuery { PageSize = size, Page = 1 };
        if (orderBy.HasValue)
            query.OrderBy = orderBy;
        return query;
    }

    private async Task Scenario(string name, int iterations, Func<Task<int>> action)
    {
        await action();
        var times = new List<double>(iterations);
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await action();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        var alloc = (GC.GetAllocatedBytesForCurrentThread() - allocBefore) / iterations / 1024;
        Line($"{name,-52} {Median(times),10:F2} {alloc,10}");
    }

    private void Micro(string name, List<IProduct> products, Func<IProduct, int> access)
    {
        long sink = 0;
        foreach (var p in products)
            sink += access(p);

        var best = double.MaxValue;
        long alloc = 0;
        for (var pass = 0; pass < 5; pass++)
        {
            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            foreach (var p in products)
                sink += access(p);
            var ns = sw.Elapsed.TotalNanoseconds / products.Count;
            alloc = (GC.GetAllocatedBytesForCurrentThread() - allocBefore) / products.Count;
            best = Math.Min(best, ns);
        }

        GC.KeepAlive(sink);
        Line($"{name,-36} {best,10:F0} ns {alloc,8} B");
    }

    // Mirrors PerStoreCache.FillStoreCache step by step with a stopwatch around each part.
    private void Replica<T>(
        string name,
        List<UmbracoContent> nodes,
        List<BenchStore> stores,
        Func<UmbracoContent, IStore, T> create,
        Func<T, string?> sku)
        where T : class
    {
        GC.Collect();
        var memoryBefore = GC.GetTotalMemory(true);
        var pauseBefore = GC.GetTotalPauseDuration();
        long disabled = 0, created = 0, added = 0, indexed = 0;
        var keep = new List<object>();
        var total = Stopwatch.StartNew();
        foreach (var store in stores)
        {
            var cache = new System.Collections.Concurrent.ConcurrentDictionary<Guid, T>();
            var idIndex = new System.Collections.Concurrent.ConcurrentDictionary<int, Guid>();
            var skuIndex = new System.Collections.Concurrent.ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var groupIndex = new System.Collections.Concurrent.ConcurrentDictionary<int, System.Collections.Concurrent.ConcurrentDictionary<Guid, byte>>();
            keep.Add(cache);
            keep.Add(idIndex);
            keep.Add(skuIndex);
            keep.Add(groupIndex);
            foreach (var node in nodes)
            {
                var t0 = Stopwatch.GetTimestamp();
                var isDisabled = node.IsItemDisabled(store);
                var t1 = Stopwatch.GetTimestamp();
                if (isDisabled) continue;
                var item = create(node, store);
                var t2 = Stopwatch.GetTimestamp();
                cache[node.Key] = item;
                var t3 = Stopwatch.GetTimestamp();
                idIndex[node.Id] = node.Key;
                var itemSku = sku(item);
                if (!string.IsNullOrWhiteSpace(itemSku))
                    skuIndex[itemSku.Trim()] = node.Key;
                groupIndex.GetOrAdd(node.Id / 2, static _ => new System.Collections.Concurrent.ConcurrentDictionary<Guid, byte>()).TryAdd(node.Key, 0);
                var t4 = Stopwatch.GetTimestamp();
                disabled += t1 - t0;
                created += t2 - t1;
                added += t3 - t2;
                indexed += t4 - t3;
            }
        }

        total.Stop();
        var pause = (GC.GetTotalPauseDuration() - pauseBefore).TotalMilliseconds;
        var retained = (GC.GetTotalMemory(true) - memoryBefore) / 1024.0 / 1024.0;
        double count = nodes.Count * stores.Count;
        double Us(long ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency / count;
        Line($"{name,-10} {Us(disabled),9:F2} {Us(created),9:F2} {Us(added),9:F2} {Us(indexed),9:F2} {pause,12:F0} {total.Elapsed.TotalMilliseconds,9:F0} {retained,12:F0}");
        GC.KeepAlive(keep);
    }

    private void Step(string name, List<UmbracoContent> nodes, Func<UmbracoContent, int> action) => StepOf(name, nodes, action);

    private void StepOf<T>(string name, List<T> items, Func<T, int> action)
    {
        long sink = 0;
        var best = double.MaxValue;
        long alloc = 0;
        for (var pass = 0; pass < 4; pass++)
        {
            var allocBefore = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            foreach (var item in items)
                sink += action(item);
            best = Math.Min(best, sw.Elapsed.TotalMicroseconds / items.Count);
            alloc = (GC.GetAllocatedBytesForCurrentThread() - allocBefore) / items.Count;
        }

        GC.KeepAlive(sink);
        Line($"{name,-44} {best,10:F2} {alloc,10}");
    }

    private void Line(string text)
    {
        _report.AppendLine(text);
        _output.WriteLine(text);
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        return sorted[sorted.Count / 2];
    }

    private static int EnvInt(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;

    private sealed class BenchStore : IStore
    {
        private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

        public BenchStore(string alias, int id)
        {
            Alias = alias;
            Id = id;
            Key = Guid.NewGuid();
            Currency = new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" };
            Currencies = [Currency];
            Culture = CultureInfoDto.From(new CultureInfo("is-IS"));
            Cultures = [Culture];
        }

        public string Alias { get; }
        public int Id { get; set; }
        public Guid Key { get; }
        public decimal Vat => 0.24m;
        public CultureInfoDto Culture { get; }
        public List<CultureInfoDto> Cultures { get; }
        public CurrencyModel Currency { get; }
        public List<CurrencyModel> Currencies { get; }
        public CurrencyModel GetCurrentCurrency() => Currency;
        public IEnumerable<UmbracoDomain> Domains => Array.Empty<UmbracoDomain>();
        public UmbracoContent StoreRootNode => null!;
        public int StoreRootNodeId => 2;
        public string OrderNumberPrefix => string.Empty;
        public string UrlPrefix(string culture) => string.Empty;
        public string OrderNumberTemplate => string.Empty;
        public string Url => "/";
        public bool VatIncludedInPrice => true;
        public bool UserBasket => false;
        public bool ShareBasketBetweenStores => false;
        public bool ApplyVatOnShipping => false;
        public string Title => Alias;
        public string ContentTypeAlias => "ekmStore";
        public DateTime CreateDate => DateTime.MinValue;
        public DateTime UpdateDate => DateTime.MinValue;
        public int ParentId => 1;
        public int SortOrder => Id;
        public int Level => 2;
        public bool VariesByCulture => false;
        public string Path { get; set; } = "-1,1";
        public string[] PathArray { get; set; } = ["-1", "1"];
        public string GetValue(string propAlias, string? alias = null, bool fallback = false) => string.Empty;
        public string GetRawValue(string propAlias) => string.Empty;
        public IReadOnlyDictionary<string, string> Properties => Empty;
    }

    private sealed class BenchUrlService : IUrlService
    {
        public List<UmbracoUrl> BuildCategoryUrls(IEnumerable<UmbracoContent> items, IStore store)
        {
            var url = new StringBuilder("/");
            foreach (var item in items)
                url.Append(item.GetValue("slug", store.Alias)).Append('/');

            return store.Cultures
                .Select(c => new UmbracoUrl { Culture = c.Name, Store = store.Alias, Url = url.ToString() })
                .ToList();
        }

        public IEnumerable<string> BuildCategoryUrls(string slug, List<string> hierarchy, IStore store)
            => Array.Empty<string>();

        public IEnumerable<string> BuildProductUrls(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId)
            => BuildProductUrlsWithContext(item, categories, store, nodeId).Select(x => x.Url);

        public List<UmbracoUrl> BuildProductUrlsWithContext(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId)
        {
            var slug = item.GetValue("slug", store.Alias);
            return categories
                .SelectMany(c => c.UrlsWithContext)
                .Select(c => new UmbracoUrl { Culture = c.Culture, Store = store.Alias, Url = c.Url + slug + "/", Domain = c.Domain })
                .ToList();
        }

        public string? GetNodeEntityUrl(INodeEntityWithUrl node) => node.Urls.FirstOrDefault();
    }

    /// <summary>ekom(1) / catalog(2) / top category / sub category / product / variant group / variant</summary>
    private sealed class SyntheticTree : INodeService
    {
        private const int TopCategories = 10;
        private const int SubCategories = 10;
        private readonly Dictionary<string, List<UmbracoContent>> _byType = new();
        private readonly Dictionary<int, (Dictionary<string, string> Defaults, Dictionary<string, string> Content)> _source = new();
        private readonly Dictionary<int, UmbracoContent> _byId = new();
        private readonly string[] _stores;
        private readonly List<int> _productIds = new();
        private int _nextId = 10;

        private static readonly (string Name, string Alias, bool Filterable, bool Multiple, int Count)[] FieldDefinitions =
        [
            ("Color", "color", true, true, 12), ("Size", "size", true, true, 10), ("Brand", "brand", true, false, 40),
            ("Material", "material", true, true, 10), ("Season", "season", true, false, 4), ("Gender", "gender", true, false, 3),
            ("Collection", "collection", true, false, 25), ("Features", "features", true, true, 8),
            ("Care instructions", "careInstructions", false, false, 0), ("Weight", "weight", false, false, 0),
        ];

        private readonly List<(Guid Key, string Alias, bool Multiple, List<string> ValueIds)> _fields = new();

        public SyntheticTree(int products, int variantsPerProduct, string[] stores)
        {
            _stores = stores;
            foreach (var (name, alias, filterable, multiple, count) in FieldDefinitions)
            {
                var ids = Enumerable.Range(1, count).Select(i => $"{alias}-{i}").ToList();
                var values = new Newtonsoft.Json.Linq.JArray(ids.Select((id, i) => new Newtonsoft.Json.Linq.JObject
                {
                    ["id"] = id,
                    ["values"] = new Newtonsoft.Json.Linq.JObject { ["en-US"] = $"{name} {i + 1}", ["is-IS"] = $"{name} {i + 1} (IS)" },
                }));
                var field = Add("ekmMetafield", 3, Guid.Empty, "-1,1,3", 3, name, new Dictionary<string, string>
                {
                    ["title"] = new Newtonsoft.Json.Linq.JObject
                    {
                        ["values"] = new Newtonsoft.Json.Linq.JObject { ["en-US"] = name, ["is-IS"] = name + " (IS)" },
                        ["type"] = "Language",
                    }.ToString(Formatting.None),
                    ["alias"] = alias,
                    ["description"] = string.Empty,
                    ["filterable"] = filterable ? "1" : "0",
                    ["enableMultipleChoice"] = multiple ? "1" : "0",
                    ["required"] = "0",
                    ["readOnly"] = "0",
                    ["allConditionsMustMatch"] = alias == "features" ? "1" : "0",
                    ["values"] = count == 0 ? string.Empty : values.ToString(Formatting.None),
                });
                _fields.Add((field.Key, alias, multiple, ids));
            }

            var subs = new List<(int Id, Guid Key, string Path)>();
            for (var t = 0; t < TopCategories; t++)
            {
                var top = Add("ekmCategory", 2, Guid.Empty, "-1,1,2", 3, $"Category {t}", CategoryProps($"category-{t}", $"Category {t}"));
                if (t == 0) FirstTopCategoryId = top.Id;
                for (var s = 0; s < SubCategories; s++)
                {
                    var sub = Add("ekmCategory", top.Id, top.Key, top.Path, 4, $"Category {t}.{s}", CategoryProps($"sub-{t}-{s}", $"Category {t}.{s}"));
                    subs.Add((sub.Id, sub.Key, sub.Path));
                }
            }

            CategoryCount = TopCategories + TopCategories * SubCategories;
            ProductsPerTopCategory = products / TopCategories;

            for (var p = 0; p < products; p++)
            {
                var parent = subs[p % subs.Count];
                var product = Add("ekmProduct", parent.Id, parent.Key, parent.Path, 5, $"Product {p}", ProductProps(p));
                _productIds.Add(product.Id);
                var group = Add("ekmProductVariantGroup", product.Id, product.Key, product.Path, 6, "Colors", GroupProps());
                for (var v = 0; v < variantsPerProduct; v++)
                    Add("ekmProductVariant", group.Id, group.Key, group.Path, 7, $"Variant {v}", VariantProps(p, v));
            }
        }

        public int CategoryCount { get; }
        public int ProductsPerTopCategory { get; }
        public int FirstTopCategoryId { get; private set; }

        public int[] SomeProductIds(int count) => _productIds.Take(count).ToArray();

        private string PerStore(Func<string, string> value)
        {
            var values = new Newtonsoft.Json.Linq.JObject();
            foreach (var store in _stores)
                values[store] = value(store);
            return new Newtonsoft.Json.Linq.JObject { ["values"] = values, ["type"] = "Store" }.ToString(Formatting.None);
        }

        private Dictionary<string, string> CategoryProps(string slug, string title) => new()
        {
            ["title"] = PerStore(s => $"{title} {s}"),
            ["slug"] = PerStore(_ => slug),
            ["disable"] = PerStore(_ => "0"),
            ["description"] = PerStore(s => $"<p>Description of {title} for {s}. " + new string('x', 300) + "</p>"),
            ["ekmVirtualUrl"] = "0",
        };

        private Dictionary<string, string> ProductProps(int p) => new()
        {
            ["title"] = PerStore(s => $"Product {p} {s}"),
            ["slug"] = PerStore(_ => $"product-{p}"),
            ["sku"] = $"SKU-{p}",
            ["disable"] = PerStore(_ => "0"),
            ["price"] = PerStore(_ => $"[{{\"Currency\":\"is-IS\",\"Price\":{1000 + (p * 37 % 9000)}}}]"),
            ["description"] = PerStore(s => $"<p>Long description of product {p} for {s}. " + new string('x', 600) + "</p>"),
            ["summary"] = PerStore(s => $"Summary of product {p} for {s}"),
            ["enableBackorder"] = PerStore(_ => "0"),
            ["brand"] = $"Brand {p % 20}",
            ["score"] = (p % 100).ToString(CultureInfo.InvariantCulture),
            ["vat"] = string.Empty,
            ["categories"] = string.Empty,
            ["relatedProducts"] = string.Empty,
            ["images"] = string.Empty,
            ["metafields"] = Metafields(p),
            ["metaTitle"] = PerStore(s => $"Product {p} {s}"),
            ["metaDescription"] = PerStore(s => $"Meta description {p} {s}"),
        };

        private string Metafields(int p)
        {
            if (Environment.GetEnvironmentVariable("EKOM_BENCH_METAFIELDS") == "0")
                return string.Empty;

            var random = new Random(7919 + p);
            var items = new Newtonsoft.Json.Linq.JArray();
            foreach (var (key, alias, multiple, ids) in _fields)
            {
                if (ids.Count == 0)
                {
                    items.Add(new Newtonsoft.Json.Linq.JObject { ["key"] = key, ["values"] = alias == "weight" ? $"{100 + random.Next(0, 1900)} g" : "Machine wash" });
                    continue;
                }

                if (alias is "collection" or "features" && random.Next(0, 10) < 3)
                    continue;

                var count = multiple ? 1 + random.Next(0, 3) : 1;
                items.Add(new Newtonsoft.Json.Linq.JObject
                {
                    ["key"] = key,
                    ["values"] = new Newtonsoft.Json.Linq.JArray(ids.OrderBy(_ => random.Next()).Take(count)
                        .Select(id => new Newtonsoft.Json.Linq.JObject { ["id"] = id })),
                });
            }

            return items.ToString(Formatting.None);
        }

        private Dictionary<string, string> GroupProps() => new()
        {
            ["title"] = PerStore(s => $"Colors {s}"),
            ["disable"] = PerStore(_ => "0"),
            ["images"] = string.Empty,
        };

        private Dictionary<string, string> VariantProps(int p, int v) => new()
        {
            ["title"] = PerStore(s => $"Variant {v} {s}"),
            ["sku"] = $"SKU-{p}-{v}",
            ["disable"] = PerStore(_ => "0"),
            ["price"] = PerStore(_ => $"[{{\"Currency\":\"is-IS\",\"Price\":{1000 + (p * 37 % 9000) + v * 100}}}]"),
            ["enableBackorder"] = PerStore(_ => "1"),
            ["images"] = string.Empty,
        };

        private UmbracoContent Add(string type, int parentId, Guid parentKey, string parentPath, int level, string name, Dictionary<string, string> content)
        {
            var id = _nextId++;
            var defaults = new Dictionary<string, string>
            {
                ["id"] = id.ToString(CultureInfo.InvariantCulture),
                ["parentID"] = parentId.ToString(CultureInfo.InvariantCulture),
                ["parentKey"] = parentKey.ToString(),
                ["__Key"] = Guid.NewGuid().ToString(),
                ["nodeName"] = name,
                ["__NodeTypeAlias"] = type,
                ["sortOrder"] = (id % 50).ToString(CultureInfo.InvariantCulture),
                ["level"] = level.ToString(CultureInfo.InvariantCulture),
                ["__Path"] = $"{parentPath},{id}",
                ["createDate"] = new DateTime(2024, 1, 1).AddMinutes(id).ToString("O", CultureInfo.InvariantCulture),
                ["updateDate"] = new DateTime(2024, 6, 1).AddMinutes(id).ToString("O", CultureInfo.InvariantCulture),
                ["__VariesByCulture"] = "n",
                ["url"] = "#",
            };
            var node = new UmbracoContent(defaults, content);
            _source[id] = (defaults, content);
            _byId[id] = node;
            if (!_byType.TryGetValue(type, out var list))
                _byType[type] = list = new List<UmbracoContent>();
            list.Add(node);
            return node;
        }

        public IEnumerable<UmbracoContent> NodesByTypes(string contentTypeAlias)
            => _byType.TryGetValue(contentTypeAlias, out var list) ? list : Array.Empty<UmbracoContent>();

        public IEnumerable<UmbracoContent> NodesByTypesFaster(string contentTypeAlias) => NodesByTypes(contentTypeAlias);

        // Like the Umbraco services, map a fresh content object for every ancestor requested.
        private IEnumerable<UmbracoContent> Ancestors(string path, bool includeSelf, bool catalogOnly)
        {
            var ids = path.Split(',');
            var result = new List<UmbracoContent>();
            for (var i = 0; i < ids.Length - (includeSelf ? 0 : 1); i++)
            {
                if (!int.TryParse(ids[i], out var id) || !_source.TryGetValue(id, out var source)) continue;
                var type = source.Defaults["__NodeTypeAlias"];
                if (catalogOnly && type is not ("ekmCategory" or "ekmProduct")) continue;
                result.Add(new UmbracoContent(source.Defaults, source.Content));
            }

            return result;
        }

        public IEnumerable<UmbracoContent> NodeAncestors(string t, bool preview = false)
            => _byId.TryGetValue(int.Parse(t, CultureInfo.InvariantCulture), out var node) ? Ancestors(node.Path, false, false).Reverse() : Array.Empty<UmbracoContent>();

        public IEnumerable<UmbracoContent> NodeCatalogAncestors(string t)
            => _byId.TryGetValue(int.Parse(t, CultureInfo.InvariantCulture), out var node) ? Ancestors(node.Path, true, true) : Array.Empty<UmbracoContent>();

        public IEnumerable<UmbracoContent> GetAllCatalogAncestors(UmbracoContent item) => Ancestors(item.Path, true, true);

        public IEnumerable<UmbracoContent> NodeChildren(string t) => Array.Empty<UmbracoContent>();
        public bool IsItemUnpublished(UmbracoContent content) => false;
        public UmbracoContent? NodeById(Guid t, bool preview = false) => null;
        public UmbracoContent? NodeById(int t, bool preview = false) => _byId.GetValueOrDefault(t);
        public UmbracoContent? NodeById(string t, bool preview = false) => int.TryParse(t, out var id) ? NodeById(id) : null;
        public UmbracoContent? MediaById(Guid t) => null;
        public UmbracoContent? MediaById(int t) => null;
        public UmbracoContent? MediaById(string t) => null;
        public string GetUrl(string t, string url = null!) => "#";
    }
}
