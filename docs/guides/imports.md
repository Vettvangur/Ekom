[Ekom docs](../README.md) / [Guides](README.md) / Catalog imports

# Catalog imports

The import service brings catalog data from another system, such as an ERP or PIM system, into Umbraco: categories, products, variant groups, variants, media, prices, stock and warehouse stock. Use it to create the catalog from a feed and keep it in step with that system.

The service is `Ekom.Services.IImportService`. It is a synchronous .NET API that runs on your server. Ekom does not offer it as a public HTTP endpoint. If another system must start an import over HTTP, build your own endpoint and protect it.

On this page:

- [Choose the operation](#choose-the-operation)
- [Before you start](#before-you-start)
- [Create or update one product](#create-or-update-one-product)
- [Synchronize the whole catalog](#synchronize-the-whole-catalog)
- [Synchronize one category branch or move categories](#synchronize-one-category-branch-or-move-categories)
- [Update one existing item](#update-one-existing-item)
- [Synchronize variant groups and variants](#synchronize-variant-groups-and-variants)
- [Media](#media)
- [Stock and warehouse stock](#stock-and-warehouse-stock)
- [Update only prices from an ERP system](#update-only-prices-from-an-erp-system)
- [Prices in database mode](#prices-in-database-mode)
- [Control what is saved and published](#control-what-is-saved-and-published)
- [How imports handle moved content](#how-imports-handle-moved-content)
- [Import events](#import-events)
- [Errors and failures](#errors-and-failures)

## Choose the operation

| You want to | Method | Deletes content that is not in the payload |
| --- | --- | --- |
| Make the whole catalog match a feed | `FullSync(data, parentKey, syncUser)` | Yes: categories, products, variant groups and variants. |
| Make one category branch match a feed | `CategorySync(data, categoryKey, syncUser)` | Products, variant groups and variants. Not categories. |
| Move categories to new parents, nothing else | `MoveSync(data, parentKey, syncUser)` | No |
| Create or update one product with its variants and media | `ProductSync(product, parentKey, mediaRootKey, syncUser, forceUpdate)` | The product's variant groups and variants that are not in the payload. |
| Update the values of one product that exists | `ProductUpdateSync(product, parentKey, mediaRootKey, syncUser, forceUpdate)` | No. Variants and media are not touched. |
| Update the values of one category that exists | `CategoryUpdateSync(category, parentKey, syncUser)` | No |
| Update the values of one variant that exists | `VariantUpdateSync(variant, parentKey, syncUser)` | No |
| Create or update one variant group under a product | `VariantGroupSync(group, productKey, mediaRootKey, syncUser)` | The product's other variant groups, and variants missing from this group. |
| Create or update one variant under a variant group | `VariantSync(variant, groupKey, mediaRootKey, syncUser)` | No |
| Change only the images or files of a product or variant | `SyncProductMedia(...)`, `SyncVariantMedia(...)` | No. Optionally replaces the field's references. |
| Change only prices, without saving nodes | `Ekom.API.Prices` (not the import service) | No |

`syncUser` is the Umbraco user ID recorded on the changes. The default is `-1`.

## Before you start

### Identify every item with a stable identifier

Every import model inherits from `ImportBase`. These members are required:

| Member | What it is |
| --- | --- |
| `Identifier` | The item's ID in the other system. Ekom stores it in the `ekmIdentifier` property and uses it to find the item on the next import. |
| `NodeName` | The name of the Umbraco node. |
| `Title` | The title, as a dictionary from culture to value, for example `["en-US"] = "Running shoe"`. |

Identifiers are compared exactly, so upper and lower case matter. Do not use a SKU as the identifier unless the other system never changes it.

### Describe the category tree

`ImportData` holds the feed for `FullSync`, `CategorySync` and `MoveSync`:

| Member | What it is |
| --- | --- |
| `Categories` | The top categories. Each `ImportCategory` has `SubCategories`, so the list is a tree. |
| `Products` | All products, as a flat list. |
| `MediaRootKey` | Required. The media folder where new media is created. `Guid.Empty` means do not load media. |
| `RecycleBinKey` | Optional. A category node to move removed products to, instead of deleting them. |
| `ProductProcessKey` | Optional. A category node for products that come back from the recycle node. |

Each `ImportCategory` also has a required `ParentIdentifier`: the identifier of its parent category, or empty for a category directly under the catalog root. Keep `ParentIdentifier` and the `SubCategories` nesting in agreement. When they differ, Ekom moves the category to the parent named in `ParentIdentifier`.

A product's `Categories` lists category identifiers. The first one that Ekom finds becomes the primary category: the product node lives under it. The other categories are saved in the product's `categories` picker.

### Choose the catalog root

`parentKey` (or `categoryKey`) is the node the import works under. Ekom only finds and changes content below it. When you pass `null`, Ekom uses the first `ekmCatalog` node that is not in the recycle bin. If there is no such node, the import throws `ArgumentNullException`.

### Protect content from the import

Set the `ekmDisableSync` property to `true` on a node to keep the import away from it. Ekom does not update, move or delete a node with this property set. `SyncProductMedia` and `SyncVariantMedia` are the exception: they also change sync-disabled nodes.

### Run one large import at a time

Only one `FullSync`, `MoveSync` or `CategorySync` can run at a time in one application. A second one throws `InvalidOperationException("Sync is already in progress.")`. The single-item methods do not have this guard.

This guard does not work across servers. If more than one server can start imports, make sure yourself that only one runs at a time, and that full, single-product and media jobs on the same catalog do not overlap. Backoffice edits and other writers can still change content during an import.

## Create or update one product

`ProductSync` creates the product if it does not exist and updates it if it does, together with its variant groups, variants and media.

```csharp
using Ekom.Models.Import;
using Ekom.Services;

var product = new ImportProduct
{
    Identifier = "erp-product-123",
    NodeName = "Trail running shoe",
    Title = new Dictionary<string, object>
    {
        ["en-US"] = "Trail running shoe",
    },
    SKU = "SKU-123",
    Categories = ["erp-category-shoes"],
    Price =
    [
        new ImportPrice
        {
            StoreAlias = "Store",
            Currency = "en-US",
            Price = 149.95m,
        },
    ],
    Stock =
    [
        new ImportStock
        {
            StoreAlias = "Store",
            Stock = 20m,
        },
    ],
};

importService.ProductSync(
    product,
    parentKey: catalogRootKey,
    mediaRootKey: mediaRootKey,
    syncUser: -1);
```

What `ProductSync` does:

- At least one of the product's `Categories` must exist under the catalog root. If none does, Ekom logs a warning and does not save the product.
- It looks for the existing product only under the primary category. If the product now lives in another category, `ProductSync` creates a new product under the primary category. Use `FullSync` or `CategorySync` to move products, or set `PreservePrimaryCategory` to `true` to find the product anywhere under the root.
- It synchronizes the product's `VariantGroups`. Variant groups that are not in the payload are deleted. Variants that are not in their group in the payload are deleted.
- It loads only the media that the payload refers to.

To change only the product's own values, without touching its variants, use `ProductUpdateSync`.

## Synchronize the whole catalog

`FullSync` makes the catalog under the root match the payload. Treat the payload as the complete truth: what is missing from it is removed.

```csharp
var data = new ImportData
{
    MediaRootKey = mediaRootKey,
    RecycleBinKey = removedProductsCategoryKey, // optional
    Categories = categories,
    Products = products,
};

importService.FullSync(data, parentKey: catalogRootKey);
```

### What FullSync removes

- **Categories** that are not in the payload are deleted. They are not moved to the recycle node.
- **Products** that are not in the payload are deleted. When `RecycleBinKey` is set, they are unpublished and moved to that category node instead. Products already in the recycle node stay there.
- **Products whose new primary category does not exist** are deleted, or moved to the recycle node when it is set.
- **Variant groups and variants** that are missing from a product in the payload are deleted.
- Nodes with `ekmDisableSync` set to `true` are never removed.

FullSync refuses to run when no product in the payload has a category. It throws `ArgumentException("No products connected to categories in importProducts, sync stopped")` before it changes any product. This protects the products from a broken feed. Categories are processed before products, so category changes made earlier in the same call stay. A feed with no products at all does not remove any products.

When the same product identifier appears twice in the payload, Ekom logs a warning. Do not send duplicates.

### Products that come back

When a product that is in the recycle node appears in the payload again, Ekom moves it to its primary category and publishes it.

`ProductProcessKey` is for a staging step. When both `RecycleBinKey` and `ProductProcessKey` are set and a product that is saved is still under the recycle node, Ekom first moves it to the processing node. Products under either node, with their variant groups and variants, are only saved, never published or unpublished. See [Staging categories](#staging-categories).

### Test before you turn it on

Run the exact payload against a copy of the production database before you run it in production. Check what it would delete.

## Synchronize one category branch or move categories

### CategorySync

`CategorySync(data, categoryKey, syncUser)` works like `FullSync`, with the category `categoryKey` as the root:

- It creates and updates the categories in the payload below that category. It does not delete categories.
- It synchronizes the products in `data.Products`, and removes products under the branch that are not in the payload, in the same way as `FullSync`, including `RecycleBinKey` and `ProductProcessKey`.

### MoveSync

`MoveSync(data, parentKey, syncUser)` moves existing categories to the parent named in their `ParentIdentifier`. It does not save category values, does not touch products and does not delete anything. It does not raise `SyncFinished`. Send only categories that already exist; use `FullSync` or `CategorySync` to create categories.

## Update one existing item

`ProductUpdateSync`, `CategoryUpdateSync` and `VariantUpdateSync` update one item that already exists. They find it by `Identifier` under the root, with a targeted database query, without loading the rest of the catalog.

- If no item has that identifier, or the item has `ekmDisableSync` set, they throw `ArgumentNullException`. An empty identifier never matches.
- They do not create, move or delete anything.

`ProductUpdateSync` updates the product's own values, stock, warehouse stock and prices. It does not synchronize variant groups, variants or media. It raises [`ProductImportEvaluating`](#product-eligibility) after it found the product; an excluded product is not saved.

```csharp
importService.ProductUpdateSync(
    importProduct,
    parentKey: catalogRootKey,
    mediaRootKey: mediaRootKey,
    syncUser: -1);
```

## Synchronize variant groups and variants

`VariantGroupSync(group, productKey, mediaRootKey, syncUser)` creates or updates one variant group under the product with key `productKey`, and the variants in it.

- Other variant groups of that product that are not in the call are **deleted**, unless they have `ekmDisableSync` set.
- Variants in the group that are not in `group.Variants` are deleted.

To add or change one group without removing the others, use `ProductSync` with the full list of groups, or `VariantSync` for each variant.

`VariantSync(variant, groupKey, mediaRootKey, syncUser)` creates or updates one variant under the variant group with key `groupKey`. It does not delete other variants.

## Media

`Images` (on every import model) and `Files` (on products and variants) take a list of `IImportMedia`:

| Type | Use it for | Required members |
| --- | --- | --- |
| `ImportMediaFromUdi` | Media that already exists in Umbraco. | `Udi` |
| `ImportMediaFromExternalUrl` | Media that Ekom downloads. | `Url`, `FileName`, `NodeName` |
| `ImportMediaFromBytes` | Media you pass as bytes. | `Bytes`, `FileName`, `NodeName` |
| `ImportMediaFromBase64` | Media you pass as base64 text. | `Base64`, `FileName`, `NodeName` |

All types have `SortOrder` and `Action` (`ImportMediaAction.Add`, the default, or `Delete`). The URL, bytes and base64 types also have `Identifier` and `Comparer`, which Ekom uses to recognize media it imported before, and `Date`.

New media is created under `ImportData.MediaRootKey`, or under the `mediaRootKey` argument of the single-item methods.

- `FullSync` and `CategorySync` load the whole media folder. With `MediaRootKey = Guid.Empty` they do not load the media folder.
- `ProductSync`, `VariantGroupSync` and `VariantSync` load only the media the payload refers to. Media that a save handler adds later, for example an `ImportMediaFromUdi` added in `ProductSaveStarting`, is looked up under the media folder when it is needed.
- The single-item methods need a `mediaRootKey` that exists.

### Change only the media of a product or variant

`SyncProductMedia` and `SyncVariantMedia` change one media field of one product or variant, and nothing else:

```csharp
importService.SyncProductMedia(
    "erp-product-123",
    images,
    mediaRootKey,
    ImportMediaTypes.Image,
    ImportMediaContentTypes.images,
    replaceExisting: true,
    syncUser: -1);
```

- They find the item by identifier anywhere in the content tree, including unpublished items and items with `ekmDisableSync` set. Items in the recycle bin are ignored. When no item matches, they throw `ArgumentNullException`.
- `ImportMediaContentTypes.images` or `ImportMediaContentTypes.files` selects the field. `ImportMediaTypes.Image` or `ImportMediaTypes.File` is the kind of media.
- They load only the media they need: the incoming identifiers, comparers and UDIs, and the media the field already refers to, so that untouched media can keep its place in the order.
- They do not change product values, categories, children or stock.
- A published item that changed is saved and published again. An unpublished item is only saved.

By default the incoming media is merged into the field. With `replaceExisting: true` the field afterwards holds only the incoming media:

- Every incoming item must be a supported media type with `Action = ImportMediaAction.Add`. A `Delete` action, an unknown type or a `null` item throws `ArgumentException` before anything is changed.
- An empty list clears the field.
- Matching media is reused, references are not duplicated, and the normal order and publish rules apply. Replacing removes references only. It does not delete media items and does not change other fields.
- If any incoming item cannot be downloaded, imported or found, Ekom throws before it changes the field, so the field keeps its old references. Incoming UDIs must point to media of the right type under the media folder.
- This is not one transaction. Media files created or updated before the failure stay. A failed content save behaves as it does elsewhere.

If you have your own `IImportService` implementation that only has the older method signatures, merging through the new overload still works, but `replaceExisting: true` throws `NotSupportedException` until you implement it.

If existing code passes an untyped `default` as the sixth argument, write `syncUser: default` or `default(int)`, so the compiler does not pick the new `bool` overload. Calls that pass a user ID, or no user ID, are not affected.

## Stock and warehouse stock

### Sellable stock

`ImportProduct.Stock` and `ImportVariant.Stock` replace the stored stock count. They are not changes to add to it.

- `StoreAlias` is used when `Ekom:PerStoreStock` is `true`. Otherwise one count is set for all stores.
- A negative value is imported as `0`.
- Only values that differ from the current count are written. Ekom collects them and writes them in batches of 200, and the import waits for each batch, so the stock is saved when the method returns.
- A failed batch is logged as an error. It does not stop the import.
- Stock is written even when the node's content did not change.
- When the import saves a product or variant node, Ekom does not re-apply the stock value stored on the node by the backoffice stock editor. The stock table keeps the value from the feed or from an earlier ERP update.

A replaced count does not take open reservations into account. Read [Stock reservations](reservations.md) before you import stock into a store that takes orders. See also [Stock](stock.md).

### Warehouse stock

`ImportProduct.WarehouseStock` and `ImportVariant.WarehouseStock` change only the warehouses you list. Warehouses you leave out keep their balance.

```csharp
product.WarehouseStock =
[
    new ImportWarehouseStock
    {
        StoreAlias = "Store",
        WarehouseKey = mainWarehouseKey,
        Balance = 12m,
    },
    new ImportWarehouseStock
    {
        StoreAlias = "Store",
        WarehouseKey = closedWarehouseKey,
        Clear = true,
    },
];
```

- `Clear = true` removes the balance. Otherwise `Balance` is set.
- Ekom uses the `SKU` in the payload. When it is empty, warehouse stock is skipped.
- Each entry is applied on its own. If any entry fails, for example because the warehouse is not set up for that store, the other entries are still saved, and then Ekom throws `InvalidOperationException` with all failed entries. For a product, the product is marked as failed and its content is not saved (see [Errors and failures](#errors-and-failures)). For a variant, the exception stops the import.

Warehouse stock is for display and integrations. It does not change what customers can buy. See [Warehouse stock](stock.md#warehouse-stock).

## Update only prices from an ERP system

When the ERP system changes prices often, you can write prices without saving or publishing any node. This needs database price storage. Turn on `Ekom:UseDatabasePrices` and restart Ekom:

```json
{
  "Ekom": {
    "UseDatabasePrices": true
  }
}
```

Then call `Ekom.API.Prices` directly for messages that only carry prices. This writes to the `EkomPrice` table. It does not load, save or publish an Umbraco node. It throws `InvalidOperationException` when database prices are turned off. See [Database price storage](database-price-storage.md).

### One price

```csharp
// Map your ERP identifier to the key of the existing Umbraco node.
// For a variant price, use the variant's key.
Guid nodeKey = existingProductOrVariantKey;

await Ekom.API.Prices.Instance.SetPriceAsync(
    nodeKey: nodeKey,
    storeAlias: "Web",
    currency: "en-US",
    price: 49.95m,
    cancellationToken: cancellationToken);
```

- `Web` must be one of your store aliases.
- `en-US` is the store's currency value, not the ISO code `USD`.
- The amount follows the store's own setting for whether prices include VAT.
- The node must already exist. The price API does not look up ERP identifiers, does not create nodes, and does not check that the node, store or currency exists.

### A service for an ERP change feed

`Ekom.API.Prices` is registered by Ekom, so you can inject it:

```csharp
using Ekom.API;

public sealed record ErpPriceChange(
    Guid NodeKey,
    string StoreAlias,
    string Currency,
    decimal Amount);

public sealed class ErpPriceSync
{
    private readonly Prices _prices;

    public ErpPriceSync(Prices prices)
    {
        _prices = prices;
    }

    public async Task ApplyAsync(
        IEnumerable<ErpPriceChange> changes,
        CancellationToken cancellationToken = default)
    {
        foreach (var change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _prices.SetPriceAsync(
                nodeKey: change.NodeKey,
                storeAlias: change.StoreAlias,
                currency: change.Currency,
                price: change.Amount,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
```

1. Register the service in the site's service setup, for example `services.AddTransient<ErpPriceSync>()`.
2. In your ERP job or protected integration endpoint, map the feed's ERP identifiers to existing node keys.
3. Call `ApplyAsync` with the mapped changes.

How this behaves:

- Each call inserts or replaces one price for one node, price property, store and currency. Other prices do not change.
- Each call is its own transaction. If the feed fails halfway, the first part stays saved.
- Sending the same update again replaces the same row. It does not create a duplicate.
- When two feeds write the same price, the last write wins. Coordinate feeds that can overlap.

To update the discount price, use the same call with `propertyAlias: "ekmDiscountPrice"`:

```csharp
await Ekom.API.Prices.Instance.SetPriceAsync(
    nodeKey, "Web", "en-US", 39.95m,
    propertyAlias: "ekmDiscountPrice",
    cancellationToken: cancellationToken);
```

Do not call `IContentService.Save` or a publish method after a price-only write.

After a write, Ekom updates its in-memory price copy on that server. Later reads and the backoffice price editor on that server show the new price at once, without a publish or a manual refresh. Writes from another server, or straight in SQL, need `await prices.FillCacheAsync(cancellationToken)` on each server that serves the site. For node prices without an override, Ekom keeps using the node value.

The Algolia plugin queues committed price changes and reindexes the affected products in the background, without slowing down the ERP writer. Other search indexes and page caches can subscribe to `Ekom.Events.PriceEvents.PriceChanged`.

### Change all prices of a product together

Use `ApplyAsync` when all prices of a product must change together. If one change fails its check or conflicts, none of them is saved. Changes to the value already stored do nothing: no new timestamp and no event.

```csharp
using Ekom.Models;

await prices.ApplyAsync(
[
    new PriceMutation(productKey, "Web", "en-US", 149.95m),
    new PriceMutation(productKey, "Web", "en-GB", 119.95m),
], cancellationToken);
```

`SetPrice` and `SetPriceAsync` always replace the current value, which is what an ERP feed needs. When a person edits a price they loaded earlier, use `CheckExpected: true, ExpectedPrice: loadedPrice` on the `PriceMutation`. `ExpectedPrice: null` means "no override may exist". When the stored value differs, Ekom throws `PriceConflictException`; reload and try again.

The [Variant App](variant-app.md) sends the original values it loaded, writes only the currencies the editor changed, and reports a conflict when someone changed an edited price after it was loaded. A custom client of the Variant App routes must send `originalPriceValues` and `priceOverrides` when database prices are on.

## Prices in database mode

`ImportProduct.Price` and `ImportVariant.Price` (the property name is singular) follow the price storage setting. You can keep sending your normal import payload:

```csharp
// Keep the normal product payload and identifier from your ERP mapping.
importProduct.Price =
[
    new ImportPrice
    {
        StoreAlias = "Web",
        Currency = "en-US",
        Price = 49.95m,
    },
];

importService.ProductUpdateSync(
    importProduct,
    parentKey: catalogRootKey,
    mediaRootKey: mediaRootKey,
    syncUser: -1);
```

### With `Ekom:UseDatabasePrices` set to `true`

- Product and variant prices are written to the table through `Prices.Apply`, as one batch per node, before the importer checks whether the node's content changed.
- The default change check (the content hash) leaves out `Price`. A change of price only updates the table; it does not save or publish the node.
- This applies to the update methods and to the product and variant steps of `FullSync`, `CategorySync` and `ProductSync`.
- A new node gets its key and its table prices before it is first saved or published.
- An invalid price stops the whole price batch of that node before its content is saved.
- An empty `Price` list leaves the table rows as they are.

`FullSync`, `CategorySync` and `ProductSync` read the table prices from the database once for each chunk of 200 products, including their variants. A product or variant whose table prices already match the feed is skipped without a transaction of its own. Any difference is still written through `Prices.Apply` before the content is saved, and invalid prices are still rejected. If another server or SQL changes a price after its chunk was read, the import leaves that row as it is; the next sync corrects it.

Other content changes, media changes and `forceUpdate: true` still save and publish as normal.

When you first turn on database prices, existing nodes have a content hash that included prices, so the next import saves them once to store the new hash.

If you set your own `Comparer`, Ekom uses it as it is. Leave prices out of your comparer, or price-only changes will save the node. Table prices are written even when your comparer has not changed.

Price writes and content saves are separate transactions. If the content save fails after the prices were written, the table rows stay for that node key. Retry with the same key, or remove the unused overrides. Do not treat a failed content save as a successful import.

### With `Ekom:UseDatabasePrices` set to `false`

Prices are stored on the node and are part of the default change check, so a price change saves the node.

## Control what is saved and published

### Change detection

Ekom saves a node only when something changed. It compares a value called the comparer, stored on the node, with the comparer of the incoming item.

- Set `Comparer` yourself, for example to a version number or timestamp from the other system.
- When you leave it empty, Ekom computes a SHA-256 hash of the item. The hash leaves out children, media, stock, warehouse stock and `EventProperties`, because Ekom handles those separately. With database prices it also leaves out `Price`.

A node is saved when the comparer changed, when the node is new, or when its images or files changed.

`forceUpdate: true` on `ProductSync` and `ProductUpdateSync` skips the comparison for the product, its variant groups and its variants. Use it after you change how fields are mapped.

### Values on each import item

| Member | Behavior |
| --- | --- |
| `SaveEvent` | `SavePublish` (default), `Save` or `Unpublish`. See the next table. |
| `PreservePublishStatus` | `true`: an existing published node is saved and published, an existing unpublished node is only saved, and a new node is saved but not published. |
| `PreserveExistingValues` | `true`: keep the current value when the incoming value is empty. For products this covers the title (except on new products), summary, description, the extra categories and media. For variants it covers the description and media. |
| `PreservePrimaryCategory` | Products only. `true`: an existing product stays in its current category, and the first `Categories` value is used only when the product is created. |
| `SortOrder` | Sets the Umbraco sort order among siblings. |
| `CreateDate`, `UpdateDate` | Set the node's dates. When empty, the current dates stay. |
| `AdditionalProperties` | Values for other properties, by property alias. For products and categories, aliases the document type does not have are skipped. |
| `EventProperties` | Data for your import event handlers. Ekom does not save it. |

`Title`, `Slug`, `Description` and `Summary` are dictionaries from culture to value. `AdditionalProperties` is how you fill your own document type properties; the values must be in the format the property editor stores.

Some product and variant values are written only when you send them:

- `SKU` is written only when it is not empty. An import cannot clear it.
- `Vat` is written only when it has a value. An import cannot clear it.
- `EnableBackorder` is written when it is `true` or `false`. Leave it `null` to keep the current value.

### How SaveEvent is applied

| `SaveEvent` | New node | Existing node |
| --- | --- | --- |
| `SavePublish` | Saved and published. | Saved and published. |
| `Unpublish` | Saved, not published. | Saved, then unpublished. |
| `Save` | Saved, not published. | Saved, not published. A published node stays published with its old content, and the saved changes wait as a draft. |

`PreservePublishStatus` applies to `SavePublish` only.

When the installed Umbraco version has a single save-and-publish method (added in Umbraco 18.1), Ekom saves and publishes content that does not vary by culture in one operation. Otherwise it saves and then publishes.

### Staging categories

In `FullSync` and `CategorySync`, `ImportData.ProductProcessKey` and `RecycleBinKey` are staging categories. When a product's parent is one of them, the product, its variant groups and its variants are only saved, whatever their `SaveEvent` or `PreservePublishStatus` says. Ekom uses the product it already loaded for this check, so it adds no extra lookups.

Outside these categories the normal rules apply: `SavePublish` with `PreservePublishStatus = false` publishes new children.

The single-item methods do not take staging keys, so this rule does not apply to them.

## How imports handle moved content

When Umbraco moves a node, it also updates the path of every node below it. The import must not then save an old copy of those child nodes. Ekom handles this for you:

- After it moves a product, it reloads the moved product's variant groups and variants before it imports them. It does this in batches of IDs and only for nodes it already loaded.
- After it moves a category, it reloads the nodes below it before they are used again.
- Products that did not move cause no extra reads. Ekom does not reload the whole catalog, does not look up variants one by one, adds no startup scans and needs no database migration.
- When it deletes a node, it removes that node and everything below it from its working lists.

If a move or delete fails or is cancelled, the import stops instead of carrying on as if it worked. If a reloaded child node is missing, in the recycle bin, newly sync-disabled, or under an unexpected parent, the import stops before it processes that product's children. These checks do not undo earlier changes of the import, and reloading cannot repair a path that was already wrong in the database.

The extra reads grow with the number of moved nodes. If your feeds move many large products, measure an import with many moves before you go live.

### Check moves before you go live

Run these steps on a copy of the production database:

1. Create a product with a variant group and variants in one category, and a target category at a different depth in the tree.
2. Import the product into the target category, and change a variant value or comparer, so that a child node is really saved and not skipped as unchanged.
3. Reload the product, variant group and variants. Check that each path contains the current parent chain, that each level matches its path, and that the imported values were saved.
4. Repeat with a product that comes back from the recycle node, and with the move to the processing node. Test with a changed and an unchanged product comparer, and run the same import again to check that nothing changes.
5. Compare a feed without moves with a feed with many moves: note the time and the number of reloads. Include cancelled moves and deletes, and missing child nodes, and check that old copies of child nodes are not saved.

## Import events

Ekom raises these static async events, in `Ekom.Events.ImportEvents`:

| Event | When | Arguments |
| --- | --- | --- |
| `CategorySaveStarting` | Before a category is compared and saved. | `CategoryContent`, `ImportCategory`, `IsCreateOperation`, `ImagesHaveNoChanges` |
| `ProductImportEvaluating` | Before products are reconciled. See [Product eligibility](#product-eligibility). | `ImportProduct`, `ProductContent`, `ImportRootKey`, `ExcludeFromImport` |
| `ProductSaveStarting` | Before a product's prices, stock and content are processed. | `ProductContent`, `ImportProduct`, `IsCreateOperation`, `ImagesHaveNoChanges`, `FilesHaveNoChanges` |
| `VariantSaveStarting` | Before a variant's prices, stock and content are processed. | `VariantContent`, `ImportVariant`, `IsCreateOperation`, `ImagesHaveNoChanges`, `FilesHaveNoChanges` |
| `SyncFinished` | After an import method finished. | `ImportCategories`, `ImportProducts`, `ImportVariants`, `ImportVariantGroups` (the saved items), `Type` (`ImportSyncType`) |

- Each handler is a `Func<TArgs, Task>`. Handlers run one after another.
- In a starting event, set `ImagesHaveNoChanges` or `FilesHaveNoChanges` to `true` to skip importing that media.
- `MoveSync` and the media methods do not raise `SyncFinished`. `CategoryUpdateSync` reports `ImportSyncType.VariantUpdateSync`.

### Product eligibility

Subscribe to `ProductImportEvaluating` to leave products out of an import based on the existing node, without loading the product tree again.

`ImportProductEvaluatingEventArgs` gives you:

- `ImportProduct`: the incoming product.
- `ProductContent`: the existing node that the import found for it, or `null`.
- `ImportRootKey`: the key of the import root.
- `ExcludeFromImport`: `false` at first. Set it to `true` to leave the product out.

```csharp
using Ekom.Events;

private static Task EvaluateProductAsync(ImportProductEvaluatingEventArgs args)
{
    if (args.ImportRootKey == catalogRootKey
        && args.ProductContent?.GetValue<bool>("excludeFromExternalImport") == true)
    {
        args.ExcludeFromImport = true;
    }

    return Task.CompletedTask;
}
```

1. Add the handler once at startup: `ImportEvents.ProductImportEvaluating += EvaluateProductAsync`.
2. Remove it at shutdown.
3. Use `ImportRootKey` to limit the handler to the imports it is meant for.

How the event works:

- Handlers run one after another. Once a handler sets `ExcludeFromImport` to `true`, a later handler cannot set it back to `false`.
- Treat the product and node as read-only here. Use the event to decide eligibility, not to change identifiers, categories or saved content.
- The event uses the import's in-memory index and adds no content reads. `ProductContent` is the sync-enabled node the current import can see, not a check across the whole catalog. `ProductSync` looks under the primary category unless `PreservePrimaryCategory` is `true`.
- `VariantGroupSync`, `VariantSync` and `VariantUpdateSync` do not raise this event.

What happens to an excluded product, including its variant groups and variants:

- In `FullSync` and the product step of `CategorySync`, which remove missing products, an existing excluded product is treated as missing. It is unpublished and moved to the recycle node, or deleted when there is no recycle node. A product already in the recycle node stays there. `ekmDisableSync` still protects a node.
- `ProductSync` and `ProductUpdateSync` skip an excluded product and do not remove the existing node. `ProductUpdateSync` still throws when the product does not exist, before the event runs. `SyncFinished` still runs, but the excluded product is not in the saved list.
- A feed that has products but where every product is excluded still removes missing products. A feed that has no products at all never removes products. The check that products have categories runs on the feed before exclusion.

Ekom evaluates all products before it starts reconciling them. If a handler throws, the import stops before any product is moved, restored, removed, created or saved. Category changes made earlier in the same import are not undone. The event does not make the import a transaction.

## Errors and failures

An import is not one transaction. A failure stops or skips part of the work, but changes already made stay.

| What fails | What happens |
| --- | --- |
| Saving a product (including its prices or warehouse stock) | The error is logged and stored in `ImportProduct.Exception`. The import goes on with the next product. Failed products are listed in the log, grouped by error message, and are included in `SyncFinished`. |
| Saving a category | The error is logged and rethrown. The import stops. |
| Saving a variant (including its prices or warehouse stock) | The exception stops the import. A variant that Umbraco reports as "Cannot save a non-current version" is skipped with a warning instead. |
| A product move or delete that fails or is cancelled | The import stops with `InvalidOperationException`. |
| Writing a stock batch | The error is logged. The import goes on. |
| A product with no category that exists under the root | A warning is logged and the product is skipped. |
| `FullSync` or `MoveSync` as a whole | The error is logged as critical and rethrown. |
| An update method cannot find its item | `ArgumentNullException`. |

Run imports in a background process, not in a web request, and keep the logs. After a large import, check that the catalog caches on every server show the new data. See [Refresh the caches](stores.md#refresh-the-caches).

## Related pages

- [Database price storage](database-price-storage.md): keep prices in a table and update them without publishing.
- [Stock](stock.md): what imported stock and warehouse stock mean.
- [Catalog and products](catalog-and-products.md): how the imported catalog is read on the storefront.
- [Events](../reference/events.md): every event Ekom raises, including import events.
- [Configuration reference](../reference/configuration-reference.md): all `Ekom` settings.

[Back to Guides](README.md)
