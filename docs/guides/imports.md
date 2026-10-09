# Catalog imports

`Ekom.Services.IImportService` synchronizes external catalog data into Umbraco content, media, and sellable stock. Umbraco 17 and 18 also support warehouse-stock imports. It is a synchronous server-side API. Ekom does not expose it as a public HTTP import endpoint; create an authenticated integration boundary appropriate to the site.

## Choose the operation

| Method | Scope |
| --- | --- |
| `FullSync(ImportData, parentKey, syncUser)` | Reconciles category/product trees in scope and can delete content absent from the payload. |
| `MoveSync(ImportData, parentKey, syncUser)` | Reconciles category parent relationships only. |
| `CategorySync(ImportData, categoryKey, syncUser)` | Synchronizes one category branch and its supplied products. |
| `ProductSync(ImportProduct, parentKey, mediaRootKey, syncUser, forceUpdate)` | Creates/updates one complete product, including groups, variants and media. |
| `ProductUpdateSync(...)` | Updates an existing product's own values; missing identifier throws and children/media are not synchronized. |
| `CategoryUpdateSync(...)` | Updates one existing category. |
| `VariantGroupSync(...)` | Synchronizes one group under a product. |
| `VariantSync(...)` | Synchronizes one variant under a group without deleting siblings. |
| `VariantUpdateSync(...)` | Updates an existing variant by external identifier. |
| `SyncProductMedia(...)` / `SyncVariantMedia(...)` | Synchronizes a selected media type/content field. |

Only one `FullSync`, `MoveSync`, or `CategorySync` can run in-process at a time; a competing operation throws `InvalidOperationException`. Single-entity methods do not use that same guard. Coordinate imports across application nodes externally.

## Identity and hierarchy

Every `ImportBase` requires:

- `Identifier`: stable external identity, persisted as Ekom's import identifier and used to match existing content.
- `NodeName`: Umbraco content name.
- `Title`: dictionary keyed by culture.

Do not use a mutable SKU as `Identifier` unless the external system guarantees it is stable. Categories use `ParentIdentifier` to describe hierarchy. A product's first resolvable `Categories` identifier becomes its primary parent; later identifiers are additional category relationships.

## Minimal product sync

```csharp
using Ekom.Models.Import;
using Ekom.Services;

var product = new ImportProduct
{
    Identifier = "erp-product-123",
    NodeName = "Example product",
    Title = new Dictionary<string, object>
    {
        ["en-US"] = "Example product",
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

`ProductSync` requires at least one category identifier that resolves in the selected catalog scope. It synchronizes the supplied `VariantGroups`; groups absent from that product payload are deleted, and variants absent from a supplied group are deleted. Use `ProductUpdateSync` for a product-only patch that must not reconcile children.

## Full synchronization is authoritative

Treat `FullSync` as a desired-state operation:

- Products absent from the payload are deleted unless their `ekmDisableSync` property is true.
- If `ImportData.RecycleBinKey` identifies a content node, absent products are unpublished and moved there instead of deleted.
- A returning product can be restored to its primary category; `ProductProcessKey` participates in the product save flow for that recovery setup.
- Products whose new primary category cannot be resolved are deleted or moved to the configured recycle node.
- Variant groups and variants missing from a synchronized product tree are deleted.
- Full product reconciliation refuses a payload with no product-to-category connections.

Test the exact payload and scope in a non-production database before enabling deletion. `parentKey` determines the catalog root in which content is resolved; an omitted key uses the import service's root resolution.

### Moves and loaded content

Umbraco updates descendant paths when a parent moves. Imports must not subsequently save the pre-move descendant objects. After successful product moves, Ekom refreshes the affected, already-loaded descendants in bounded ID batches before importing variant groups and variants. Reconciliation and recycle-to-processing moves are coalesced before that product's children are processed. Category moves likewise refresh affected loaded descendants before they are reused. This does not reload the full catalog or perform per-variant lookups; products that did not move add no refresh reads.

Successful deletions evict the deleted root and its loaded subtree from the import's collections and indexes. Canceled or failed required move/delete operations stop processing rather than treating the operation as successful. These checks do not roll back earlier import changes, and refreshing loaded objects does not repair an already-corrupt database path.

If a requested descendant is missing, trashed, newly sync-disabled, or has an unexpected parent when refreshed, the import stops before processing that product's children rather than reusing an unsafe snapshot. Parent buckets and canonical lists are compacted at deletion-stage boundaries, avoiding a full sibling-bucket scan for each deleted group or variant.

Refresh cost scales with the affected loaded descendants. Imports moving many large product trees need additional reads; benchmark a move-heavy feed before deployment. These changes add no startup scans or migrations and do not provide distributed coordination. Full, single-product, and media jobs that share a catalog should be coordinated by the integration across application instances; unrelated backoffice or external writes can still overlap.

Before deployment, verify this behavior against a non-production Umbraco database:

1. Create a product with a variant group and variants beneath one category, with a destination category at a different tree depth.
2. Import the product into that destination and change a variant property/comparer so a descendant save is exercised, not skipped by change detection.
3. Reload the product, group, and variants. Check that each path includes its current parent chain, each level agrees with that path, and the imported descendant values were saved.
4. Repeat with restoration from recycle and the recycle-to-processing save path. Test both changed and unchanged product comparers, and run the same import again to confirm stability.
5. Compare a no-move feed with a move-heavy feed: record duration and refresh read counts. Include canceled moves/deletions and missing refreshed descendants; verify that stale descendants are not subsequently saved.

## Change detection and save policy

`Comparer` can be supplied by the integration. When omitted, Ekom computes a SHA-256 hash of the relevant serialized model. Child collections, media, stock, warehouse stock, and transient `EventProperties` are selectively excluded from the parent content hash because they are processed separately.

`forceUpdate: true` bypasses product/group/variant content comparison for `ProductSync` or `ProductUpdateSync`. It is useful after a schema mapping change.

Each imported entity has:

| Property | Behavior |
| --- | --- |
| `SaveEvent` | `SavePublish` (default), `Save`, or `Unpublish`. |
| `PreservePublishStatus` | Keeps existing publish state instead of forcing `SaveEvent`; creation still needs a defined state. |
| `PreserveExistingValues` | Preserves existing images, description and files when corresponding imported values are null/empty. |
| `PreservePrimaryCategory` | Keeps the physical parent category of an existing product. The first `Categories` value is used only when creating a new product. |
| `SortOrder` | Applies Umbraco sibling sort order when supplied. |
| `CreateDate` / `UpdateDate` | Optional content dates; omitted values preserve current dates. |
| `AdditionalProperties` | Values written to matching Umbraco property aliases. |
| `EventProperties` | Transient data available to import event handlers and not automatically persisted. |

Property dictionaries such as `Title`, `Slug`, `Description`, and `Summary` support culture-keyed values. `AdditionalProperties` is the extension point for site-specific document-type properties; values must match the target editor's expected storage format.

For `FullSync` and `CategorySync`, the supplied `ImportData.ProductProcessKey` and `RecycleBinKey` define staging categories. When a product's immediate parent is either category, the product, its variant groups, and its variants are saved only, regardless of their `SaveEvent` or `PreservePublishStatus`. The already loaded product is passed down to group and variant saves; this check adds no per-child content lookups or ancestor traversal. Existing product moves from recycle to processing are unchanged. Outside these categories, existing save/publish policy remains in effect, so `SavePublish` with `PreservePublishStatus = false` still attempts to publish newly imported children. Single-entity sync methods do not receive these staging keys and retain their existing behavior.

## Media

`ImportBase.Images` and product/variant `Files` accept:

- `ImportMediaFromUdi` for an existing Umbraco media UDI
- `ImportMediaFromExternalUrl`
- `ImportMediaFromBytes`
- `ImportMediaFromBase64`

New media is stored under `ImportData.MediaRootKey` or the method's `mediaRootKey`. External/byte/base64 models support identifiers/comparers, sort order and `ImportMediaAction`. A zero `ImportData.MediaRootKey` makes full/category sync skip loading a media root; single product/variant media operations require the supplied root to resolve.

`SyncProductMedia` and `SyncVariantMedia` resolve the requested non-trashed content by import identifier across the content tree, including unpublished and `ekmDisableSync` content. They load only relevant media under the supplied root: incoming identifiers, comparers and UDI keys, plus existing references in the selected field so untouched media remains available for ordering. These are media-only operations; they do not reconcile product values, categories, children or stock. Changed published content follows the existing save/publish path; unpublished content is saved without publishing.

## Stock and warehouse stock

`ImportProduct.Stock` and `ImportVariant.Stock` call `SetStockAsync`; values are absolute snapshots, not deltas. With `PerStoreStock`, supply the relevant `StoreAlias`. Coordinate snapshots with active reservations because an absolute set does not account for external quantities held elsewhere.

On Umbraco 17 and 18, warehouse entries are also desired mutations, but only listed identities change:

```csharp
product.WarehouseStock =
[
    new ImportWarehouseStock
    {
        StoreAlias = "Store",
        WarehouseKey = warehouseKey,
        Balance = 12m,
    },
    new ImportWarehouseStock
    {
        StoreAlias = "Store",
        WarehouseKey = oldWarehouseKey,
        Clear = true,
    },
];
```

The product/variant must have a non-empty SKU. `Clear = true` removes the balance; otherwise `Balance` is set. Omitted warehouse identities remain unchanged. Failures are logged per entry. Warehouse balances are display-only and do not affect checkout.

## Events and error handling

All supported Umbraco integrations expose these static async events:

- `ImportEvents.CategorySaveStarting`
- `ImportEvents.ProductImportEvaluating`
- `ImportEvents.ProductSaveStarting`
- `ImportEvents.VariantSaveStarting`
- `ImportEvents.SyncFinished`

Starting-event arguments expose the Umbraco `IContent`, import model, creation state, and media no-change flags. `SyncFinished` reports saved model lists and `ImportSyncType`.

### Product eligibility

Subscribe to `ProductImportEvaluating` to exclude incoming products based on existing content properties without loading the product tree again. `ImportProductEvaluatingEventArgs` exposes the incoming `ImportProduct`, nullable existing `ProductContent`, resolved `ImportRootKey`, and `ExcludeFromImport` (initially false). Scope handlers using `ImportRootKey` and inspect the supplied content directly.

```csharp
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

Register `ImportEvents.ProductImportEvaluating += EvaluateProductAsync` once at startup and unsubscribe at shutdown. Handlers run sequentially; exclusion is cumulative, so assigning false after an earlier exclusion does not reinclude the product. Treat the supplied model and content as read-only during evaluation; use this event to set eligibility, not to mutate identifiers, categories, or persisted content.

Excluding a product removes it from the effective incoming import, including its variant groups and variants:

- With missing-product removal enabled (`FullSync` and the product reconciliation in `CategorySync`), an existing excluded product is treated as missing: unpublished and moved to the configured recycle node, or deleted when no recycle node is configured. Already-recycled products remain there. Existing `ekmDisableSync` protection remains unchanged.
- `ProductSync` and `ProductUpdateSync` skip excluded products without removing existing content. `ProductUpdateSync` still throws when the requested existing product cannot be found, before evaluation. Normal `SyncFinished` notifications still run for successfully completed imports, but excluded products are not recorded as saved.
- A valid nonempty feed where every product is excluded still performs missing-product reconciliation. Originally empty feeds retain the no-removal safeguard, and full-import category validation runs on the original feed before exclusion.

The event uses the existing in-memory identifier index and adds no content-service reads. `ProductContent` is the matching sync-enabled node available to the current import lookup, or null; it is not a catalog-wide existence guarantee. In particular, `ProductSync` normally looks under the selected primary category unless `PreservePrimaryCategory` is true. Direct variant-only sync methods do not raise this event.

All products are evaluated before product reconciliation begins. A subscriber exception aborts the import before product moves, restoration, removal, creation, or saving; earlier category processing is not rolled back. The event does not make the import transactional.

Product save failures are attached to `ImportProduct.Exception`, grouped in logs, and included in completion data. Top-level full-sync failures are logged as critical and rethrown. Single update methods throw when their identifier cannot be found. Run imports in a background integration process, capture logs, and refresh/verify catalog caches after large operations as required by the hosting topology.
