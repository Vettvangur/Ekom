#if !NET8_0
using Ekom.Models.Import;
using Ekom.Tests.Objects;
using Ekom.Umb.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Xunit;
using Xunit.Abstractions;

namespace Ekom.Tests.Tests;

/// <summary>
/// Manual benchmark for a product sync where nothing has changed, over a synthetic catalog.
/// Skipped unless EKOM_BENCH=1. Sizes: EKOM_BENCH_CATEGORIES, EKOM_BENCH_PRODUCTS_PER_CATEGORY, EKOM_BENCH_VARIANTS.
/// Content is real Umbraco model objects held in memory, so only the import service's own work is measured;
/// database reads, saves, publishing, stock, prices and media are excluded.
/// </summary>
[Trait("Category", "Benchmark")]
[Collection("Reservations")]
public class ImportPerformanceBenchmark
{
    private static readonly string[] ProductIgnore = ["VariantGroups", "Images", "EventProperties", "Files", "Stock", "WarehouseStock", "UpdateSlug"];
    private static readonly string[] GroupIgnore = ["Variants", "Images", "EventProperties"];
    private static readonly string[] VariantIgnore = ["Images", "EventProperties", "Files", "Stock", "WarehouseStock"];

    private readonly ITestOutputHelper _output;
    private int _nextId = 1;

    public ImportPerformanceBenchmark(ITestOutputHelper output) => _output = output;

    [Fact]
    public void MeasureUnchangedProductSync()
    {
        if (Environment.GetEnvironmentVariable("EKOM_BENCH") != "1")
            return;

        var categoryCount = EnvInt("EKOM_BENCH_CATEGORIES", 20);
        var productsPerCategory = EnvInt("EKOM_BENCH_PRODUCTS_PER_CATEGORY", 500);
        var variantsPerProduct = EnvInt("EKOM_BENCH_VARIANTS", 3);

        using var scope = new ConfigurationScope();
        var contentService = new Mock<IContentService>(MockBehavior.Strict);
        var service = new ImportService(null!, contentService.Object, null!, null!, null!,
            NullLogger<ImportService>.Instance, null!, null!, null!, null!);
        var hash = typeof(ImportService)
            .GetMethod("ComputeSha256Hash", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<object, string[]?, string>>(service);

        var helper = new Mock<IShortStringHelper>();
        helper.Setup(x => x.CleanStringForSafeAlias(It.IsAny<string>())).Returns((string value) => value);
        helper.Setup(x => x.CleanStringForSafeAlias(It.IsAny<string>(), It.IsAny<string>())).Returns((string value, string _) => value);
        helper.Setup(x => x.CleanString(It.IsAny<string>(), It.IsAny<CleanStringType>())).Returns((string value, CleanStringType _) => value);
        var catalogType = ContentTypeOf(helper.Object, "ekmCatalog");
        var categoryType = ContentTypeOf(helper.Object, "ekmCategory");
        var productType = ContentTypeOf(helper.Object, "ekmProduct");
        var groupType = ContentTypeOf(helper.Object, "ekmProductVariantGroup");
        var variantType = ContentTypeOf(helper.Object, "ekmProductVariant");

        var root = Node(catalogType, null, "root", null);
        var nodes = new List<IContent> { root };
        var categories = new List<IContent>();
        var products = new List<IContent>();
        var imports = new List<ImportProduct>();

        for (var c = 0; c < categoryCount; c++)
        {
            var category = Node(categoryType, root, $"cat-{c}", null);
            nodes.Add(category);
            categories.Add(category);

            for (var p = 0; p < productsPerCategory; p++)
            {
                var import = ProductImport(c, p, variantsPerProduct);
                imports.Add(import);

                var product = Node(productType, category, import.Identifier, hash(import, ProductIgnore));
                nodes.Add(product);
                products.Add(product);

                foreach (var importGroup in import.VariantGroups)
                {
                    var group = Node(groupType, product, importGroup.Identifier, hash(importGroup, GroupIgnore));
                    nodes.Add(group);
                    foreach (var importVariant in importGroup.Variants)
                        nodes.Add(Node(variantType, group, importVariant.Identifier, hash(importVariant, VariantIgnore)));
                }
            }
        }

        SetField(service, "umbracoRootContent", root);
        SetField(service, "catalogContentType", catalogType);
        SetField(service, "categoryContentType", categoryType);
        SetField(service, "productContentType", productType);
        SetField(service, "productVariantGroupContentType", groupType);
        SetField(service, "productVariantContentType", variantType);
        var productsSaved = new List<ImportProduct>();
        var groupsSaved = new List<ImportVariantGroup>();
        var variantsSaved = new List<ImportVariant>();
        SetField(service, "productsSaved", productsSaved);
        SetField(service, "categoriesSaved", new List<ImportCategory>());
        SetField(service, "variantGroupsSaved", groupsSaved);
        SetField(service, "variantsSaved", variantsSaved);

        var iterate = typeof(ImportService).GetMethod("IterateProductTree", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var runs = new List<double>();
        for (var run = 0; run < 4; run++)
        {
            var stopwatch = Stopwatch.StartNew();
            iterate.Invoke(service, [imports, nodes, products, categories, null, null, -1, true, null, null, false, null]);
            runs.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        // The stored comparers match the feed, so nothing may be saved, moved or deleted.
        Assert.Empty(contentService.Invocations);
        Assert.Empty(productsSaved);
        Assert.Empty(groupsSaved);
        Assert.Empty(variantsSaved);

        var items = imports.Count * (2 + variantsPerProduct);
        var best = runs.Skip(1).Min();
        _output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"IMPORTBENCH products={imports.Count} items={items} first={runs[0]:F0}ms best={best:F0}ms perItem={best * 1000 / items:F1}us"));
    }

    private static ImportProduct ProductImport(int category, int index, int variants) => new()
    {
        Identifier = $"p-{category}-{index}",
        NodeName = $"Product {category}-{index}",
        SKU = $"SKU-{category}-{index}",
        Vat = 24,
        Title = new() { ["is-IS"] = $"Vara {index}", ["en-US"] = $"Product {index}" },
        Slug = new() { ["is-IS"] = $"vara-{index}", ["en-US"] = $"product-{index}" },
        Description = new() { ["is-IS"] = "<p>Lysing a vorunni.</p>", ["en-US"] = "<p>A description of the product.</p>" },
        Categories = [$"cat-{category}"],
        Price = [new ImportPrice { StoreAlias = "Store", Currency = "ISK", Price = 1990 + index }],
        AdditionalProperties = new() { ["brand"] = "Acme", ["weight"] = 12.5 },
        VariantGroups =
        [
            new ImportVariantGroup
            {
                Identifier = $"g-{category}-{index}",
                NodeName = "Sizes",
                Title = new() { ["is-IS"] = "Staerdir", ["en-US"] = "Sizes" },
                Variants = Enumerable.Range(0, variants).Select(v => new ImportVariant
                {
                    Identifier = $"v-{category}-{index}-{v}",
                    NodeName = $"Variant {v}",
                    SKU = $"SKU-{category}-{index}-{v}",
                    Title = new() { ["is-IS"] = $"Afbrigdi {v}", ["en-US"] = $"Variant {v}" },
                    Price = [new ImportPrice { StoreAlias = "Store", Currency = "ISK", Price = 1990 + index }],
                }).ToList(),
            },
        ],
    };

    private IContent Node(IContentType type, IContent? parent, string identifier, string? comparer)
    {
        var id = _nextId++;
        var content = new Content(identifier, parent?.Id ?? -1, type)
        {
            Id = id,
            Key = Guid.NewGuid(),
            Path = parent == null ? $"-1,{id}" : $"{parent.Path},{id}",
            Level = (parent?.Level ?? 0) + 1,
        };
        content.SetValue(Configuration.ImportAliasIdentifier, identifier);
        if (comparer != null) content.SetValue("comparer", comparer);
        return content;
    }

    private static ContentType ContentTypeOf(IShortStringHelper helper, string alias)
    {
        var type = new ContentType(helper, -1) { Alias = alias, Name = alias };
        foreach (var property in new[] { Configuration.ImportAliasIdentifier, "comparer", "sku", "title", "images", "files" })
        {
            type.AddPropertyType(new PropertyType(helper, "Umbraco.TextBox", ValueStorageType.Nvarchar)
            {
                Alias = property,
                Name = property,
            });
        }

        return type;
    }

    private static void SetField(ImportService service, string name, object value)
        => typeof(ImportService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, value);

    private static int EnvInt(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;
}
#endif
