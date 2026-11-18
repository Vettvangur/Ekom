# Models and behavior

[Ekom docs](../README.md) / [Reference](README.md) / Models and behavior

Ekom's APIs return interfaces such as `IStore`, `IProduct`, `IPrice` and `IOrderInfo`. This page lists the members you use most, and the behavior that the member names do not tell you.

Catalog objects come from Ekom's in-memory cache. Order and stock objects come from the database. Treat both as read-only: change data through the [.NET API](dotnet-api.md), not by setting properties on these objects.

On this page:

- [Catalog entities](#catalog-entities)
- [Prices](#prices)
- [Product queries and responses](#product-queries-and-responses)
- [Orders](#orders)
- [Discounts](#discounts)
- [Sellable stock and reservations](#sellable-stock-and-reservations)
- [Warehouse stock](#warehouse-stock)
- [Serialization over HTTP](#serialization-over-http)

## Catalog entities

Guide: [Catalog and products](../guides/catalog-and-products.md).

Catalog objects share node data from Umbraco: key, node ID, title, sort order, property values and the store they belong to. A product, category or variant exists once for each store.

### `IStore`

| Member | Type | Description |
| --- | --- | --- |
| `Alias` | `string` | The store alias, used throughout the APIs. |
| `Vat` | `decimal` | The store's VAT rate. |
| `VatIncludedInPrice` | `bool` | Whether prices entered in Umbraco include VAT. |
| `Culture`, `Cultures` | `CultureInfoDto`, `List<CultureInfoDto>` | The request's culture when the store has it, otherwise the store's first culture; and all the store's cultures. |
| `Currency`, `Currencies` | `CurrencyModel`, `List<CurrencyModel>` | The active currency and all currencies of the store. |
| `GetCurrentCurrency()` | `CurrencyModel` | The currency in the customer's `EkomCurrency-{alias}` cookie; otherwise the currency that matches the request's culture; otherwise the store's first currency. `Currency` returns the same value. |
| `Domains` | `IEnumerable<UmbracoDomain>` | The store's Umbraco domains. |
| `StoreRootNode`, `StoreRootNodeId` | | The store's root content node. |
| `Url`, `UrlPrefix(string culture)` | `string` | The store URL and the URL prefix for a culture. |
| `OrderNumberPrefix`, `OrderNumberTemplate` | `string` | How order numbers are built. |
| `UserBasket` | `bool` | The store's `userBasket` property, or `Ekom:UserBasket` when the property is not set. |
| `ShareBasketBetweenStores` | `bool` | The store's `shareBasketBetweenStores` property, or `Ekom:ShareBasket`. |
| `ApplyVatOnShipping` | `bool` | The store's `applyVatOnShipping` property, or `Ekom:ApplyVatOnShipping`. |

Guide: [Stores](../guides/stores.md).

### `IProduct`

| Member | Description |
| --- | --- |
| `Price` | The price to show, with discounts applied. An `IPrice`. |
| `OriginalPrice`, `Prices` | The underlying price data. Not included in JSON. |
| `DisableDiscounts` | `true` when discounts must not apply to the product and its variants. |
| `ProductDiscountAsync(string? price = null, CancellationToken ct = default)` | The product discount that applies, or `null`. |
| `Available` | Whether the product can be bought. See [How Ekom decides that a product is available](../guides/stock.md#how-ekom-decides-that-a-product-is-available). |
| `Backorder` | `true` when the product can be bought without stock. The default checkout stock policy skips these lines. |
| `Stock`, `StockBuffer` | Not included in JSON. Read stock through `Ekom.API.Stock`. |
| `SKU`, `Summary`, `Description`, `Images`, `Metafields`, `Vat` | Product data. |
| `VariantGroups` | The product's variant groups. |
| `PrimaryVariantGroup`, `AllVariants` | Not included in JSON. |
| `PrimaryVariant` | The first available variant in the primary variant group, or its first variant when none is available. |
| `Categories`, `CategoriesIds`, `CategoryAncestors` | The categories the product is in. Not included in JSON. |
| `RelatedProductsAsync(int count = 4, CancellationToken ct = default)` | Related products. |

Navigation members are left out of JSON so that HTTP responses do not grow large or loop.

### `ICategory`

| Member | Description |
| --- | --- |
| `ProductsAsync(ProductQuery? query = null, CancellationToken ct = default)` | Products directly in the category, as a `ProductResponse`. |
| `ProductsRecursiveAsync(ProductQuery? query = null, CancellationToken ct = default)` | Products in the category and all categories below it. |
| `SubCategories` | Direct child categories. |
| `SubCategoriesRecursive`, `Ancestors`, `RootCategory` | All categories below, the categories above, and the top category. Not included in JSON. |
| `FiltersAsync(bool filterable = true, CancellationToken ct = default)` | Filter groups built from the products' metafields. |
| `StockBuffer` | Units held back from sale for products in the category, unless the product or variant sets its own buffer. |
| `HasProducts()`, `VirtualUrl` | Whether the category has products, and whether its URL is virtual. |

### `IVariant` and `IVariantGroup`

A variant is a version of a product, such as a size or a colour. Variant groups hold the variants of a product. Each exists once for each store.

`IVariant` has its own `Price`, `OriginalPrice`, `Prices`, `Vat`, `SKU`, `Backorder`, `Stock`, `Available`, `Description` and `Images`, and points back to its product through `ProductKey`, `ProductId` and `Product`. `Product`, `VariantGroup` and `StockBuffer` are not included in JSON. A product whose variant structure requires a variant cannot be added to the basket without one; Ekom throws `VariantRequiredException`.

### Payment and shipping providers

`IPaymentProvider` and `IShippingProvider` exist once for each store. Both have prices, a zone (the countries they serve) and an order amount range. Shipping providers also have `ShippingMethods`. The provider list methods filter by these limits before the [provider events](events.md#provider-events) run.

### Custom date and time properties

The Umbraco package stores custom date and time properties in Ekom's catalog cache as text in the round-trip format (`O`), not in the server's display culture. This covers the `Umbraco.DateTime`, `Umbraco.DateTimeUnspecified` and `Umbraco.DateTimeWithTimeZone` property editors. A UTC value is stored as `2026-04-10T14:03:55.0000000Z`. This avoids day and month mix-ups when other code, such as the Algolia plugin, turns the text into a date.

- UTC and offset information is kept when the source value has it.
- A date without a time zone stays without one. Ekom does not convert it.
- Date-only and time-only editors, custom editors, and text properties that look like dates are not changed.

When you read one of these values as a string, format it yourself for display.

## Prices

`ICalculatedPrice` is one amount: `Value` (`decimal`) and `CurrencyString` (the formatted text).

`IPrice` is a price with its VAT and discount:

| Member | Description |
| --- | --- |
| `Value` | The price after discounts, with VAT. The same amount as `WithVat`. |
| `OriginalValue` | The price before discounts, as a `decimal`. |
| `BeforeDiscount`, `AfterDiscount`, `DiscountAmount` | Before discounts, after discounts and the difference, as the store shows prices: with VAT when the store's prices include VAT, without VAT when they do not. |
| `BeforeDiscountWithVat`, `BeforeDiscountWithOutVat` | Before discounts, with and without VAT in every store. |
| `AfterDiscountWithOutVat` | After discounts, without VAT in every store. With VAT, use `WithVat`. |
| `WithVat`, `WithoutVat` | After discounts, with and without VAT in every store. |
| `Vat` | The VAT amount. |
| `Discount`, `HasDiscount` | The discount applied, and whether there is one. |
| `DiscountedQuantity` | How many units the discount applies to. `0` by default. |
| `Currency` | The currency. |

Order totals:

| Member of `IOrderInfo` | Description |
| --- | --- |
| `SubTotal` | The lines before discounts, as an `IPrice`. Use `SubTotal.WithVat` or `SubTotal.WithoutVat`. |
| `GrandTotal` | The order value after discounts, with VAT, shipping and payment fees, before gift cards. |
| `ChargedAmount` | What the customer still has to pay after gift cards, never below `0`. Use it in payment requests, not `GrandTotal`. |
| `ChargedVat` | VAT of the whole order: lines, shipping and the payment fee. `GrandTotalWithOutVat + ChargedVat` is `GrandTotal`. |
| `Vat` | VAT of the lines, plus shipping VAT when `ApplyVatOnShipping` is on for the store. Does not include VAT on the payment fee. |
| `OrderLineTotal`, `OrderLineTotalWithOutVat` | Total of the lines. |
| `DiscountAmountWithVat`, `DiscountAmountWithOutVat` | Every product and order discount on the lines, with and without VAT in every store. |
| `ProductDiscountAmountWithVat`, `ProductDiscountAmountWithOutVat` | The product discounts only, with and without VAT in every store. |
| `DiscountAmount`, `ProductDiscountAmount` | The same discounts as the store shows prices: with VAT when the store's prices include VAT, without VAT when they do not. |

The class `OrderInfo` also has `GrandTotalWithOutVat`: the grand total before gift cards, without VAT.

Two sets of totals add up in every store, whether or not its prices include VAT:

| | With VAT | Without VAT |
| --- | --- | --- |
| Lines before discounts | `SubTotal.WithVat` | `SubTotal.WithoutVat` |
| Minus discounts | `DiscountAmountWithVat` | `DiscountAmountWithOutVat` |
| Lines after discounts | `OrderLineTotal` | `OrderLineTotalWithOutVat` |
| With shipping and the payment fee | `GrandTotal` | `GrandTotalWithOutVat` |

Do not mix the columns, and do not put `DiscountAmount` in either one: in a store whose prices exclude VAT it is the amount without VAT. `SubTotal` is calculated with the store's VAT rate, so in an order whose lines have different VAT rates, `SubTotal.WithVat` minus `DiscountAmountWithVat` can differ from `OrderLineTotal`. See [Grand total and charged amount](../guides/orders-and-baskets.md#grand-total-and-charged-amount).

## Product queries and responses

Guide: [Catalog and products](../guides/catalog-and-products.md).

`ProductQueryBase` has the members shared by product queries and searches:

| Member | Type | Default | Description |
| --- | --- | --- | --- |
| `Page`, `PageSize` | `int?` | `null` | Paging. Both must be set for paging to apply. |
| `SearchQuery` | `string` | empty | Text to search for. |
| `SearchFields` | `List<EkomSearchField>` | empty | Fields to search. `SearchType` is `Exact`, `Wildcard`, `Fuzzy` or `FuzzyAndWilcard` (the default, spelled this way in the code). |
| `Ids`, `Keys`, `Skus` | lists | empty | Products to load, for the by-ID, by-key and by-SKU methods. |
| `StoreAlias` | `string` | empty | The store. |
| `AllFiltersVisible` | `bool` | `false` | `true`: the response's filters are built from all products, before the query's filters and search. `false`: only from the matching products. |

`ProductQuery` adds:

| Member | Type | Default | Description |
| --- | --- | --- | --- |
| `MetaFilters` | `Dictionary<string, List<string>>?` | empty | Metafield values to filter by. |
| `PropertyFilters` | `Dictionary<string, List<string>>?` | empty | Property values to filter by. |
| `PropertySelectors`, `PropertySelectorsSeparator` | | empty | Properties to collect distinct values from, for building filter menus. |
| `OrderBy` | `OrderBy?` | `Ekom:DefaultProductOrderBy` | Sort order. `NoOrder` keeps the list order. |
| `FilterOutZeroPriceProducts` | `bool` | `false` | Leaves out products with a price of `0`. |
| `Filter` | `Func<IProduct, bool>?` | `null` | Your own condition. C# only. |
| `RaiseEvents` | `bool` | `true` | `false` skips the filter service and the catalog events. |

`new ProductQuery(IQueryCollection query)` reads a query string: `filter_*`, `property_*`, `q`, `page` or `p`, and `orderby`. The HTTP routes bind the body instead and do not use this constructor.

`SearchRequest` is used by `ProductSearchAsync`. It adds `SearchNodeById`, `NodeTypeAlias`, `MetaFilters`, `PropertyFilters`, `OrderBy` (default `NoOrder`) and `ExamineIndex`.

A `ProductResponse` applies a query in this order:

1. property selectors,
2. metafield and property filters,
3. search,
4. the [`IProductFilterService`](services-and-dependency-injection.md#filter-product-lists) and the `BeforeReturnProductsAsync` event,
5. the `Filter` condition and `FilterOutZeroPriceProducts`,
6. sorting, then paging.

| Member of `ProductResponse` | Description |
| --- | --- |
| `Products` | The products on the requested page, or all matching products without paging. |
| `ProductCount`, `TotalProductCount` | Both hold the number of matching products before paging. |
| `Page`, `PageSize`, `PageCount` | Paging values. Set only when the query has both `Page` and `PageSize`. |
| `Filters` | Filter groups for the products. |
| `PropertySelectors` | The values collected for each property selector. |

When the query is `null`, Ekom returns the products as they are, after the filter service and the event, without sorting or paging.

## Orders

Guide: [Orders and baskets](../guides/orders-and-baskets.md).

### `IOrderInfo`

An `IOrderInfo` is a basket or an order. A basket is an order that has not been paid yet.

| Member | Description |
| --- | --- |
| `UniqueId` | The order key. |
| `OrderNumber`, `ReferenceId` | The order number shown to customers, and the database reference number. |
| `StoreInfo`, `Culture` | The store and culture the order was made in. |
| `OrderLines`, `TotalQuantity` | The lines, read-only, and the total quantity. |
| `CustomerInformation` | Customer and shipping details. |
| `PaymentProvider`, `ShippingProvider` | The chosen providers, or `null`. |
| `Coupon`, `Discount`, `Giftcards` | The order coupon, the order discount and the gift cards. |
| `Consent`, `Tracking` | The customer's consent and marketing attribution. |
| `CustomData` | Your own values saved on the order. See [Order custom data](../guides/order-custom-data.md). |
| `OrderStatus` | The legacy status, `Ekom.Utilities.OrderStatus`. |
| `CreateDate`, `UpdateDate`, `PaidDate` | Dates. `PaidDate` is `null` until the order is paid. |
| `ReservationIds` | The stock reservations held for the order. `HangfireJobs` is an older name for the same list. |
| Totals | See [Prices](#prices). |

`Order.GetOrderAsync(Guid)` returns any order, including completed orders. The methods without a key return only the current basket.

Some members have setters, but setting them does not save anything. Change orders through `Ekom.API.Order`.

### `IOrderLine`

| Member | Description |
| --- | --- |
| `Key` | The line key. Use it to change or remove the line. |
| `ProductKey`, `VariantKey` | The product and the variant, if any. |
| `Product`, `Variant` | A snapshot of the product and variant, saved with the line. |
| `Quantity`, `Amount`, `Vat` | Quantity, price (`IPrice`) and VAT rate. |
| `Coupon`, `Discount` | The line coupon and discount. |
| `OrderLineInfo`, `Settings` | Line data, such as the `orderline...` values, and line settings. |
| `OrderInfo` | The order. Not included in JSON. |

Linked lines (add-ons that belong to another line) are saved with links between parent and child lines. Create them with `AddLinkedOrderLinesAsync` or the `linkedProducts` field of `POST /ekom/order/add`, so that they are checked and saved together. See [Linked order lines](../guides/linked-order-lines.md).

### Order settings

| Class | Adds | Used by |
| --- | --- | --- |
| `OrderSettings` | `FireEvents` (default `true`), `FireOnOrderUpdatedEvent` (default `true`), `IsEventHandler`, `OrderInfo`, `ClearShippingProvider`, `CustomData`, `AlgoliaQueryId`, `Consent`, `Tracking` | Most `Order` methods. |
| `AddOrderSettings` | `OrderAction` (default `AddOrUpdate`), `VariantKey` | Adding lines. |
| `RemoveOrderSettings` | `VariantKey` | `RemoveOrderLineProductAsync`. |
| `ChangeOrderSettings` | Nothing | `UpdateStatusAsync`. |
| `DiscountOrderSettings` | `Coupon` | Coupon methods. |

`OrderAction` values: `AddOrUpdate` adds to an existing line of the same product, `Set` sets the quantity, `New` always adds a new line.

Over HTTP, `OrderRequest` (for `POST /ekom/order/add`) takes a decimal quantity and linked products. `OrderlineRequest` (for changing a line) takes a whole-number quantity of `0` or more.

### Status snapshot

`OrderData.Status` and `OrderInfo.Status` return an `Ekom.Models.Orders.OrderStatusSnapshot`. Reading it changes nothing.

| Member | Description |
| --- | --- |
| `OrderStatus` | `Draft`, `Open`, `Closed` or `Cancelled`. |
| `PaymentStatus` | `Pending`, `Authorized`, `Paid`, `PartiallyPaid`, `Failed`, `PartiallyRefunded`, `Refunded` or `Voided`. |
| `FulfillmentStatus` | `Unfulfilled`, `Picking`, `Picked`, `Packed`, `PartiallyFulfilled` or `Fulfilled`. |
| `RequiresReview` | `true` when Ekom could not fully work out the statuses from an older order's history. |
| `LegacyStatus` | The legacy status as text. |
| `Revision` | Changes when the order is saved. A status change from the manager must name the revision it expects. |
| `AutoCloseSuppressed` | `true` when staff reopened the order, so it does not close automatically. |
| `FulfilledQuantities` | Fulfilled quantity per line key. |

The three statuses are `null` when an older order's history does not show them. They are written to JSON as names. Ekom keeps the legacy status in step: legacy `Closed` maps to Closed / Paid / Fulfilled, and legacy `ReadyForDispatch` to Open / Paid / Unfulfilled. See [Order, payment, and fulfillment statuses](../guides/order-statuses.md) for the full mapping.

## Discounts

Guide: [Discounts and coupons](../guides/discounts.md).

`IDiscount` is a configured discount rule for orders or products. `IProductDiscount` is a discount on products. `Ekom.API.Discounts` returns the configured discounts; the coupon methods on `Order` check coupon uses and the store's discounts.

`OrderDiscountCalculationRequest` prices a coupon without a basket:

| Member | Description |
| --- | --- |
| `CouponCode` | Required. |
| `StoreAlias` | The store. |
| `Lines` | Each line has `ClientLineId`, `Sku` (required), `VariantSku`, `VariantKey`, `Quantity` (default `1`) and `PricingContext`, a dictionary of your own pricing values. |

The result says whether the coupon applied, which lines it applied to and why, the discount, the currency and totals, messages, and the price before and after discount and the VAT of each line.

## Sellable stock and reservations

Guides: [Stock](../guides/stock.md), [Stock reservations](../guides/reservations.md).

Sellable stock is kept per product or variant key, for all stores or per store depending on `Ekom:PerStoreStock`. A product with no stock row reads as `0`. Availability subtracts the stock buffer of the variant, product or category.

`StockReservationRequest` asks for a positive `Quantity`. It can also carry `MinimumRemainingStock`, a store, a coupon or discount, a duration, an idempotency key, an order key and a payment attempt key. This differs from `Stock.ReserveStockAsync`, whose `value` must be negative.

A reservation is `Active`, `Consumed`, `Released` or `Expired`. Using it up (`Consumed`) keeps the stock deducted; releasing or expiring it returns the stock once.

## Warehouse stock

Warehouse stock is separate from sellable stock. A balance belongs to a store, a warehouse and a SKU. Guide: [Stock](../guides/stock.md#warehouse-stock).

| Type | Description |
| --- | --- |
| `WarehouseDefinition` | Key, code, name, sort order and whether it is hidden. |
| `WarehouseStockBalance` | A saved balance, never negative, and when it was updated. |
| `WarehouseStockLevel` | A visible warehouse with its balance. A `null` balance means none is saved, not `0`. |
| `WarehouseStockMutationRequest` | `Set` with a `Balance`, or `Clear`. |
| `WarehouseStockBatchResult` | The result of each entry, in order, and the number of inserted, updated, cleared, unchanged and failed entries. |

SKUs are trimmed and compared without regard to case. Setting the value that is already saved, or clearing a balance that does not exist, returns `Unchanged`.

## Serialization over HTTP

Ekom's controllers return these interfaces as JSON. Members marked "not included in JSON" on this page are available in C# but missing over HTTP. Manager and backoffice routes write camel-case names. Storefront routes read request bodies without regard to the case of property names; the examples use camel case.

Do not send these objects back to Ekom to change data. Use the request types the routes accept (`OrderRequest`, `OrderlineRequest`, `PaymentRequest`, warehouse change requests) or the methods in the [.NET API](dotnet-api.md).

## Related pages

- [.NET API](dotnet-api.md): the methods that return these objects.
- [HTTP API](http-api.md): the routes that return them as JSON.
- [Events](events.md): the events that pass them to your code.
- [Orders and baskets](../guides/orders-and-baskets.md): how orders are built and changed.
- [Order, payment, and fulfillment statuses](../guides/order-statuses.md): what each status means.

[Back to Reference](README.md)
