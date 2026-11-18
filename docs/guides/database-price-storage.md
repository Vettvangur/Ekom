# Database price storage

[Ekom docs](../README.md) / [Guides](README.md) / Database price storage

By default Ekom stores prices on the product and variant nodes in Umbraco, so a price change goes live when the node is published. With database price storage, Ekom keeps prices in a database table instead. An ERP system or importer can then change a price without saving or publishing the node, and the change is live at once.

Use it when prices change often. Keep the default node storage if price changes must follow Umbraco's draft and publish workflow.

What you gain and what it costs:

- A price change is live as soon as it is written. There is no draft.
- Ekom holds every table price in memory on each application server, so you need memory for all price rows.
- Each server has its own copy. A write made on one server, or straight in SQL, is not seen on another server until you reload its copy. See [Caching and several servers](#caching-and-several-servers).

In this page, a price row in the table is called an override: it overrides the price stored on the node.

On this page:

- [Turn on database prices](#turn-on-database-prices)
- [Write prices from code](#write-prices-from-code)
- [Prices from the catalog import](#prices-from-the-catalog-import)
- [Edit prices in the backoffice](#edit-prices-in-the-backoffice)
- [Which price is used](#which-price-is-used)
- [Caching and several servers](#caching-and-several-servers)
- [React to price changes](#react-to-price-changes)
- [Move existing node prices into the table](#move-existing-node-prices-into-the-table)
- [Go back to node prices](#go-back-to-node-prices)
- [Read prices in background jobs](#read-prices-in-background-jobs)

## Turn on database prices

1. Set the option in appsettings:

   ```json
   {
     "Ekom": {
       "UseDatabasePrices": true
     }
   }
   ```

2. Restart the application on every server.

Ekom creates the `EkomPrice` table at startup on SQL Server and SQLite. It creates the table whether or not the option is on.

Turning the option on does not move or change any node prices. Until a price has an override, Ekom keeps using the node price. To fill the table, write prices from your ERP sync, or copy the node prices once. See [Move existing node prices into the table](#move-existing-node-prices-into-the-table).

### What a price row holds

Each row is one price for one node, price property, store and currency:

| Column | Notes |
| --- | --- |
| `NodeKey` | The key (GUID) of the product or variant node. |
| `PropertyAlias` | `price` for the base price, `ekmDiscountPrice` for the discount price, or a custom price property. Up to 100 characters. |
| `StoreAlias` | The store alias. Up to 100 characters. |
| `Currency` | The store's `CurrencyValue`, such as `en-US`. Up to 50 characters. |
| `Price` | A `decimal(18,4)` amount. |
| `UpdateDate` | When the row was last written, in UTC. |

Ekom trims the property alias, store alias and currency and stores them in upper case, so upper and lower case do not matter when you write or read them. The base price and the discount price are separate rows.

## Write prices from code

Inject `Ekom.API.Prices`, or use `Prices.Instance`. These calls write to the table. They do not load, save or publish the node.

```csharp
using Ekom.API;

// Base price of a product in store "Web", currency "en-US".
await prices.SetPriceAsync(productKey, "Web", "en-US", 49.95m, cancellationToken: cancellationToken);

// Discount price of the same product.
await prices.SetPriceAsync(
    productKey, "Web", "en-US", 39.95m,
    propertyAlias: "ekmDiscountPrice",
    cancellationToken: cancellationToken);
```

- `Web` must be one of your store aliases.
- `en-US` is the store's currency value, not the ISO code `USD`.
- The amount follows the store's own setting for whether prices include VAT.
- The node must already exist. Ekom does not look up ERP identifiers, does not create nodes, and does not check that the node, store or currency exists. Map your ERP identifier to the Umbraco node key yourself. For a variant price, use the variant's key.

Each call replaces the current value. Writing the value that is already stored does nothing: no write, no new `UpdateDate`, no event.

### What can go wrong

| Exception | When |
| --- | --- |
| `InvalidOperationException` | `Ekom:UseDatabasePrices` is not `true`. |
| `ArgumentException` | The node key is empty, a store alias, currency or property alias is blank or too long, or a batch contains the same price twice. |
| `ArgumentOutOfRangeException` | The price is negative, has more than 4 decimals, or is larger than `decimal(18,4)` allows. |
| `PriceConflictException` | A change with `CheckExpected: true` found a different value than expected. See [Change several prices at once](#change-several-prices-at-once). |

Ekom checks the whole batch before it writes anything. When the write hits a deadlock, a locked SQLite database or a duplicate-key conflict, Ekom retries it, up to 5 times.

### Change several prices at once

`ApplyAsync` writes a list of `PriceMutation` values in one transaction. If one change fails its check, none of them is saved.

```csharp
using Ekom.Models;

await prices.ApplyAsync(
[
    new PriceMutation(productKey, "Web", "en-US", 149.95m),
    new PriceMutation(productKey, "Web", "en-GB", 119.95m),
], cancellationToken);
```

`PriceMutation(NodeKey, StoreAlias, Currency, Price, PropertyAlias = "price", CheckExpected = false, ExpectedPrice = null)`:

- A `Price` of `null` removes the override, so the node price applies again.
- With `CheckExpected: true`, the change is only saved when the stored value equals `ExpectedPrice`. `ExpectedPrice: null` means "no override may exist". When the value differs, Ekom throws `PriceConflictException` and saves nothing. Reload the price and try again.

Use `CheckExpected` when a person edits a price they loaded earlier, so they do not overwrite a newer value. An ERP feed normally replaces the value and does not need it.

`Validate(changes)` runs the same checks without writing.

### A service for an ERP change feed

For a complete example of an injectable service that applies an ERP price feed, see [Catalog imports: update only prices](imports.md#update-only-prices-from-an-erp-system).

## Prices from the catalog import

When the option is on, the importer writes `ImportProduct.Price` and `ImportVariant.Price` to the table, not to the node:

- Prices are written before the importer checks whether the node changed, so they are written even when the content is unchanged.
- The importer's default change check leaves prices out, so a price-only change does not save or publish the node.
- New nodes get their key and their table prices before they are first saved.

See [Prices in database mode](imports.md#prices-in-database-mode) for the details, including custom comparers and the first sync after you turn the option on.

## Edit prices in the backoffice

The Ekom Price property editor works with database prices on Umbraco 17, 18 and 19.

1. Edit the price field.
2. Click the document's normal **Save** or **Save and Publish**.

**Saving makes the price change live, even when the node stays a draft.** There is no separate button for the price and no automatic save when you leave the field.

How a save handles prices:

- Only the currencies the editor changed are written. Saving other content does not copy old node prices over newer ERP values.
- The prices are written in the same database transaction as the content save. If the save is rolled back, the price changes are rolled back too.
- Other servers and code see the new prices after the save commits, and Ekom raises the change events then.
- When two editors change the same currency, the last save wins.
- When a price cannot be saved, for example because the editor has no access to that store or the amount is invalid, Ekom cancels the whole content save, shows the error in the backoffice, and logs it with the site's logger.

The [Variant App](variant-app.md) also writes variant prices to the table. It writes only the prices that the editor changed, and it reports a conflict when someone else changed the same price after the view loaded it. Content, stock and price changes from the Variant App are not one transaction.

### Who can edit prices

Editing a price in the backoffice requires a signed-in, approved backoffice user who has access to the content node and to the store, and permission to update the content.

The backoffice price routes require permission to publish as well:

| Route | What it does |
| --- | --- |
| `PUT /ekom/backoffice/Prices/{id}/{propertyAlias}` | Sets one price. Body: `storeAlias`, `currency`, `price`. Returns `400` for an unknown store or currency. |
| `DELETE /ekom/backoffice/Prices/{id}/{propertyAlias}/{storeAlias}/{currency}` | Removes one override. |

Both return `400` when database prices are turned off, `404` when the node does not exist, and `403` when the user lacks permission or store access.

If you replace `ISecurityService`, your implementation must do these access checks. The interface's default methods deny access.

## Which price is used

For each store and currency, Ekom uses the override when there is one, and the node price when there is not. This happens per currency, so a node can have an override for one currency and use its node price for another.

- A stored `0` is a real price. It is not the same as "no override".
- The rule that a variant with a base price of `0` uses its product's price still applies, also when the `0` comes from the table.
- The raw Umbraco property value still holds the node price. To get the price with overrides, use Ekom's price models (for example `product.Prices`) or `prices.Resolve(...)`.

## Caching and several servers

Ekom loads all table prices into memory at startup. Products, variants, discounts and providers read prices from that copy, so normal price reads do not query the database. While Ekom calculates one product, it uses one consistent set of base, discount and parent prices, even if a write happens at the same time.

After a write through `Prices`, Ekom reads the changed rows back from the database in one transaction and updates the memory copy. If that refresh fails, Ekom logs a warning and clears the copy; the next read loads all prices again.

Each server has its own copy. A new request does not reload it. After a write on another server, or straight in SQL, reload the copy on every server that serves the site:

```csharp
await prices.FillCacheAsync(cancellationToken);
```

### Skip unchanged prices in bulk writes

A bulk writer can avoid one transaction per unchanged node:

1. Call `prices.Refresh(nodeKeys)` to read those nodes' rows from the database into memory, in one read.
2. Call `prices.IsCurrent(changes)` for each node. It checks, from memory only, whether the table already holds exactly these prices. It validates the changes the same way `Apply` does.
3. Call `Apply` or `ApplyAsync` only for the changes that are not current.

A write made elsewhere after the `Refresh` call is not seen until the next refresh.

## React to price changes

A table write does not raise Umbraco's publish notifications. Subscribe to `Ekom.Events.PriceEvents.PriceChanged` instead. The event arguments are `Key`, `StoreAlias`, `Currency`, `PropertyAlias`, `OldValue` and `NewValue`. `NewValue` is `null` when an override was removed.

- The event fires after the change is committed, once for each changed price.
- If your handler throws, Ekom logs a warning. The price stays saved and the handler is not called again.
- Keep handlers short: add the work to a queue and process it elsewhere.

The [Algolia plugin](../integrations/README.md) uses this event to queue the affected products for reindexing when product indexing is turned on. This is best effort. After a crash or a full queue, reconcile the index yourself.

## Move existing node prices into the table

You do not need an ERP system to use database prices. To copy the current node prices into the table:

1. Turn on `Ekom:UseDatabasePrices` and restart.
2. Pause price editing in the backoffice, so the copy is a stable snapshot.
3. Run a migration job that calls `MigrateNodePricesAsync` for every product and variant, with the **published** raw value of each price property.

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

Pass the published products and variants under the catalog root you migrate, and the Ekom stores that sell that catalog. Add any custom price property aliases to the list. For a large catalog, process the nodes in pages and record how far you got.

Use the published raw value, not the `IContent` draft value: table prices are live at once, so a draft value would go live. Do not pass calculated storefront prices either. They can include discounts, VAT conversion or a price taken from the parent product.

How the copy works:

- It reads all node price formats: a single number, a list of currency prices, prices per store, and values wrapped in `values`.
- A single number becomes the price of each store's first (default) currency.
- Missing values, and stores or currencies that are not in the `stores` you pass, are skipped. Check your store and currency setup before you run it.
- `0` is copied as `0`, so the variant rule for a zero price keeps working.
- Existing overrides are never replaced, including prices already edited in the backoffice. Running the job again only adds missing rows.
- The method returns the number of rows it added.
- Invalid JSON in a node value throws, so a broken value is not marked as migrated.

Each node and property is one transaction. If a check fails, that node and property is left unchanged; nodes already done stay done. If another writer adds a row for the same node at the same time, Ekom throws `PriceConflictException` instead of overwriting it. Run that node and property again; the new row is then skipped.

The job does not save or publish any node, and the node values stay as they were, so you can go back.

After the copy, editors keep using the Ekom Price editor and save the document. Table writes raise the normal price events, so with the Algolia plugin a large copy queues many reindex jobs. Let the queue finish.

## Go back to node prices

### Remove one override

Removing an override makes the node price apply again at once:

```csharp
await prices.RemovePriceAsync(productKey, "Web", "en-US", cancellationToken: cancellationToken);
```

Setting a price of `0` is not a removal; `0` is a real price. A `PriceMutation` with `Price: null` also removes an override, and can use `CheckExpected`.

### Turn database prices off

Turning the option off makes Ekom use the node prices again. Those can be older than the table prices. To keep the current prices:

1. Pause ERP writers and price editing in the backoffice.
2. While database prices are still on, load each affected product and variant as `IContent` and call `content.CopyDatabasePricesToNode()` (namespace `Ekom.Utilities`). Call it again with `"ekmDiscountPrice"` and with each custom price property. Then save and publish the node the way your site normally does.
3. Check that every node was published.
4. Set `Ekom:UseDatabasePrices` to `false` and restart every server. Start the writers again in node mode.

`CopyDatabasePricesToNode` merges the overrides into the node value and keeps the node prices for currencies without an override. It reads the older node formats too. It does not save or publish. It throws `ArgumentException` when the node has no such property, and `InvalidOperationException` when an override has a store or currency that is not set up, before it changes the node.

`prices.GetAsync(nodeKey, propertyAlias)` returns the overrides of one node while database prices are on, if you want to export them.

Turning the option off does not delete the table rows. If you turn it on again, they apply again. Remove overrides you do not want to reuse before you turn the option off.

### Set prices on a node from code in either mode

`content.SetPriceAsync(storeAlias, currency, price, propertyAlias, ct)` and `content.SetPrice(...)` on `IContent` (namespace `Ekom.Utilities`) follow the setting:

- Database prices on: they write the override at once. Do not save or publish the node for a price-only change.
- Database prices off: they change the node value. Save and publish the node yourself.

They do nothing when the node has no such property.

## Read prices in background jobs

Read prices the normal way. You do not need a scope or a preload step:

```csharp
foreach (var product in products)
{
    var pricesForExport = product.Prices; // Read from memory. No database query for prices.
}

// Only after writes from another server or straight in SQL:
await prices.FillCacheAsync(cancellationToken);
```

`BeginReadScope`, `PreloadAsync` and `PreloadProductsAsync` still exist so older code compiles, but they are not needed. Ekom loads the price cache by itself.

## Related pages

- [Catalog imports](imports.md#update-only-prices-from-an-erp-system): an ERP price feed and how the importer writes prices.
- [Variant App](variant-app.md): edit variant prices in the backoffice.
- [Catalog and products](catalog-and-products.md): read products and their prices.
- [Events](../reference/events.md): every event Ekom raises, including price changes.
- [Configuration reference](../reference/configuration-reference.md): all `Ekom` settings.

[Back to Guides](README.md)
