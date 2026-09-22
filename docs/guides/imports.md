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

## Media

`ImportBase.Images` and product/variant `Files` accept:

- `ImportMediaFromUdi` for an existing Umbraco media UDI
- `ImportMediaFromExternalUrl`
- `ImportMediaFromBytes`
- `ImportMediaFromBase64`

New media is stored under `ImportData.MediaRootKey` or the method's `mediaRootKey`. External/byte/base64 models support identifiers/comparers, sort order and `ImportMediaAction`. A zero `ImportData.MediaRootKey` makes full/category sync skip loading a media root; single product/variant media operations require the supplied root to resolve.

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
- `ImportEvents.ProductSaveStarting`
- `ImportEvents.VariantSaveStarting`
- `ImportEvents.SyncFinished`

Starting-event arguments expose the Umbraco `IContent`, import model, creation state, and media no-change flags. `SyncFinished` reports saved model lists and `ImportSyncType`.

Product save failures are attached to `ImportProduct.Exception`, grouped in logs, and included in completion data. Top-level full-sync failures are logged as critical and rethrown. Single update methods throw when their identifier cannot be found. Run imports in a background integration process, capture logs, and refresh/verify catalog caches after large operations as required by the hosting topology.
