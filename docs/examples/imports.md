[Ekom docs](../README.md) / [Examples](README.md) / Import products from another system

# Import products from another system

These examples bring a catalog from an ERP system into Ekom with `Ekom.Services.IImportService`: categories, products, images, files, variants, prices, stock and warehouse stock. They also show how to update prices and stock fast, and how to run an import safely on a live site. [Catalog imports](../guides/imports.md) explains every operation and option; this page is the code.

All examples use one ERP feed format, defined in the first example, and one options class, `ErpImportOptions`. The composer that registers every class is in [Run imports in a background job](#run-imports-in-a-background-job).

On this page:

- [Prepare Umbraco for imports](#prepare-umbraco-for-imports)
- [Sync the whole catalog from an ERP feed](#sync-the-whole-catalog-from-an-erp-feed)
- [Import a product with images and files](#import-a-product-with-images-and-files)
- [Import a t-shirt with colors and sizes](#import-a-t-shirt-with-colors-and-sizes)
- [Update prices and stock without publishing](#update-prices-and-stock-without-publishing)
- [Import warehouse stock](#import-warehouse-stock)
- [Run imports in a background job](#run-imports-in-a-background-job)
- [Skip products that did not change](#skip-products-that-did-not-change)

## Prepare Umbraco for imports

Ekom does not add the properties the import writes to. Add them to the document and media types before the first import:

| Type | Alias | Editor | Why |
| --- | --- | --- | --- |
| `ekmCategory`, `ekmProduct`, `ekmProductVariantGroup`, `ekmProductVariant` | `ekmIdentifier` | Textstring | Ekom stores each item's `Identifier` here and finds the item by it on the next import. |
| The same four | `comparer` | Textstring | Ekom stores the change-detection value here. |
| The same four (optional) | `ekmDisableSync` | Toggle | When it is on, the import does not change, move or delete the node. `SyncProductMedia` and `SyncVariantMedia` still change it. |
| `ekmProduct`, `ekmProductVariant` | `files` | Media Picker, multiple items | Only when you import `Files`. |
| Media types `Image` and `File` | `comparer` | Textstring | Needed for media from a URL, bytes or base64. |
| Media types `Image` and `File` (optional) | `ekmIdentifier` | Textstring | Stores the `Identifier` of imported media. |
| Media types `Image` and `File` (optional) | `ekmSortOrder` | Numeric | Stores the `SortOrder` of imported media. |

- When `ekmIdentifier` or `comparer` is missing, saving the node or media item throws `InvalidOperationException` ("No PropertyType exists with the supplied alias").
- Use Textstring for `ekmIdentifier` and `comparer`. The single-item methods and the media lookup find items with a database query on the short-text column.
- Create a media folder for imported media, and note the keys of that folder and of the `ekmCatalog` node. The examples read them from `ErpImportOptions`.

## Sync the whole catalog from an ERP feed

The ERP system sends a flat list of categories and products. The mapper turns it into an `ImportData` tree, and `ErpCatalogImport` runs `FullSync` and reports the products that failed.

The feed format (replace it with your own):

```csharp
namespace MySite.ErpImport;

// The shape of the feed your ERP system sends. Replace it with your own.
public sealed class ErpCatalogFeed
{
    public List<ErpCategory> Categories { get; set; } = [];
    public List<ErpProduct> Products { get; set; } = [];
}

public sealed class ErpCategory
{
    public required string Code { get; set; }
    public string? ParentCode { get; set; }
    public int Position { get; set; }

    // Culture -> text, for example "en-US" and "de-DE".
    public Dictionary<string, string> Name { get; set; } = [];
    public Dictionary<string, string> Description { get; set; } = [];
}

public sealed class ErpProduct
{
    public required string ItemNo { get; set; }
    public required string Sku { get; set; }

    // The first code is the primary category.
    public List<string> CategoryCodes { get; set; } = [];

    public Dictionary<string, string> Name { get; set; } = [];
    public Dictionary<string, string> DescriptionHtml { get; set; } = [];

    public List<ErpPrice> Prices { get; set; } = [];
    public decimal OnHand { get; set; }
    public decimal VatPercent { get; set; }
    public bool? AllowBackorder { get; set; }

    public string? Brand { get; set; }
    public decimal? WeightKg { get; set; }
    public Dictionary<string, string> Material { get; set; } = [];

    public List<ErpImage> Images { get; set; } = [];
    public List<ErpWarehouseBalance> Warehouses { get; set; } = [];

    // Changes when names, texts or categories change in the ERP system,
    // but not when only the price or the stock changes.
    public long ContentVersion { get; set; }
}

public sealed class ErpPrice
{
    // "WEB" or "WHOLESALE".
    public required string PriceList { get; set; }

    // ISO code, for example "USD" or "EUR".
    public required string Currency { get; set; }
    public decimal Amount { get; set; }
}

public sealed class ErpImage
{
    public required string Id { get; set; }
    public required string Url { get; set; }
    public DateTime ChangedAt { get; set; }
    public int Position { get; set; }
}

public sealed class ErpWarehouseBalance
{
    // The warehouse code, as entered in the Ekom Warehouse Editor on the store node.
    public required string WarehouseCode { get; set; }
    public decimal? Quantity { get; set; }
}
```

The options, bound from appsettings in the composer:

```csharp
namespace MySite.ErpImport;

// Bound from the "ErpImport" section of appsettings.json.
public sealed class ErpImportOptions
{
    // The ekmCatalog node the import works under.
    public Guid CatalogRootKey { get; set; }

    // The media folder where imported images and files are created.
    public Guid MediaRootKey { get; set; }

    // Optional: a category node that removed products are moved to instead of being deleted.
    public Guid? RemovedProductsCategoryKey { get; set; }

    // The ID of the Umbraco user the changes are recorded on. -1 is the super user.
    public int SyncUserId { get; set; } = -1;

    // The key the ERP system sends in the X-Api-Key header.
    public string ApiKey { get; set; } = string.Empty;
}
```

The mapper. The site's own properties `brand`, `weightKg` and `material` go in `AdditionalProperties`:

```csharp
using Ekom.Models.Import;

namespace MySite.ErpImport;

public static class ErpCatalogMapper
{
    public static ImportData Map(ErpCatalogFeed feed, ErpImportOptions options)
    {
        return new ImportData
        {
            MediaRootKey = options.MediaRootKey,
            RecycleBinKey = options.RemovedProductsCategoryKey,
            Categories = BuildCategoryTree(feed.Categories, parentCode: null),
            Products = feed.Products.Select(MapProduct).ToList(),
        };
    }

    // The ERP sends a flat list with parent codes. Ekom wants a tree:
    // top categories in ImportData.Categories, the rest in SubCategories.
    private static List<ImportCategory> BuildCategoryTree(List<ErpCategory> all, string? parentCode)
    {
        return all
            .Where(c => c.ParentCode == parentCode)
            .OrderBy(c => c.Position)
            .Select(c => new ImportCategory
            {
                Identifier = "cat-" + c.Code,
                // Empty for a category directly under the catalog root.
                ParentIdentifier = c.ParentCode == null ? string.Empty : "cat-" + c.ParentCode,
                NodeName = c.Name.GetValueOrDefault("en-US", c.Code),
                Title = ToCultureValues(c.Name),
                Description = ToCultureValues(c.Description),
                SortOrder = c.Position,
                SubCategories = BuildCategoryTree(all, c.Code),
            })
            .ToList();
    }

    public static ImportProduct MapProduct(ErpProduct erp)
    {
        var product = new ImportProduct
        {
            Identifier = "item-" + erp.ItemNo,
            NodeName = erp.Name.GetValueOrDefault("en-US", erp.Sku),
            SKU = erp.Sku,
            Title = ToCultureValues(erp.Name),
            Description = ToCultureValues(erp.DescriptionHtml),
            Categories = erp.CategoryCodes.Select(code => "cat-" + code).ToList(),
            Vat = erp.VatPercent,                    // 24 means 24 %.
            EnableBackorder = erp.AllowBackorder,    // null keeps the value set in Umbraco.
            Stock =
            [
                new ImportStock { StoreAlias = "Store", Stock = erp.OnHand },
            ],
            AdditionalProperties = new Dictionary<string, object>
            {
                // Plain properties take the value their property editor stores.
                ["weightKg"] = erp.WeightKg ?? 0m,
                // An Ekom.Property (multilingual) property takes culture -> value.
                ["material"] = ToCultureValues(erp.Material),
            },
        };

        if (!string.IsNullOrWhiteSpace(erp.Brand))
        {
            product.AdditionalProperties["brand"] = erp.Brand;
        }

        foreach (var price in erp.Prices)
        {
            var storeAlias = price.PriceList switch
            {
                "WEB" => "Store",
                "WHOLESALE" => "Wholesale",
                _ => null,
            };

            // Ekom wants the store's currency value, not the ISO code.
            var currency = price.Currency switch
            {
                "USD" => "en-US",
                "EUR" => "de-DE",
                _ => null,
            };

            if (storeAlias == null || currency == null)
            {
                continue;
            }

            product.Price.Add(new ImportPrice
            {
                StoreAlias = storeAlias,
                Currency = currency,
                Price = price.Amount,
            });
        }

        foreach (var image in erp.Images)
        {
            product.Images.Add(new ImportMediaFromExternalUrl
            {
                Identifier = "erp-image-" + image.Id,
                Url = image.Url,
                FileName = Path.GetFileName(new Uri(image.Url).AbsolutePath),
                NodeName = erp.Sku + " " + (image.Position + 1),
                Date = image.ChangedAt,     // Part of the default comparer: a new date imports the image again.
                SortOrder = image.Position, // 0 is the first image.
            });
        }

        return product;
    }

    private static Dictionary<string, object> ToCultureValues(Dictionary<string, string> values)
    {
        return values.ToDictionary(x => x.Key, x => (object)x.Value);
    }
}
```

The import, called by the background job in [Run imports in a background job](#run-imports-in-a-background-job):

```csharp
using Ekom.Models.Import;
using Ekom.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MySite.ErpImport;

public sealed record ErpImportResult(int ProductCount, IReadOnlyList<ImportProduct> FailedProducts);

public sealed class ErpCatalogImport
{
    private readonly IImportService _importService;
    private readonly ErpImportOptions _options;
    private readonly ILogger<ErpCatalogImport> _logger;

    public ErpCatalogImport(
        IImportService importService,
        IOptions<ErpImportOptions> options,
        ILogger<ErpCatalogImport> logger)
    {
        _importService = importService;
        _options = options.Value;
        _logger = logger;
    }

    // Synchronous and slow: call it from a background job, never from a web request.
    public ErpImportResult RunFullSync(ImportData data)
    {
        _logger.LogInformation(
            "Starting ERP full sync with {CategoryCount} top categories and {ProductCount} products",
            data.Categories?.Count ?? 0,
            data.Products.Count);

        _importService.FullSync(data, _options.CatalogRootKey, _options.SyncUserId);

        // A product that failed to save keeps its exception; the import went on with the next one.
        var failed = data.Products.Where(p => p.Exception != null).ToList();

        foreach (var product in failed)
        {
            _logger.LogWarning(
                product.Exception,
                "ERP product {Identifier} ({Sku}) was not saved",
                product.Identifier,
                product.SKU);
        }

        return new ErpImportResult(data.Products.Count, failed);
    }
}
```

What to know:

- **FullSync deletes.** Treat the feed as the whole catalog:
  - A category that is not anywhere in the feed's category tree is deleted permanently, not moved to the Umbraco recycle bin, together with everything below it: subcategories, products, variant groups and variants. `RecycleBinKey` does not save them, and `ekmDisableSync` on a product below the category does not protect it. A product from a deleted category that is still in the feed is created again as a new node, with a new key.
  - A product that is not in the feed is deleted permanently. When `RecycleBinKey` is set (here `RemovedProductsCategoryKey`), it is unpublished and moved to that category instead, and published again when it comes back.
  - Variant groups and variants that are missing from a product in the feed are deleted permanently.
  - Media items are never deleted. Images and files that are no longer used stay in the media library.
- **FullSync overwrites.** When an item has changed, its title, description, summary, node name, extra categories, images, files and each `AdditionalProperties` value are written as sent. An empty `Description` or `Images` list clears the field. Editors' changes to those fields are lost the next time the ERP item changes. `SKU` and `Vat` are only written when they have a value, and `EnableBackorder` only when it is `true` or `false`. Properties you do not send are not touched.
- **Prices:** `Currency` is the store's currency value (`en-US`), not the ISO code (`USD`). With `Ekom:UseDatabasePrices` the prices go to the price table; otherwise they are stored on the node. See [Prices in database mode](../guides/imports.md#prices-in-database-mode).
- **Stock** in `Stock` replaces the stored count, also when the node did not change. `StoreAlias` counts only when `Ekom:PerStoreStock` is `true`.
- **Failures:** a product that fails to save gets its exception in `ImportProduct.Exception`, and the import goes on with the next product. A category or variant that fails stops the import. A product whose categories are not found is skipped with a log warning and no exception, so it is not in the failed list. See [Errors and failures](../guides/imports.md#errors-and-failures).
- FullSync throws `ArgumentException` and changes no products when no product in the feed has a category. Category changes made before that stay.
- Run the feed against a copy of the production database first, and check what it deletes.

## Import a product with images and files

`ProductSync` creates or updates one product with its media. This mug uses every kind of media: a URL that Ekom downloads, base64 text, bytes, and an existing media item by its UDI. `ReplaceMugImages` changes only the images of the product.

```csharp
using System.Security.Cryptography;
using System.Text;
using Ekom.Models.Import;
using Ekom.Services;
using Microsoft.Extensions.Options;

namespace MySite.ErpImport;

public sealed class MugMediaImport
{
    private readonly IImportService _importService;
    private readonly ErpImportOptions _options;

    public MugMediaImport(IImportService importService, IOptions<ErpImportOptions> options)
    {
        _importService = importService;
        _options = options.Value;
    }

    public void ImportMug(byte[] careGuidePdf, string lifestylePhotoBase64, Guid brandBadgeMediaKey)
    {
        var product = new ImportProduct
        {
            Identifier = "item-10045",
            NodeName = "Ceramic mug",
            SKU = "MUG-WHITE",
            Title = new Dictionary<string, object>
            {
                ["en-US"] = "Ceramic mug",
                ["de-DE"] = "Keramikbecher",
            },
            Categories = ["cat-KITCHEN"],
            Price =
            [
                new ImportPrice { StoreAlias = "Store", Currency = "en-US", Price = 14.90m },
            ],
            Images =
            [
                // Downloaded by Ekom. SortOrder 0 makes it the first image.
                new ImportMediaFromExternalUrl
                {
                    Identifier = "erp-image-10045-front",
                    Url = "https://erp.example.com/media/10045/front.jpg",
                    FileName = "mug-white-front.jpg",
                    NodeName = "MUG-WHITE front",
                    Date = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
                    SortOrder = 0,
                },

                // Sent as base64 text, for example from a JSON feed.
                new ImportMediaFromBase64
                {
                    Base64 = lifestylePhotoBase64,
                    // The default comparer leaves out the content. A hash of it makes
                    // a changed photo a new media item; an unchanged one is reused.
                    Comparer = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(lifestylePhotoBase64))),
                    FileName = "mug-white-lifestyle.jpg",
                    NodeName = "MUG-WHITE lifestyle",
                    SortOrder = 1,
                },

                // An image that already exists under the media root, picked by its UDI.
                new ImportMediaFromUdi
                {
                    Udi = "umb://media/" + brandBadgeMediaKey.ToString("N"),
                    SortOrder = 2,
                },
            ],
            Files =
            [
                // Sent as bytes, with a hash of the bytes as its comparer.
                new ImportMediaFromBytes
                {
                    Bytes = careGuidePdf,
                    Comparer = Convert.ToHexString(SHA256.HashData(careGuidePdf)),
                    FileName = "mug-care-guide.pdf",
                    NodeName = "MUG-WHITE care guide",
                    SortOrder = 0,
                },
            ],
        };

        _importService.ProductSync(
            product,
            parentKey: _options.CatalogRootKey,
            mediaRootKey: _options.MediaRootKey,
            syncUser: _options.SyncUserId);
    }

    // Replace the images of the product and change nothing else.
    public void ReplaceMugImages(Guid newFrontMediaKey, Guid newBackMediaKey)
    {
        List<IImportMedia> images =
        [
            new ImportMediaFromUdi { Udi = "umb://media/" + newFrontMediaKey.ToString("N"), SortOrder = 0 },
            new ImportMediaFromUdi { Udi = "umb://media/" + newBackMediaKey.ToString("N"), SortOrder = 1 },
        ];

        _importService.SyncProductMedia(
            "item-10045",
            images,
            _options.MediaRootKey,
            ImportMediaTypes.Image,
            ImportMediaContentTypes.images,
            replaceExisting: true,
            syncUser: _options.SyncUserId);
    }
}
```

What to know:

- **Where media goes:** Ekom creates imported media in numbered subfolders (`1`, `2`, ...) of the media folder you pass as `mediaRootKey`, 400 items per folder. `ImportMediaTypes.Image` creates an `Image` media item, `ImportMediaTypes.File` a `File` media item.
- **Reuse:** Ekom finds media it imported before by `Identifier`, or else by `Comparer`. When you leave `Comparer` empty, Ekom computes one from the item's values without the file content, so for bytes and base64 a changed file with the same name is not noticed. Set `Comparer` to a hash of the content, as here. For a URL the default comparer includes `Url`, `FileName`, `NodeName` and `Date`, so a new `Date` imports the image again. A media item found by its `Identifier` is reused as it is; for bytes and base64 the file is not replaced.
- **UDI media** must be an `Image` or `File` media item under the media folder you pass. Other media is dropped from the field without an error.
- **Order:** Ekom has no separate main image. Storefront code uses the first image, for example `product.Images.FirstOrDefault()`. Without an `ekmSortOrder` property on the media types, the field follows the `SortOrder` you send. With that property, Ekom stores `SortOrder` on the media item it creates and orders the field by the stored values; media without a value goes last. The stored value belongs to the media item, so media shared by several products has one order.
- **The field is replaced:** in `ProductSync` and `FullSync` the images field afterwards holds only the media in `Images`. An empty list removes all images. `SyncProductMedia` merges unless you pass `replaceExisting: true`.
- **`PreserveExistingValues = true`:** when the field already has any media, Ekom leaves it as it is and ignores the incoming media; only an empty field is filled. On an existing product it also leaves the title, summary, description and extra categories as they are, even when the payload has new values.
- **A failed download:** when the URL returns an error status or cannot be reached, Ekom logs an error and leaves that image out; the product is saved without it. Other errors, such as a timeout or invalid base64, fail the product (`ImportProduct.Exception`). In a category, variant group or variant they stop the import. With `replaceExisting: true`, any failure throws before the field is changed.
- **Not one transaction:** media items are created before the product is saved. If the product save fails, they stay.
- `ProductUpdateSync` and `VariantUpdateSync` do not touch media. Use `ProductSync`, `VariantSync` or `SyncProductMedia`.

See [Media](../guides/imports.md#media) for the rules of `SyncProductMedia` and `SyncVariantMedia`.

## Import a t-shirt with colors and sizes

In Ekom a variant group is one choice of the first option, usually a color, and its variants are the second option, usually sizes. This t-shirt has a group for each color, a variant for each size, and its own SKU, price, stock and image on each variant. `UpdateRedLarge` then changes one variant.

```csharp
using Ekom.Models.Import;
using Ekom.Services;
using Microsoft.Extensions.Options;

namespace MySite.ErpImport;

public sealed class TshirtImport
{
    private static readonly string[] Colors = ["Red", "Blue"];
    private static readonly string[] Sizes = ["S", "M", "L"];

    private readonly IImportService _importService;
    private readonly ErpImportOptions _options;

    public TshirtImport(IImportService importService, IOptions<ErpImportOptions> options)
    {
        _importService = importService;
        _options = options.Value;
    }

    // Creates or updates the product, both color groups and all six variants.
    public void ImportTshirt(IReadOnlyDictionary<string, decimal> stockBySku)
    {
        var product = new ImportProduct
        {
            Identifier = "item-20010",
            NodeName = "Organic t-shirt",
            SKU = "TSHIRT",
            Title = new Dictionary<string, object>
            {
                ["en-US"] = "Organic t-shirt",
                ["de-DE"] = "Bio-T-Shirt",
            },
            Categories = ["cat-TSHIRTS", "cat-NEW"],
            Vat = 24m,
            Price =
            [
                new ImportPrice { StoreAlias = "Store", Currency = "en-US", Price = 25m },
                new ImportPrice { StoreAlias = "Store", Currency = "de-DE", Price = 23m },
            ],
        };

        for (var i = 0; i < Colors.Length; i++)
        {
            var color = Colors[i];

            // In Ekom a variant group is one color; its variants are the sizes.
            var group = new ImportVariantGroup
            {
                Identifier = "item-20010-" + color.ToLowerInvariant(),
                NodeName = color,
                Title = new Dictionary<string, object>
                {
                    ["en-US"] = color,
                    ["de-DE"] = color == "Red" ? "Rot" : "Blau",
                },
                SortOrder = i,
                Images =
                [
                    new ImportMediaFromExternalUrl
                    {
                        Identifier = "erp-image-20010-" + color.ToLowerInvariant(),
                        Url = $"https://erp.example.com/media/20010/{color.ToLowerInvariant()}.jpg",
                        FileName = $"tshirt-{color.ToLowerInvariant()}.jpg",
                        NodeName = "TSHIRT " + color,
                        SortOrder = 0,
                    },
                ],
            };

            for (var j = 0; j < Sizes.Length; j++)
            {
                var sku = $"TSHIRT-{color.ToUpperInvariant()}-{Sizes[j]}";
                var priceUsd = Sizes[j] == "L" ? 27m : 25m;
                group.Variants.Add(BuildVariant(sku, color, Sizes[j], j, priceUsd, stockBySku.GetValueOrDefault(sku)));
            }

            product.VariantGroups.Add(group);
        }

        // Groups and variants that exist under this product but are not in the payload are deleted.
        _importService.ProductSync(
            product,
            parentKey: _options.CatalogRootKey,
            mediaRootKey: _options.MediaRootKey,
            syncUser: _options.SyncUserId);
    }

    // Changes one variant that exists. Other variants, the group and the product are not touched.
    public void UpdateRedLarge(decimal newPrice, decimal newStock)
    {
        // Build the whole variant, as in the full payload, not only the changed values:
        // when the variant has changed, its title and description are written as sent.
        var variant = BuildVariant("TSHIRT-RED-L", "Red", "L", 2, newPrice, newStock);

        _importService.VariantUpdateSync(variant, _options.CatalogRootKey, _options.SyncUserId);
    }

    private static ImportVariant BuildVariant(
        string sku, string color, string size, int position, decimal priceUsd, decimal stock)
    {
        return new ImportVariant
        {
            Identifier = "variant-" + sku,
            NodeName = size,
            SKU = sku,
            Title = new Dictionary<string, object>
            {
                ["en-US"] = size,
                ["de-DE"] = size,
            },
            SortOrder = position,
            Price =
            [
                new ImportPrice { StoreAlias = "Store", Currency = "en-US", Price = priceUsd },
                new ImportPrice { StoreAlias = "Store", Currency = "de-DE", Price = priceUsd - 2m },
            ],
            Stock =
            [
                new ImportStock { StoreAlias = "Store", Stock = stock },
            ],
            EnableBackorder = false,
            Images =
            [
                new ImportMediaFromExternalUrl
                {
                    Identifier = "erp-image-" + sku,
                    Url = $"https://erp.example.com/media/20010/{color.ToLowerInvariant()}-{size.ToLowerInvariant()}.jpg",
                    FileName = $"{sku.ToLowerInvariant()}.jpg",
                    NodeName = sku,
                    SortOrder = 0,
                },
            ],
        };
    }
}
```

In a full sync, put the groups in `VariantGroups` of the product in the feed in the same way.

What to know:

- `ProductSync` deletes the variant groups of this product that are not in `VariantGroups`, and the variants that are not in their group's `Variants`. Always send all groups and variants.
- `VariantUpdateSync` finds the variant by `Identifier` under the root you pass and updates its values, stock and prices. It does not create the variant, does not change media, and throws `ArgumentNullException` when no variant has that identifier. Build the whole variant: if the content changed, the title and description are written as sent.
- To add or change one variant and its images without the rest of the product, use `VariantSync(variant, variantGroupKey, mediaRootKey, syncUser)`. `VariantGroupSync` deletes the product's other variant groups. See [Synchronize variant groups and variants](../guides/imports.md#synchronize-variant-groups-and-variants).
- `ProductSync` finds an existing product only under its primary category (the first of `Categories`). If the product lives in another category, it creates a second product. Set `PreservePrimaryCategory = true` or use `FullSync` to move products.
- A variant that fails to save stops the import. Products, groups and variants saved before it stay saved.
- On variant groups and variants, every `AdditionalProperties` alias must exist on the document type, or the save throws. On products and categories, unknown aliases are skipped.

## Update prices and stock without publishing

ERP systems often send prices and stock many times a day. Writing them through `Ekom.API.Stock` and `Ekom.API.Prices` saves no node and publishes nothing.

```csharp
using Ekom.API;
using Ekom.Models;
using Microsoft.Extensions.Logging;

namespace MySite.ErpImport;

public sealed record ErpStockLine(string Sku, decimal OnHand);

public sealed record ErpPriceLine(string Sku, string StoreAlias, string Currency, decimal Amount);

public sealed class ErpQuickUpdates
{
    private readonly Catalog _catalog;
    private readonly Stock _stock;
    private readonly Prices _prices;
    private readonly ILogger<ErpQuickUpdates> _logger;

    public ErpQuickUpdates(Catalog catalog, Stock stock, Prices prices, ILogger<ErpQuickUpdates> logger)
    {
        _catalog = catalog;
        _stock = stock;
        _prices = prices;
        _logger = logger;
    }

    // Replaces the stock count of every SKU in the list. No node is saved or published.
    public async Task SetStockAsync(IReadOnlyList<ErpStockLine> lines, CancellationToken ct)
    {
        var requests = new List<StockSetRequest>(lines.Count);

        foreach (var line in lines)
        {
            var key = await FindKeyAsync(line.Sku, "Store", ct).ConfigureAwait(false);
            if (key == null)
            {
                _logger.LogWarning("Stock for unknown SKU {Sku} was skipped", line.Sku);
                continue;
            }

            requests.Add(new StockSetRequest(key.Value, "Store", Math.Max(line.OnHand, 0m)));
        }

        // Written in batches of 200, one transaction per batch.
        await _stock.SetStockAsync(requests, ct).ConfigureAwait(false);
    }

    // Writes prices to the EkomPrice table. Needs Ekom:UseDatabasePrices = true.
    public async Task SetPricesAsync(IReadOnlyList<ErpPriceLine> lines, CancellationToken ct)
    {
        var mutationsByNode = new Dictionary<Guid, List<PriceMutation>>();

        foreach (var line in lines)
        {
            var key = await FindKeyAsync(line.Sku, line.StoreAlias, ct).ConfigureAwait(false);
            if (key == null)
            {
                _logger.LogWarning("Price for unknown SKU {Sku} was skipped", line.Sku);
                continue;
            }

            if (!mutationsByNode.TryGetValue(key.Value, out var mutations))
            {
                mutations = [];
                mutationsByNode[key.Value] = mutations;
            }

            mutations.Add(new PriceMutation(key.Value, line.StoreAlias, line.Currency, line.Amount));
        }

        // Read the current rows once, then skip the nodes whose prices are already right.
        _prices.Refresh(mutationsByNode.Keys);

        foreach (var (nodeKey, mutations) in mutationsByNode)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // IsCurrent checks memory only. ApplyAsync writes all prices of one node in one transaction.
                if (!_prices.IsCurrent(mutations))
                {
                    await _prices.ApplyAsync(mutations, ct).ConfigureAwait(false);
                }
            }
            catch (ArgumentException ex)
            {
                // Invalid amount, store or currency: this node is left unchanged, the rest go on.
                _logger.LogWarning(ex, "Prices for node {NodeKey} were rejected", nodeKey);
            }
        }
    }

    private async Task<Guid?> FindKeyAsync(string sku, string storeAlias, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        // Reads Ekom's in-memory catalog. raiseEvent: false, so your catalog event handlers do not hide items.
        var product = await _catalog.GetProductAsync(sku, storeAlias, raiseEvent: false, ct: ct).ConfigureAwait(false);
        if (product != null)
        {
            return product.Key;
        }

        var variant = await _catalog.GetVariantAsync(sku, storeAlias, ct).ConfigureAwait(false);
        return variant?.Key;
    }
}
```

Which way to choose:

| Change | Use | Saves or publishes nodes |
| --- | --- | --- |
| Stock only | `Stock.SetStockAsync(IEnumerable<StockSetRequest>)`, as in `SetStockAsync` above | No |
| Prices only, with `Ekom:UseDatabasePrices` set to `true` | `Prices.ApplyAsync`, as in `SetPricesAsync` above | No |
| Prices only, with node prices | `ProductUpdateSync` or `VariantUpdateSync` with the full payload | Yes: the price is part of the node, so every changed product is saved and published |
| Prices, stock and texts from one message | `ProductUpdateSync` or `VariantUpdateSync` with the full payload | Only when the content changed. With database prices, a price change alone does not save the node |

What to know:

- `SetStockAsync` replaces counts. It does not take open reservations into account, and it writes in batches of 200, each its own transaction. If a later batch fails, the earlier batches stay. See [Stock](../guides/stock.md#setting-many-values-at-once).
- `ApplyAsync` writes all prices of one node in one transaction. Another node's prices are separate, so a failure halfway leaves the first nodes updated. `SetPricesAsync` throws `InvalidOperationException` when database prices are off. See [Database price storage](../guides/database-price-storage.md#skip-unchanged-prices-in-bulk-writes).
- `FindKeyAsync` looks SKUs up in Ekom's catalog, which holds published products and variants only. A product that is not published is skipped.
- Never call `ProductUpdateSync` with a partial payload, such as an identifier and a price only. When the content check sees a change, the empty title and description are saved over the real ones.
- Price writes are seen at once on the server that made them. Other servers need `prices.FillCacheAsync()`. See [Caching and several servers](../guides/database-price-storage.md#caching-and-several-servers).

## Import warehouse stock

When the ERP system sends a balance per warehouse, map each warehouse code to the warehouse key that Ekom knows for the store. Use `MapAsync` in place of `ErpCatalogMapper.Map` in the job.

```csharp
using Ekom.API;
using Ekom.Models;
using Ekom.Models.Import;

namespace MySite.ErpImport;

public sealed class ErpWarehouseMapper
{
    private readonly Warehouse _warehouse;

    public ErpWarehouseMapper(Warehouse warehouse)
    {
        _warehouse = warehouse;
    }

    // Use this in place of ErpCatalogMapper.Map when the feed has warehouse balances.
    public async Task<ImportData> MapAsync(ErpCatalogFeed feed, ErpImportOptions options, CancellationToken ct)
    {
        IReadOnlyList<WarehouseDefinition> warehouses =
            await _warehouse.GetWarehousesAsync("Store", ct).ConfigureAwait(false);
        var warehouseKeys = warehouses.ToDictionary(w => w.Code, w => w.Key, StringComparer.OrdinalIgnoreCase);

        var data = ErpCatalogMapper.Map(feed, options);

        // Map keeps the order of feed.Products.
        for (var i = 0; i < feed.Products.Count; i++)
        {
            AddWarehouseStock(data.Products[i], feed.Products[i], warehouseKeys);
        }

        return data;
    }

    private static void AddWarehouseStock(
        ImportProduct product,
        ErpProduct erp,
        IReadOnlyDictionary<string, Guid> warehouseKeys)
    {
        foreach (var balance in erp.Warehouses)
        {
            if (!warehouseKeys.TryGetValue(balance.WarehouseCode, out var warehouseKey))
            {
                // Not set up on the store node: an entry for it would fail the product.
                continue;
            }

            product.WarehouseStock.Add(new ImportWarehouseStock
            {
                StoreAlias = "Store",
                WarehouseKey = warehouseKey,
                // No quantity from the ERP means the item is no longer kept in that warehouse.
                Clear = balance.Quantity == null,
                Balance = Math.Max(balance.Quantity ?? 0m, 0m),
            });
        }
    }
}
```

What to know:

- Warehouse stock is for display and integrations. It does not change what customers can buy; `Stock` does. See [Warehouse stock](../guides/stock.md#warehouse-stock).
- Only the warehouses you send change. `Clear = true` removes the balance; otherwise `Balance` is set. A balance cannot be negative.
- Ekom stores warehouse stock by `SKU`. A product or variant without `SKU` in the payload gets no warehouse stock.
- `GetWarehousesAsync` leaves out warehouses that are hidden from the storefront. To import to a hidden warehouse, keep its key in your configuration.
- If one entry fails, the other entries are saved and then the product fails with `InvalidOperationException`, and its content is not saved. On a variant, the failure stops the import. That is why the mapper skips codes it does not know.

## Run imports in a background job

`IImportService` is synchronous and has no HTTP endpoint. Here the ERP system posts its feed to an endpoint the site owns, protected by an API key. The endpoint queues the feed and answers `202 Accepted` at once; a hosted service runs the import, one at a time, and the ERP system reads the result from the status endpoint.

The queue, the lock and the status:

```csharp
using System.Threading.Channels;

namespace MySite.ErpImport;

public sealed class ErpImportQueue
{
    // Room for one waiting feed. A feed that arrives while one is waiting is refused.
    private readonly Channel<ErpCatalogFeed> _channel = Channel.CreateBounded<ErpCatalogFeed>(
        new BoundedChannelOptions(1)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    // Every job in this application that calls IImportService waits for this gate first.
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public bool TryEnqueue(ErpCatalogFeed feed) => _channel.Writer.TryWrite(feed);

    public IAsyncEnumerable<ErpCatalogFeed> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed class ErpImportStatus
{
    private readonly object _lock = new();

    public string State { get; private set; } = "Idle";
    public DateTime? StartedUtc { get; private set; }
    public DateTime? FinishedUtc { get; private set; }
    public int ProductsSaved { get; private set; }
    public IReadOnlyList<string> FailedSkus { get; private set; } = [];
    public string? Error { get; private set; }

    public void Started()
    {
        lock (_lock)
        {
            State = "Running";
            StartedUtc = DateTime.UtcNow;
            FinishedUtc = null;
            ProductsSaved = 0;
            FailedSkus = [];
            Error = null;
        }
    }

    public void Saved(int productsSaved)
    {
        lock (_lock)
        {
            ProductsSaved = productsSaved;
        }
    }

    public void Finished(ErpImportResult result)
    {
        lock (_lock)
        {
            State = result.FailedProducts.Count == 0 ? "Succeeded" : "SucceededWithErrors";
            FinishedUtc = DateTime.UtcNow;
            FailedSkus = result.FailedProducts.Select(p => p.SKU ?? p.Identifier).ToList();
        }
    }

    public void Failed(string error)
    {
        lock (_lock)
        {
            State = "Failed";
            FinishedUtc = DateTime.UtcNow;
            Error = error;
        }
    }
}
```

The endpoint:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Sync;

namespace MySite.ErpImport;

[ApiController]
[Route("api/erp/catalog-import")]
public sealed class ErpImportController : ControllerBase
{
    private readonly ErpImportQueue _queue;
    private readonly ErpImportStatus _status;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly ErpImportOptions _options;

    public ErpImportController(
        ErpImportQueue queue,
        ErpImportStatus status,
        IServerRoleAccessor serverRoleAccessor,
        IOptions<ErpImportOptions> options)
    {
        _queue = queue;
        _status = status;
        _serverRoleAccessor = serverRoleAccessor;
        _options = options.Value;
    }

    [HttpPost]
    [RequestSizeLimit(200_000_000)]
    public IActionResult Start([FromBody] ErpCatalogFeed feed)
    {
        if (!HasValidApiKey())
        {
            return Unauthorized();
        }

        // Imports run on one server only: the single server or the scheduling publisher.
        if (_serverRoleAccessor.CurrentServerRole == ServerRole.Subscriber)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Send imports to the backoffice server.");
        }

        if (!_queue.TryEnqueue(feed))
        {
            return Conflict("An import is already waiting. Try again when it has started.");
        }

        return Accepted();
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        if (!HasValidApiKey())
        {
            return Unauthorized();
        }

        return Ok(_status);
    }

    private bool HasValidApiKey()
    {
        if (string.IsNullOrEmpty(_options.ApiKey)
            || !Request.Headers.TryGetValue("X-Api-Key", out var sent))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(sent.ToString()),
            Encoding.UTF8.GetBytes(_options.ApiKey));
    }
}
```

The worker:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MySite.ErpImport;

public sealed class ErpImportWorker : BackgroundService
{
    private readonly ErpImportQueue _queue;
    private readonly ErpImportStatus _status;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErpImportWorker> _logger;

    public ErpImportWorker(
        ErpImportQueue queue,
        ErpImportStatus status,
        IServiceScopeFactory scopeFactory,
        ILogger<ErpImportWorker> logger)
    {
        _queue = queue;
        _status = status;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var feed in _queue.ReadAllAsync(stoppingToken))
        {
            await _queue.Gate.WaitAsync(stoppingToken);
            try
            {
                _status.Started();

                using var scope = _scopeFactory.CreateScope();
                var options = scope.ServiceProvider.GetRequiredService<IOptions<ErpImportOptions>>().Value;
                var import = scope.ServiceProvider.GetRequiredService<ErpCatalogImport>();

                var data = ErpCatalogMapper.Map(feed, options);

                // FullSync is synchronous and runs for minutes: give it its own thread.
                // It cannot be cancelled once it has started.
                var result = await Task.Factory.StartNew(
                    () => import.RunFullSync(data),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

                _status.Finished(result);
                _logger.LogInformation(
                    "ERP full sync finished: {ProductCount} products in the feed, {FailedCount} failed",
                    result.ProductCount,
                    result.FailedProducts.Count);
            }
            catch (Exception ex)
            {
                // Categories, products and media saved before the failure stay saved.
                _logger.LogError(ex, "ERP full sync failed");
                _status.Failed(ex.Message);
            }
            finally
            {
                _queue.Gate.Release();
            }
        }
    }
}
```

Import event handlers. They skip new products without a price, keep images that an editor chose, and record the result when the sync finishes:

```csharp
using Ekom.Events;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;

namespace MySite.ErpImport;

public sealed class ErpImportEventsComponent : IAsyncComponent
{
    private readonly ErpImportStatus _status;
    private readonly ILogger<ErpImportEventsComponent> _logger;

    public ErpImportEventsComponent(ErpImportStatus status, ILogger<ErpImportEventsComponent> logger)
    {
        _status = status;
        _logger = logger;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        ImportEvents.ProductImportEvaluating += OnProductImportEvaluatingAsync;
        ImportEvents.ProductSaveStarting += OnProductSaveStartingAsync;
        ImportEvents.SyncFinished += OnSyncFinishedAsync;
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        ImportEvents.ProductImportEvaluating -= OnProductImportEvaluatingAsync;
        ImportEvents.ProductSaveStarting -= OnProductSaveStartingAsync;
        ImportEvents.SyncFinished -= OnSyncFinishedAsync;
        return Task.CompletedTask;
    }

    // Skip: do not create products that have no price yet.
    private Task OnProductImportEvaluatingAsync(ImportProductEvaluatingEventArgs args)
    {
        // Only new products (no existing node). In a full sync, an existing product
        // that you exclude is treated as missing and removed.
        if (args.ProductContent == null && args.ImportProduct.Price.Count == 0)
        {
            _logger.LogInformation(
                "ERP product {Identifier} has no price and was not created",
                args.ImportProduct.Identifier);
            args.ExcludeFromImport = true;
        }

        return Task.CompletedTask;
    }

    // Adjust: keep the images an editor chose by hand.
    private Task OnProductSaveStartingAsync(ImportProductEventArgs args)
    {
        // "keepEditorImages" is a true/false property you add to the ekmProduct document type.
        if (!args.IsCreateOperation && args.ProductContent.GetValue<bool>("keepEditorImages"))
        {
            args.ImagesHaveNoChanges = true;
        }

        return Task.CompletedTask;
    }

    // Run something when the import is done.
    private Task OnSyncFinishedAsync(ImportSyncFinishedEventArgs args)
    {
        if (args.Type != ImportSyncType.FullSync)
        {
            return Task.CompletedTask;
        }

        // ImportProducts holds the products that were saved and the ones that failed.
        var saved = args.ImportProducts.Count(p => p.Exception == null);
        _status.Saved(saved);

        _logger.LogInformation(
            "ERP full sync saved {CategoryCount} categories, {ProductCount} products, {VariantCount} variants",
            args.ImportCategories.Count,
            saved,
            args.ImportVariants.Count);

        return Task.CompletedTask;
    }
}
```

Register everything in a composer:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MySite.ErpImport;

public sealed class ErpImportComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<ErpImportOptions>(builder.Config.GetSection("ErpImport"));

        builder.Services.AddSingleton<ErpImportQueue>();
        builder.Services.AddSingleton<ErpImportStatus>();
        builder.Services.AddTransient<ErpCatalogImport>();
        builder.Services.AddTransient<ErpWarehouseMapper>();
        builder.Services.AddTransient<ErpQuickUpdates>();
        builder.Services.AddTransient<MugMediaImport>();
        builder.Services.AddTransient<TshirtImport>();
        builder.Services.AddTransient<ErpResave>();
        builder.Services.AddHostedService<ErpImportWorker>();

        builder.Components().Append<ErpImportEventsComponent>();
    }
}
```

And add the settings to `appsettings.json`. Keep the API key out of source control, for example in an environment variable `ErpImport__ApiKey`:

```json
{
  "ErpImport": {
    "CatalogRootKey": "6b1f8a4e-2c7d-4e0a-9a51-0f3c2d8e7b10",
    "MediaRootKey": "0d5e9c2a-71b4-4f86-8c3e-5a2b9f4d1e77",
    "RemovedProductsCategoryKey": "c4a7e2d9-5b18-4c63-b0f2-9e1d7a3c6f45",
    "SyncUserId": -1,
    "ApiKey": ""
  }
}
```

What to know:

- **One import at a time.** The queue has one reader, and every job that calls `IImportService` should wait for `ErpImportQueue.Gate`. Ekom's own guard covers only `FullSync`, `CategorySync` and `MoveSync`, and only in one application: a second one throws `InvalidOperationException("Sync is already in progress.")`. Neither works across servers, so the endpoint refuses to run on a `Subscriber` server. Point the ERP system at your backoffice server.
- **How long it blocks:** `FullSync` first loads every node under the catalog root and every image and file under the media folder into memory, then handles the items one after another on the calling thread. Each changed node is saved and published on its own. Unchanged items cost only a comparison. The call returns only when everything is done, and it cannot be cancelled. Ekom logs `Import sync progress: N%` while it runs. Measure a full import on a copy of the production database before you plan its schedule.
- **Not transactional:** every node save, media item, stock batch and price write is its own transaction. When the import fails or the application stops halfway, the work done so far stays, and the catalog is partly updated. Run the same feed again: unchanged items are skipped. The worker sets the status to `Failed`.
- **Shutting down:** the host waits only for its shutdown timeout. An import that is still running is stopped with the process, halfway.
- **The sync user** is the Umbraco user ID recorded on every change. `-1` is the super user. Create a user named for the integration if you want changes to show who made them.
- **Handlers:** handlers run one after another, inside the import. If `ProductImportEvaluating` throws, the import stops before any product changes. If `ProductSaveStarting` throws, that product fails. If `SyncFinished` throws, `FullSync` throws after all the work is done. The events are static: they fire for every import in the application, not only this job. See [Import events](../guides/imports.md#import-events).
- **Excluding in a full sync:** an existing product that `ProductImportEvaluating` excludes is treated as missing, so it is moved to the removed-products category or deleted. The handler here excludes only new products (`ProductContent == null`).
- `SyncFinished` lists the products that were saved and the ones that failed. Products that had not changed are not in the list.
- The controller is a normal ASP.NET Core API controller with attribute routing. With a large feed, raise `RequestSizeLimit` or let the job download the feed from the ERP system instead.
- After a large import, check that every server shows the new catalog. See [Refresh the caches](../guides/stores.md#refresh-the-caches).

[Use Ekom outside a web request](background-jobs.md) has more about hosted services and queue workers.

## Skip products that did not change

Ekom saves a node only when its comparer changed. By default the comparer is a hash of the import item. A comparer from the ERP system's own version number is cheaper to check, and you control when it changes.

```csharp
using System.Globalization;

namespace MySite.ErpImport;

public static class ErpComparer
{
    // Raise this number when you change how ErpCatalogMapper maps fields.
    // Every product then has a new comparer and is saved once on the next import.
    private const int MappingVersion = 3;

    public static string Build(ErpProduct erp, bool databasePrices)
    {
        // ContentVersion changes when texts or categories change in the ERP system.
        var comparer = $"v{MappingVersion}:{erp.ContentVersion}";

        if (!databasePrices)
        {
            // Node prices are only written when the node is saved,
            // so a price change must change the comparer.
            comparer += ":" + string.Join(
                ",",
                erp.Prices
                    .OrderBy(p => p.PriceList, StringComparer.Ordinal)
                    .ThenBy(p => p.Currency, StringComparer.Ordinal)
                    .Select(p => $"{p.PriceList}/{p.Currency}/{p.Amount.ToString(CultureInfo.InvariantCulture)}"));
        }

        return comparer;
    }
}
```

Use it when you map, and use `forceUpdate` to save one product even when nothing changed:

```csharp
using Ekom;
using Ekom.Models.Import;
using Ekom.Services;
using Microsoft.Extensions.Options;

namespace MySite.ErpImport;

public sealed class ErpResave
{
    private readonly IImportService _importService;
    private readonly Configuration _ekomConfiguration;
    private readonly ErpImportOptions _options;

    public ErpResave(IImportService importService, Configuration ekomConfiguration, IOptions<ErpImportOptions> options)
    {
        _importService = importService;
        _ekomConfiguration = ekomConfiguration;
        _options = options.Value;
    }

    public ImportProduct MapWithComparer(ErpProduct erp)
    {
        var product = ErpCatalogMapper.MapProduct(erp);
        product.Comparer = ErpComparer.Build(erp, _ekomConfiguration.UseDatabasePrices);
        return product;
    }

    // Saves one product, its variant groups and variants even when nothing changed.
    public void Resave(ErpProduct erp)
    {
        _importService.ProductSync(
            MapWithComparer(erp),
            parentKey: _options.CatalogRootKey,
            mediaRootKey: _options.MediaRootKey,
            syncUser: _options.SyncUserId,
            forceUpdate: true);
    }
}
```

What to know:

- Ekom saves a node when the comparer changed, when the node is new, or when its images or files changed. Stock is written in every case, and so are prices with database prices.
- Leave prices out of the comparer only when `Ekom:UseDatabasePrices` is `true`. With node prices, a price is written only when the node is saved, so a comparer that ignores prices never saves a new price.
- Raise `MappingVersion` when you change the mapping. Every product is then saved once on the next import. `FullSync` has no `forceUpdate` option.
- `forceUpdate: true` on `ProductSync` and `ProductUpdateSync` saves the product, its variant groups and its variants without comparing. Each saved node is also published, so do not use it for the whole catalog on every run.
- With the default hash, keep the payload stable: a value that differs on every run, such as `UpdateDate = DateTime.UtcNow`, changes the hash and saves every node every time. `EventProperties`, media, stock and warehouse stock are not part of the hash.
- Categories, variant groups and variants have their own `Comparer`. This example sets it for products only.

See [Change detection](../guides/imports.md#change-detection).

## Related pages

- [Catalog imports](../guides/imports.md): every import operation, option and rule behind these examples.
- [Database price storage](../guides/database-price-storage.md): keep prices in a table and change them without publishing.
- [Stock](../guides/stock.md): sellable stock, bulk stock updates and warehouse stock.
- [Events](../reference/events.md#import-events): the import events and their arguments.
- [Use Ekom outside a web request](background-jobs.md): hosted services and queue workers that call Ekom.

[Back to Examples](README.md)
