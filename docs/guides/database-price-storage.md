# Database price storage

Ekom stores prices on Umbraco nodes by default. Database storage is useful when prices change frequently: an ERP or importer can update a price without saving or publishing the product. Editors use the document's normal Save action, like stock.

The tradeoffs are immediate changes, extra memory use, and a cache on each application server. Keep node storage if price edits should follow Umbraco's draft and publishing workflow.

## Enable database storage

Set the following option and restart the application:

```json
{
  "Ekom": {
    "UseDatabasePrices": true
  }
}
```

Startup creates `EkomPrice` on SQLite and SQL Server. Each row identifies a node key, property alias, store alias, and currency. Identifiers are case-insensitive. Amounts use `decimal(18,4)` and must be non-negative. Base prices and `ekmDiscountPrice` use separate rows.

See [ERP price-only sync](imports.md#erp-price-only-sync-without-saving-or-publishing) for a complete injectable change-feed service and identifier mapping guidance.

## Update prices

Write prices without saving or publishing content:

```csharp
await Ekom.API.Prices.Instance.SetPriceAsync(
    productKey, "Web", "en-US", 49.95m,
    cancellationToken: cancellationToken);

await Ekom.API.Prices.Instance.SetPriceAsync(
    productKey, "Web", "en-US", 39.95m,
    propertyAlias: "ekmDiscountPrice",
    cancellationToken: cancellationToken);
```

`Ekom.API.Prices` is also injectable. `GetAsync(nodeKey, propertyAlias)` returns the stored overrides. Both `IContent.SetPrice` and `SetPriceAsync` honor this setting. In node mode these helpers change the content value and the caller still saves/publishes it; in database mode they write immediately and the caller should skip saving/publishing for price-only changes. For direct ERP updates, resolve an existing node key. The built-in importer assigns keys and writes prices before first publication.

The built-in importer also routes `ImportProduct.Price` and `ImportVariant.Price` to the table when enabled. Prices are applied even when content is unchanged, and the default content hash excludes prices to avoid saves/publishes for price-only changes. See [importer behavior](imports.md#using-the-built-in-ekom-importer) for custom comparers and first-sync behavior.

## Editing and fallback behavior

The Ekom Price datatype supports database prices in Umbraco 13, 17, and 18. Edit the field, then use the document's normal **Save** or **Save and Publish** action. Like stock, the `ContentSaving` event writes changed prices through the price API. There is no separate field update button or automatic write when leaving the field.

**Saving makes price changes live, even if the content remains a draft.** Only explicitly edited currencies are written; saving unrelated content does not copy stale node prices over newer ERP values. Concurrent edits to the same currency use the last committed price write. Content and table prices are not one transaction: if another save handler fails after the price write, the price may already be live, matching the stock saving workflow.

Existing node prices remain a fallback for individual currencies that have no table row. A stored zero is an explicit value; the existing variant rule that a zero base price inherits its parent remains unchanged. Enabling table storage does not migrate or modify node values. Populate the table through your ERP sync or the migration helper to make it the source for every price. Disabling the setting returns to the original node values, so copy current prices back first if you want to retain them when switching back.

## Caching and multiple servers

Prices are loaded into memory at startup and refreshed after committed changes. Products, variants, discounts, and providers use this shared cache, so normal price reads do not query the database. Each calculation keeps related base, discount, and inherited prices consistent during an update.

Refreshes read all affected rows in one transaction before publishing the cache update. Conflicting writes may briefly wait; warm cache reads remain available. Async writes await SQL and cache refreshes without blocking on the refresh gate.

Each application instance has its own cache. Use the registered `Prices` service for local updates. After direct SQL changes or writes on another server, reload each serving instance with `await prices.FillCacheAsync(cancellationToken)`. A new request does not reload the cache. Allow enough memory for all price rows.

Raw Umbraco property JSON still contains the node value. Use Ekom price models or `Prices.Resolve` when you need table overrides.

## Notifications and access

Table updates do not emit Umbraco publish notifications. `Ekom.Events.PriceEvents.PriceChanged` fires after commit, including removals. Subscribers should enqueue work. Algolia queues affected products for reindexing when product indexing is enabled. Delivery is best-effort; reconcile indexes after process failures or queue overflow.

Backoffice price editing requires an authenticated, approved user with access to the node and store, and permission to update the content. The direct live-price API retains its own publish-permission requirement. Custom `ISecurityService` implementations must implement the relevant access checks; defaults deny access. Variant-manager saves validate required fields before writing prices, but content, stock, and price changes are not one shared transaction.

## Remove overrides or return to node storage

To remove an override and immediately restore the node fallback (zero is a real override, not removal):

```csharp
await prices.RemovePriceAsync(productKey, "Web", "en-US", cancellationToken: cancellationToken);
```

The authenticated backoffice equivalent is `DELETE /ekom/backoffice/Prices/{id}/{propertyAlias}/{storeAlias}/{currency}`. A batch `PriceMutation` with `Price: null` also removes an override and can use an expected-value check.

To switch back to node storage without reverting to stale prices:

1. Pause ERP writers and backoffice price editing.
2. While database mode remains enabled, load each affected product and variant as `IContent`, call `content.CopyDatabasePricesToNode()` (namespace `Ekom.Utilities`), and explicitly save/publish it using the site's usual content workflow. Repeat with `"ekmDiscountPrice"` and any custom price properties. The helper merges overrides with node fallbacks, including legacy scalar/array formats; it does not save or publish. Unknown store/currency mappings throw before changing content.
3. Verify every affected node was published successfully, then disable `UseDatabasePrices` and restart all application servers. Resume writers in node mode.

`Prices.Get`/`GetAsync` can export overrides while database mode is enabled. Disabling the setting does not delete the table rows; they take effect again if it is re-enabled. Remove obsolete overrides before disabling the setting if you do not want to reuse them.

## Migrate existing prices without an ERP

Database storage does not require an ERP. Enable `Ekom:UseDatabasePrices` and restart to create the table. Prices continue using node values until an override exists, so enabling the flag alone does not require migration. To populate the table, call `MigrateNodePricesAsync` for each product and variant using its **published raw property value**:

```csharp
using Ekom.API;
using Ekom.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

public static async Task<int> MigratePublishedPricesAsync(
    Prices prices,
    IEnumerable<IPublishedContent> publishedNodes,
    IReadOnlyList<IStore> stores,
    CancellationToken cancellationToken = default)
{
    var inserted = 0;
    foreach (var node in publishedNodes)
    {
        foreach (var alias in new[] { "price", "ekmDiscountPrice" })
        {
            if (!node.HasProperty(alias)) continue;
            inserted += await prices.MigrateNodePricesAsync(
                node.Key, node.Value<string>(alias), stores, alias, cancellationToken);
        }
    }
    return inserted;
}
```

Run this from a controlled migration job. Supply the published products and variants under the intended catalog root, and the Ekom stores applicable to that catalog. Process a large catalog in pages and record progress. Add any custom price property aliases to the loop. Do not pass calculated storefront prices: those may include discounts, VAT conversion, or inherited parent prices.

The migration copies scalar, currency-array, store-keyed, and `values`-wrapped node formats. Scalars belong to each store's default currency. Missing values and currencies/stores outside the supplied configuration are skipped; review those mappings before migration. Zero is copied as zero, preserving variant inheritance behavior. Existing table overrides are preserved, including prices already edited in the backoffice. Re-running the job only inserts missing overrides.

Each node/property batch is atomic. Validation failures leave that batch unchanged; earlier batches stay committed. A competing insertion raises `PriceConflictException` instead of overwriting the other writer; retry that node/property to skip the newly existing row. Pause price editing during the initial migration for a predictable snapshot. Use published values rather than `IContent` draft values, because table writes are immediately live. No node save or publish is performed, and source node values remain available for rollback.

After migration, edit prices in the Ekom Price datatype and save the document. The variant manager also supports table prices. Table updates issue the normal price-change notifications, including Algolia reindex jobs when configured; allow the indexing queue to drain for large catalogs.

## Read prices in background jobs

Read prices normally; a scope or preload is no longer needed:

```csharp
foreach (var product in products)
{
    var pricesForExport = product.Prices; // Shared in-memory price cache; no price SQL reads.
}

// Only after out-of-process writes, or when explicitly rebuilding the cache:
await prices.FillCacheAsync(cancellationToken);
```

Existing `BeginReadScope`, `PreloadAsync`, and `PreloadProductsAsync` calls remain supported, but callers no longer need them for normal reads. The shared cache is initialized automatically.
