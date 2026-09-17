# Models and Behavior

Ekom APIs return interfaces and read models backed by catalog caches and persisted order/stock data. This page documents the contracts callers most often depend on and behavior that is not obvious from property names.

## Catalog entities

Most catalog contracts inherit node metadata such as key, integer ID, alias, values, sort order, and store context.

### `IStore`

Important members include `Alias`, `Vat`, primary `Culture` and all `Cultures`, primary `Currency` and all `Currencies`, domains/root node, URL and order-number configuration, plus effective `VatIncludedInPrice`, `UserBasket`, `ShareBasketBetweenStores`, and `ApplyVatOnShipping` flags. `GetCurrentCurrency()` accounts for the active Ekom currency cookie/context.

### `IProduct`

`Price` is the effective price; `OriginalPrice` and `Prices` expose underlying price data. `DisableDiscounts` suppresses product/variant discounting. `ProductDiscountAsync` resolves the applicable product discount.

`Available` combines product/variant availability rules. `Stock` and `StockBuffer` are intentionally JSON-ignored; obtain authoritative stock through `Ekom.API.Stock`. `Backorder` causes the default checkout policy to skip stock enforcement. `PrimaryVariant` chooses the first available variant in the configured primary group, falling back to its first variant.

Products expose direct/related category relationships, images, metadata, variant groups, SKU, summary/description, and related-product methods. Several graph/navigation properties are JSON-ignored to prevent large recursive HTTP payloads.

### `ICategory`

`Products`/`ProductsAsync` returns direct products; `ProductsRecursive` includes descendant categories. `SubCategories` is direct, while `SubCategoriesRecursive` and `Ancestors` traverse the tree. `Filters[Async]` derives metafield filters. A category stock buffer applies to products in the primary category unless a product buffer overrides it.

### Variants and providers

`IVariant` and `IVariantGroup` are per-store catalog nodes. Product selection requires a variant when the product's variant structure requires one.

`IPaymentProvider` and `IShippingProvider` are per-store constrained nodes. Both expose prices and zone/amount constraints; shipping providers also expose `ShippingMethods`. Provider list APIs filter constraints before raising provider events.

## Prices

`ICalculatedPrice.Value` is a decimal and `CurrencyString` is formatted output. `IPrice` distinguishes:

- `OriginalValue`/`BeforeDiscount`
- `BeforeDiscountWithOutVat`
- `AfterDiscount` and `AfterDiscountWithOutVat`
- `WithVat` and `WithoutVat`
- `Vat`, `DiscountAmount`, `Discount`, `HasDiscount`, and `DiscountedQuantity`

`IOrderInfo.SubTotal` is an `IPrice`: use `SubTotal.WithoutVat` for the net pre-discount subtotal, while `SubTotal.Value` follows the price model's VAT mode. `GrandTotal` includes VAT and shipping after discounts. `ChargedAmount` is the final amount including order lines, shipping, payment provider, and discounts. `ChargedVat` currently aliases `Vat`; it includes shipping VAT only when configured and does not add payment-provider VAT.

## Product queries and responses

`ProductQueryBase` supplies `Page`, `PageSize`, `SearchQuery`, `Ids`, `Keys`, `Skus`, `SearchFields`, `StoreAlias`, and `AllFiltersVisible`.

`ProductQuery` adds metafield/property filters, property selectors, `OrderBy`, `FilterOutZeroPriceProducts`, a C#-only predicate `Filter`, and `RaiseEvents` (default true). Its query-string constructor recognizes `filter_*`, `property_*`, `q`, `page`/`p`, and `orderby`; controller body binding does not invoke that query-string behavior.

`ProductResponse` contains `Products`, current and total counts, page/page-size/page-count, filters, and property selector values. Filtering and paging mean `ProductCount` and `TotalProductCount` are not interchangeable.

`SearchRequest` adds node ID/type restrictions, filters, `OrderBy` (default `NoOrder`), and an optional Examine index. Search fields support exact, wildcard, fuzzy, and the API's `FuzzyAndWilcard` enum spelling.

## Orders

### `IOrderInfo`

An order/cart has a `UniqueId`, `OrderNumber`, status, store/culture, customer, consent/tracking, providers, gift cards, coupon/order discount, dates, quantities, totals, and read-only lines. Mutate it through `Ekom.API.Order`; direct setters on some model properties do not persist by themselves.

`GetOrderAsync(Guid)` can return final orders. Current-cart methods enforce current basket semantics. `ReservationIds` is the current name for stock hold IDs and defaults to the legacy `HangfireJobs` contract for compatibility.

### `IOrderLine`

Each line has a unique `Key`, product/optional variant keys and snapshots, quantity, VAT, amount, discount/coupon, settings, and `OrderLineInfo`. `OrderInfo` is JSON-ignored to avoid a cycle.

Linked line groups use parent/child line metadata and should be created through `AddLinkedOrderLinesAsync` or the HTTP `linkedProducts` payload so validation and persistence are atomic. See [Linked order lines](../guides/linked-order-lines.md).

### Mutation settings

`OrderSettings` controls events and carries optional existing order, dynamic request, custom data, Algolia query ID, consent, and tracking. `FireEvents` is the master switch. `AddOrderSettings` adds `OrderAction` and optional variant key; `RemoveOrderSettings` adds variant key; `ChangeOrderSettings` controls the status-changing event; `DiscountOrderSettings` carries the coupon.

`OrderAction` governs add behavior; the default is `AddOrUpdate`. The HTTP `OrderRequest` allows decimal quantity and linked products, while its `OrderlineRequest` update model uses a non-negative integer quantity.

## Discounts

`IDiscount` represents configured order/product discount rules; `IProductDiscount` specializes product discount behavior. `Ekom.API.Discounts` returns configured read models, while order coupon methods validate coupon inventory and applicable store discount.

`OrderDiscountCalculationRequest` is a non-cart calculation contract: `CouponCode`, `StoreAlias`, and lines containing client ID, SKU/variant identity, quantity, and optional case-insensitive pricing context. The result reports whether it applied, constraints/applicable lines, discount identity, currency/totals, messages, and per-line before/after/discount/VAT values.

## Primary stock and reservations

Primary stock is keyed by product/variant `Guid`, globally or per store depending on configuration. A missing cache row reads as zero. Effective availability can subtract product/category/variant stock buffers.

`StockReservationRequest` uses a positive `Quantity`, optional `MinimumRemainingStock`, store/coupon/discount identity, duration, idempotency key, order ID, and payment-attempt ID. This differs from the compatibility facade `ReserveStockAsync`, whose `value` must be negative.

Reservation states are `Active`, `Consumed`, `Released`, and `Expired`. Results explicitly distinguish creation, idempotent repeats, conflicts, insufficient stock, terminal repeats, not found/not due, and late consumption. Consuming keeps stock deducted; releasing/expiring restores it once. See [Stock reservations](../guides/reservations.md).

## Warehouse stock

Warehouse inventory is isolated from primary stock and is keyed by normalized `(store alias, warehouse key, SKU)`.

- `WarehouseDefinition`: key, code, name, sort order, visibility.
- `WarehouseStockBalance`: persisted non-negative balance and update date.
- `WarehouseStockLevel`: visible definition plus nullable balance; null means no persisted balance, not zero.
- `WarehouseStockMutationRequest`: `Set` with a balance, or `Clear`.
- `WarehouseStockBatchResult`: ordered entries and inserted/updated/cleared/unchanged/failed counts.

SKUs are trimmed and upper-cased. Reads expose only visible published warehouses in the general list APIs. Setting an unchanged value and clearing an absent value return `Unchanged`. Batch failures are per-entry and do not erase successful entries.

## Serialization and HTTP behavior

Ekom controllers serialize interface-backed domain models. JSON-ignored navigation/stock properties may be available in C# but absent over HTTP. Manager/backoffice controllers apply camel-case output explicitly; storefront model binding is generally case-insensitive, so examples use camel case for consistency.

Do not use serialized catalog/order objects as write DTOs unless an endpoint explicitly accepts that type. Use `OrderRequest`, `OrderlineRequest`, `PaymentRequest`, warehouse mutation models, or the documented service methods.

## Related reference

- [.NET API](dotnet-api.md)
- [HTTP API](http-api.md)
- [Events](events.md)
- [Configuration](configuration-reference.md)
