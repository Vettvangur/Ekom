[Ekom docs](../README.md) / [Reference](README.md) / HTTP API

# HTTP API

Ekom adds HTTP routes under `/ekom` to the site. A storefront that runs in the browser or on another server uses them to read the catalog, change the basket and pay. This page lists every route with its method, parameters and responses.

The routes are separate from the C# API on the server. To call Ekom from your own server code, use the [.NET API](dotnet-api.md). For a walk-through of a storefront built on these routes, read [Headless storefronts](../guides/headless.md).

Routes are shown as declared in the source. ASP.NET Core matches them without regard to case.

On this page:

- [Security and request context](#security-and-request-context)
- [General data](#general-data)
- [Catalog](#catalog)
- [Providers](#providers)
- [Orders and carts](#orders-and-carts)
- [Checkout](#checkout)
- [Order-discount integration](#order-discount-integration)
- [Backoffice and manager APIs](#backoffice-and-manager-apis)

## Security and request context

The storefront routes (`/ekom/api`, `/ekom/catalog`, `/ekom/provider`, `/ekom/order`, `/ekom/checkout` and `/ekom/mvcCheckout`) are anonymous. Ekom does not require a login, an API key or a custom header for them. If some data or operation must not be public, restrict it in the host or a proxy.

`GET /ekom/order/{orderId}` returns any order, including completed orders, to anyone who knows its key. If order keys must stay secret, protect that route.

The basket is found through Ekom's basket cookie and the store of the current request. On `/ekom` routes Ekom also sets the request's store from a `storeAlias` query value, form field or `storeAlias` request header when one is sent. Routes that list `storeAlias` as a route or body value use that value.

### Limits

| Limit | Value | What happens |
| --- | --- | --- |
| Request body size | `Ekom:Api:MaxRequestBodySize`, default 128,000 bytes, for all storefront routes and `/ekom/order-discounts` | `413 Payload Too Large` |
| `POST /ekom/order/add` body | 64,000 bytes | `400 Bad Request` |
| Fields in one request to the order and checkout routes | 200 fields, names up to 100 characters, values up to 4,096 characters | `400` with `{ "message": "..." }` |
| Fields saved on one order | 500 customer fields, 500 shipping fields and 500 custom data fields | `400` with `{ "message": "..." }` |
| Catalog search text | 200 characters | The text is cut to 200 characters. |

### Rate limits

Some routes limit how often one client can call them. Ekom enforces the limits itself, so you do not need to add `UseRateLimiter()`. A client over the limit gets `429 Too Many Requests`.

| Policy | Limit | Routes |
| --- | --- | --- |
| `order-add` | 30 requests per minute | `POST /ekom/order/add` |
| `order-update` | 60 requests per minute | `POST /ekom/order/updatecustomer`, `updatetracking`, `update/shippingprovider`, `updateshippingprovider`, `update/paymentprovider`, `updatepaymentprovider` |
| `checkout-pay` | 30 requests per minute | `POST /ekom/checkout/pay`, `POST /ekom/mvcCheckout/pay` |
| `order-coupon` | 13 requests at once, then 10 more each minute | `POST /ekom/order/coupon/apply`, `POST /ekom/order/coupon/remove`, `POST /ekom/order-discounts/calculate`, `POST /ekom/order-discounts/coupon/mark-used` |
| `order-giftcard` | 13 requests at once, then 10 more each minute | `POST /ekom/order/giftcards` |

- A client is identified by its IP address. Behind a proxy or load balancer, turn on forwarded headers in ASP.NET Core, or all visitors share one limit.
- Each server counts on its own. With three servers, a client can make three times as many requests.

### Error responses

The catalog, provider, order, checkout (`/ekom/checkout`) and order-discount routes turn Ekom's exceptions into these responses:

| Exception | Response |
| --- | --- |
| `CustomerInformationValidationException`, `InvalidCustomerEmailException`, `OrderCustomDataException`, `VariantRequiredException` | `400` with `{ "message": "..." }` |
| `CouponApplicationRejectedException` | `400` with `{ "code": "couponApplicationRejected", "message": "..." }` |
| `OrderLineNegativeException` | `400` |
| `ArgumentNullException`, `OrderLineNotFoundException`, `ProductNotFoundException`, `VariantNotFoundException`, `DiscountNotFoundException`, `DiscountUnableToFindCouponException`, `StoreNotFoundException` | `404` |
| `DiscountHasNoUsageException` | `406` |
| `NotEnoughStockException`, including `NotEnoughLineStockException` | `409` |
| `CheckoutConflictException` | `409` with a body that says what blocked checkout and whether the customer can try again |
| `EkomHttpException` | its own status code, with `{ "message": "..." }` |
| `EkomProblemDetailsException` | its problem details |
| `CheckoutPayException` | `500` |
| Any other exception | `500` with the text `An unexpected error occurred`. Ekom logs the exception. |

Validation failures for customer data are not logged as errors, and the response does not repeat the submitted values.

## General data

Base route: `/ekom/api`. Guide: [Stores](../guides/stores.md).

| Method | Route | Response |
| --- | --- | --- |
| `GET` | `/ekom/api/countries` | All countries, as `Country` records. |
| `GET` | `/ekom/api/stores` | All stores, as `IStore` records. |

## Catalog

Base route: `/ekom/catalog`. Guide: [Catalog and products](../guides/catalog-and-products.md).

- Routes for a single item return `404` when it is not found.
- Lists of products return a `ProductResponse` (see [Models and behavior](models-and-behavior.md#product-queries-and-responses)). The other lists return a JSON array.
- Routes marked `GET`, `POST` read a `ProductQuery`, `SearchRequest` or array from the request body. Send it as JSON with `POST`; many clients cannot send a body with `GET`.
- Single-item routes use the store of the current request. Product list routes use `storeAlias` from the `ProductQuery` when you set it.
- Every route that reads a query limits it for public use: `searchFields` keeps only the fields in `Ekom:Search:PublicFields`, `searchQuery` is cut to 200 characters, and `examineIndex` is ignored.

| Method | Route | Parameters | Response |
| --- | --- | --- | --- |
| `GET` | `/product/{id:guid}` | Product key. | The product. |
| `GET` | `/product/{id:int}` | Umbraco node ID. | The product. |
| `GET` | `/product/sku/{sku}` | SKU. | The product. |
| `GET` | `/product/route?route=` | Product URL path. | The product. |
| `GET`, `POST` | `/products/{categoryId:int}` | Optional `ProductQuery` body. | Products directly in the category, or `404` when the category is not found. |
| `GET`, `POST` | `/products/{categoryKey:guid}` | Optional `ProductQuery` body. | Same, by category key. |
| `GET`, `POST` | `/productsrecursive/{categoryId:int}` | Optional `ProductQuery` body. | Products in the category and all categories below it, or `404`. |
| `GET`, `POST` | `/productsrecursive/{categoryKey:guid}` | Optional `ProductQuery` body. | Same, by category key. |
| `GET`, `POST` | `/productsrecursive/route?route=` | Category URL path; optional `ProductQuery` body. | Same, by category route. |
| `GET`, `POST` | `/productsbyids` | `ProductQuery` body with `ids`. | The products. `400` without a body. |
| `GET`, `POST` | `/productsbykeys` | `ProductQuery` body with `keys`. | The products. `400` without a body. |
| `GET`, `POST` | `/productsbyskus` | `ProductQuery` body with `skus`. | The products. `400` without a body. |
| `GET`, `POST` | `/allproducts` | Optional `ProductQuery` body. | All products in the store. |
| `GET`, `POST` | `/productsearch` | `SearchRequest` body. | Matching products. |
| `GET`, `POST` | `/relatedproducts/{id:guid}/{count:int}` | Product key and number of products. | Related products. |
| `GET`, `POST` | `/relatedproducts?ids=&ids=&count=4` | Repeated product keys; `count` defaults to `4`. | Related products of all the given products, at most `count`. |
| `GET`, `POST` | `/relatedproductsbysku/{sku}/{count:int}` | SKU and number of products. | Related products. |
| `GET`, `POST` | `/relatedproductsbyskus?skus=&skus=&count=4` | Repeated SKUs; `count` defaults to `4`. | Related products of all the given SKUs, at most `count`. |
| `GET`, `POST` | `/category/{id:int}` | Umbraco node ID. | The category. |
| `GET`, `POST` | `/category/{id:guid}` | Category key. | The category. |
| `GET`, `POST` | `/category/route?route=` | Category URL path. | The category. `400` when `route` is empty. |
| `GET`, `POST` | `/categoriesbyids` | `int[]` body. | The categories. |
| `GET`, `POST` | `/categoriesbykeys` | `Guid[]` body. | The categories. `400` when the array is missing or empty. |
| `GET`, `POST` | `/rootcategories` | None. | The top-level categories of the store. |
| `GET`, `POST` | `/allcategories` | None. | All categories of the store. |
| `GET`, `POST` | `/subcategories/{id:int}` or `/subcategories/{key:guid}` | Category ID or key. | Direct child categories. |
| `GET`, `POST` | `/subcategoriesrecursive/{id:int}` or `/subcategoriesrecursive/{key:guid}` | Category ID or key. | All categories below it. |
| `GET`, `POST` | `/categoryfilters/{id:int}` or `/categoryfilters/{key:guid}` | Category ID or key. | The filter groups for the category's products. |
| `GET`, `POST` | `/metafields` | None. | All metafield definitions. |

```http
POST /ekom/catalog/allproducts
Content-Type: application/json

{
  "storeAlias": "store",
  "page": 1,
  "pageSize": 24,
  "orderBy": "DateDesc",
  "metaFilters": {
    "brand": ["Ekom"]
  }
}
```

## Providers

Base route: `/ekom/provider`. Guide: [Payment and shipping providers](../guides/providers.md).

| Method | Route | Parameters | Response |
| --- | --- | --- | --- |
| `GET` | `/paymentsproviders/{storeAlias?}` | Query: `countryCode`, `orderAmount`. | Payment providers the customer can use, sorted by sort order. `404` when the store is not found. |
| `GET` | `/paymentsprovider/{id:guid}` | Provider key. | One payment provider from the store of the current request. `404` when there is no store. A provider that does not exist gives an empty response (`204`). |
| `GET` | `/shippingproviders/{storeAlias?}` | Query: `countryCode`. | Shipping providers the customer can use. The order amount used for the amount ranges is the current basket's charged amount minus its shipping price, or `0` without a basket. `404` when the store is not found. |
| `GET` | `/shippingprovider/{id:guid}` | Provider key. | One shipping provider from the store of the current request. Same responses as the payment route. |
| `GET` | `/zones` | None. | All zones. |

The plural payment route is spelled `paymentsproviders` and the single one `paymentsprovider`.

Ekom filters by country only when `countryCode` has two letters. A provider whose zone lists no countries is then left out. It filters by amount only when the amount is above `0`; a provider whose range ends at `0` has no upper limit.

## Orders and carts

Base route: `/ekom/order`. Every route returns the updated order (`IOrderInfo`) unless the table says otherwise. Guides: [Orders and baskets](../guides/orders-and-baskets.md), [Linked order lines](../guides/linked-order-lines.md).

### Reads

| Method | Route | Response |
| --- | --- | --- |
| `GET` | `/` or `/storeAlias/{storeAlias?}` | The current basket. `404` when there is none. |
| `GET` | `/{orderId}` | Any order by its key, including completed orders. `404` when not found. |
| `GET` | `/relatedproducts/{count:int}` | Products related to the lines in the current basket. `404` when there is no basket. |
| `GET` | `/relatedproducts/storeAlias/{storeAlias?}/{count:int}` | Same, for the given store. |

### Mutations

| Method | Route | Body or query | Notes |
| --- | --- | --- | --- |
| `POST` | `/add` | `OrderRequest` as JSON, form or multipart form. | Rate policy `order-add`. Other content types get `415`. A JSON body that is not an object gets `400`. |
| `POST` | `/updatecustomer` | JSON object or form fields. | Rate policy `order-update`. `consent` and `tracking` are read as objects. Other content types get `400`. |
| `POST` | `/updatetracking` | JSON `OrderTrackingUpdateRequest`: `storeAlias`, `tracking`, optional `consent`. | Rate policy `order-update`. `400` when `storeAlias` or `tracking` is missing. |
| `POST` | `/update/shippingprovider/` or `/updateshippingprovider` | `ShippingProvider` (key) and `storeAlias`, as JSON, form or query values. Other fields are saved as provider data. | Rate policy `order-update`. `400` when either value is missing. |
| `POST` | `/update/paymentprovider/` or `/updatepaymentprovider` | `PaymentProvider` (key) and `storeAlias`, as JSON, form or query values. | Rate policy `order-update`. `400` when either value is missing. |
| `POST` | `/removeorderline` | JSON `OrderlineRequest`: `lineId`, `storeAlias`. | `400` when `storeAlias` is missing. |
| `POST` | `/Updateorderlinequantity` | JSON `OrderlineRequest`: `lineId`, `storeAlias`, `quantity`. | `quantity` is a whole number, `0` or more. |
| `POST` | `/giftcards` | JSON `GiftcardRequest`: `giftcard.code`, `storeAlias`. | Rate policy `order-giftcard`. Only the code is read; the amount comes from your `IGiftcardValidator`. `403` when no validator is registered, `404` when there is no basket, `400` when the code is missing, longer than 200 characters, or rejected. |
| `DELETE` | `/giftcards/{code}?storeAlias=` | Gift card code and store alias. | |
| `POST` | `/coupon/apply` | JSON `CouponRequest`: `coupon`, `storeAlias`. | Rate policy `order-coupon`. Returns `200` with no body when applied. Returns status `450` when a better discount is already on the order and the coupon was not applied. `400` when the code is empty. For other failures see [Error responses](#error-responses). |
| `POST` | `/coupon/remove?storeAlias=` | Store alias. | Rate policy `order-coupon`. Returns `200` with no body. |
| `POST` | `/currency?currency=` | A `CurrencyValue` configured on the store, for example `en-US`. | Changes the basket's currency, if there is a basket, and sets the `EkomCurrency-{storeAlias}` cookie for 360 days. `404` when there is no store. |

About `POST /add`:

- `OrderRequest` has `productId`, `variantId` (optional), `storeAlias`, `quantity` (decimal, default `1`), `action` (`AddOrUpdate`, `Set` or `New`; default `AddOrUpdate`), `algoliaQueryId`, `consent`, `tracking` and `linkedProducts`.
- Every other top-level field is HTML-encoded and passed on as custom data. Fields whose names start with `orderline` are saved on the new line.
- With `linkedProducts`, Ekom adds the main line and its linked lines together, always as a new line. A `null` entry in the list gets `400`. Custom data on linked lines is HTML-encoded too.

```http
POST /ekom/order/add
Content-Type: application/json

{
  "productId": "5f1c3c52-7d0e-4c7b-9a8e-0d6a1f0b2c11",
  "storeAlias": "store",
  "quantity": 2,
  "orderlineGiftMessage": "Happy birthday"
}
```

About `POST /updatecustomer`: an email address that is not valid, or a customer name or email longer than 200 characters, gets `400` with a `message`. When `customerName` is empty, the limit applies to the first and last name together.

## Checkout

Guide: [Checkout](../guides/checkout.md).

| Method | Route | Description |
| --- | --- | --- |
| `POST` | `/ekom/checkout/pay?culture=` | Starts payment for the current basket. For JavaScript and headless storefronts. |
| `GET`, `POST` | `/ekom/checkout/payment-return` | Where the payment provider sends the customer back after a cancelled or failed payment. |
| `POST` | `/ekom/mvcCheckout/pay` | Starts payment from an HTML form on a server-rendered page. |

### Start payment from JavaScript

`POST /ekom/checkout/pay` takes a JSON body or form data with these `PaymentRequest` fields: `paymentProvider`, `shippingProvider` (keys, optional), `cardNumber`, `cvv`, `year`, `month`, `storeAlias`, `returnUrl`, `culture` and `nonce` (`cspNonce` in JSON). Every other field is passed to the payment provider as additional data. `culture` defaults to the culture of the request. Rate policy `checkout-pay`. The route has no antiforgery check.

The response depends on the checkout result:

| Result | Response |
| --- | --- |
| Bad request | `400`. When shipping validation failed, the body says why. |
| Offline payment completed | `302` to the receipt page. Online providers answer `230` with the provider's HTML. |
| Anything else | That status code with the result as a JSON body. |

### Payment return

`GET|POST /ekom/checkout/payment-return` reads the values from the query string and the form.

1. `orderId` is required (`400` without it). `outcome` defaults to `error`. `attemptId`, when present, must be a valid payment attempt of the order (`400` otherwise).
2. A `POST` is redirected to the same route as a `GET`, so the browser sends its basket cookies.
3. For `cancel` and `error`, Ekom releases the reservations of that payment attempt and reopens the same unpaid basket. A missing or old attempt cannot release a newer attempt.
4. Ekom restores the basket cookie and redirects to the payment provider's cancel URL for `cancel`, and to its error URL for anything else. All received values are added to that URL.

`404` when the order or its payment provider is not found. This route is a browser return only. It does not prove that the provider cancelled the payment, and it does not complete an order. A payment that succeeds late is handled by reconciliation, not by completing an edited basket.

### Start payment from a form

`POST /ekom/mvcCheckout/pay` binds `PaymentRequest` from the form and checks the antiforgery token. Rate policy `checkout-pay`. It answers with provider HTML or a redirect. On failure it redirects to the return URL with these query values:

| Failure | Query values added |
| --- | --- |
| Return URL is not safe, or sent more than once | Redirects to the checkout error URL with `errorStatus=badReturnUrl` |
| Checkout is locked or in conflict | `errorStatus`, `errorMessage`, `canRetry` |
| Shipping validation failed | `errorStatus`, `errorReason`, `errorMessage` |
| Not enough stock | `errorStatus=stockError`, `errorType` (`product`, `variant` or `unknown`), `orderline` |
| Invalid data | `errorStatus=invalidData` |
| Unexpected error | `errorStatus=serverError` |

## Order-discount integration

Another system, such as a point-of-sale or ERP system, can use these routes to price a coupon and record its use without a basket. Base route: `/ekom/order-discounts`. Guide: [Discount APIs and behavior](../guides/discounts/developers.md).

Every request must send the header `X-Ekom-Api-Key` with the exact value of `Ekom:OrderDiscountCalculation:ApiKey`. While that setting is empty, every request gets `401 Unauthorized`. A request with no body gets `400` before the key is checked.

| Method | Route | Body | Response |
| --- | --- | --- | --- |
| `POST` | `/calculate` | `OrderDiscountCalculationRequest`: `couponCode`, `storeAlias`, `lines` | The calculated discount. Changes nothing. Rate policy `order-coupon`. |
| `POST` | `/update-stock` | `{ "key": "...", "value": -1, "coupon": "..." }` | Changes discount or coupon stock by `value`. `400` when `value` is `0`. |
| `POST` | `/coupon/mark-used` | `{ "coupon": "spring10" }` | Uses up one use of the coupon. `409` when the coupon is unknown or has no uses left. Rate policy `order-coupon`. |

Each line in `lines` has `clientLineId`, `sku`, optional `variantSku` or `variantKey`, `quantity` (default `1`) and optional `pricingContext`.

```http
POST /ekom/order-discounts/calculate
Content-Type: application/json
X-Ekom-Api-Key: your-configured-key

{
  "couponCode": "spring10",
  "storeAlias": "store",
  "lines": [
    { "clientLineId": "1", "sku": "SKU-1", "quantity": 2 }
  ]
}
```

## Backoffice and manager APIs

The routes under `/ekom/backoffice` and `/ekom/manager` serve Ekom's own backoffice screens. They are not meant for storefronts, and they can change between versions.

Every route needs a signed-in Umbraco backoffice user who may open the manager (see [Manager access](configuration-reference.md#manager-access)). Routes that work on one store or one order also check that the user may work with that store, and return `403` otherwise. There is no API key for these routes.

### Backoffice routes

Relative to `/ekom/backoffice`.

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/GetNonEkomDataTypes` | Data types for property editors. |
| `GET` | `/DataType/{id:guid}` | A data type by key. |
| `GET` | `/DataType/{contentTypeAlias}/propertyAlias/{propertyAlias}` | The data type of a content property. |
| `GET` | `/Metafields` | Metafield definitions. |
| `GET` | `/Languages` | All languages. |
| `GET` | `/Languages/{id}` | Languages of the stores enabled for a node. |
| `GET` | `/Stores` | All stores. |
| `GET` | `/Stores/{id}` | Stores enabled for a node. |
| `POST` | `/Cache` | Rebuilds Ekom's caches. |
| `GET` | `/Config` | Ekom's current settings. |
| `GET` | `/Stock/{id:guid}` | Sellable stock of a product or variant. |
| `GET` | `/Stock/{id:guid}/StoreAlias/{storeAlias}` | Sellable stock in one store. |
| `PATCH` | `/stock/{id:guid}/value/{stock}` | Changes stock by `stock`. |
| `PATCH` | `/stock/{id:guid}/StoreAlias/{storeAlias}/value/{stock}` | Changes one store's stock by `stock`. |
| `PUT` | `/stock/{id:guid}/value/{stock}` | Sets stock to `stock`. |
| `PUT` | `/stock/{id:guid}/StoreAlias/{storeAlias}/value/{stock}` | Sets one store's stock to `stock`. |
| `GET` | `/WarehouseStock/{id:guid}` | Warehouse stock editor data for a product or variant SKU. |
| `GET` | `/Prices/{id}/{propertyAlias}` | Database price overrides of a node. |
| `GET` | `/PriceEditor/{id}/{propertyAlias}` | Data for the price editor. |
| `PUT` | `/Prices/{id}/{propertyAlias}` | Saves a database price override. |
| `DELETE` | `/Prices/{id}/{propertyAlias}/{storeAlias}/{currency}` | Removes a database price override. |
| `POST` | `/coupon/{couponCode}/NumberAvailable/{numberAvailable}/discountId/{id:guid}` | Adds one coupon code. |
| `POST` | `/coupon/generate/discountId/{id:guid}` | Generates coupon codes from a `CouponGenerationRequest`. |
| `GET` | `/coupon/export/discountId/{id:guid}` | Downloads a discount's coupon codes as CSV. |
| `DELETE` | `/coupon/{couponCode}/discountId/{id:guid}` | Removes a coupon code. |
| `GET` | `/coupon/discountId/{id:guid}?query=&page=1&pageSize=20` | A page of a discount's coupon codes. |

### Manager routes

Relative to `/ekom/manager`. Guide: [Manager orders](../manager/orders.md).

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/AllOrders` | Orders in the stores the user may see. |
| `GET` | `/SearchOrders` | Filtered and paged order search. See below. |
| `GET` | `/OrderViewCounts` | Order counts for the saved views, with the same filters. |
| `GET` | `/OrderChangeMarker` | A marker that changes whenever an order or order review changes. Cheap to poll. |
| `GET` | `/ExportOrders` | CSV export with the same filters. `includeOrderLines` adds a row per line. |
| `GET` | `/Order/{orderId}` | The saved order data. |
| `GET` | `/OrderInfo/{orderId}` | The order as `IOrderInfo`. |
| `GET` | `/OrderDrawer/{orderId:guid}` | Everything the order details panel shows, in one request. |
| `GET` | `/OrderLogs/{orderId}` | The order's activity log. |
| `GET` | `/orderPayments/{orderId:guid}` | The order's payments: the current payment, every attempt and manual status changes. |
| `GET` | `/orderShipments/{orderId:guid}` | The order's shipments and what happened to each. |
| `POST` | `/orderStates/{orderId:guid}` | Changes the order, payment or fulfillment status. `409` when checkout holds the order. |
| `POST` | `/changeOrderStatus?orderId=&orderStatus=&notify=` | Sets the legacy order status. `notify` decides whether order events run. `409` with a `message` when checkout holds the order. |
| `GET` | `/statusMigration?afterReferenceId=` | Previews moving older orders to the three-status model. |
| `POST` | `/statusMigration?afterReferenceId=` | Moves older orders to the three-status model. |
| `GET` | `/StatusList` | The status values. |
| `POST` | `/UpdateCustomerInformation` | Changes the customer and shipping details of an order. |
| `POST` | `/Order/{orderId}/OrderLines` | Adds a line or sets its quantity, from an `OrderLineAddRequest`. |
| `DELETE` | `/Order/{orderId}/OrderLines/{lineId}` | Removes a line. |
| `POST` | `/Order/{orderId}/ShippingProvider` | Changes the order's shipping provider. |
| `POST` | `/Order/{orderId:guid}/Shipment/Tracking` | Sets the tracking number of the order's shipment. Creates a manual shipment when there is none. Tracking numbers longer than 200 characters get `400`. |
| `GET` | `/OrderActions/{orderId}` | The custom actions available for an order. |
| `POST` | `/OrderActions/{orderId}/{actionKey}` | Runs a custom action. Returns JSON or a file. |
| `GET` | `/orderLockStatus/{orderId:guid}` | Whether checkout holds the order. |
| `POST` | `/releaseOrderLock/{orderId:guid}` | Releases a stuck checkout hold. The body must contain a `releaseToken` (`400` without it). |
| `GET` | `/giftcardRecovery/{orderId:guid}` | Evidence for a failed gift card hold. |
| `POST` | `/giftcardRecovery/{orderId:guid}/release` | Releases the gift card hold. The body needs `attemptId`, `token` and `claimId`. |
| `POST` | `/giftcardRecovery/{orderId:guid}/reconcile` | Reconciles the gift card hold. The body needs `attemptId` and `token`. `409` when checkout conflicts. |
| `GET` | `/MostSoldProducts` | Best-selling products for a date range, store and status, optionally paged. |
| `GET` | `/charts` | Revenue, order count and average order value for a date range, store and status. |
| `GET` | `/stores` | Stores the user may see. |
| `GET` | `/store-currencies` | The currencies of each store, with number and date formats. |
| `GET` | `/paymentproviders/{storeAlias}` | Payment providers of a store. |
| `GET` | `/shippingproviders/{storeAlias}` | Shipping providers of a store. |

`SearchOrders` takes these query parameters: `start`, `end`, `query`, `store`, `orderStatus`, `paymentProvider`, `productSku`, `trackingSource`, `trackingMedium`, `trackingCampaign`, `trackingTerm`, `trackingContent`, `trackingClickId`, `page`, `pageSize`, `couponCode`, `shippingProvider`, `giftcardCode`, `currency` and `needsReview`. `ExportOrders` and `OrderViewCounts` take the same filters.

- `couponCode` matches the coupon saved on the order, trimmed and without regard to case. Coupons on single lines are not searched.
- `shippingProvider` is the provider's key, not its title.
- An empty value adds no filter.
- The search, its counts and totals, and the export use the same filters. `charts` and `MostSoldProducts` filter by date, store and status only.

For the failed gift card recovery routes, see [Failed gift-card preparation recovery](../guides/giftcard-recovery.md).

### Customer routes

Relative to `/ekom/manager/customers`. Guide: [Customers](../manager/customers.md).

| Method | Route | Parameters | Purpose |
| --- | --- | --- | --- |
| `GET` | `/` | `store`, `start`, `end`, `orderStatus` (default `CompletedOrders`), `query`, `segment` (default `all`), `sort` (default `newest`), `page` (default `1`), `pageSize` (default `20`), `currency` | A page of customers. |
| `GET` | `/detail` | `store`, `identity`, `page`, `other`, `currency`, `orderStatus` | One customer and their orders. |
| `GET` | `/export` | Same filters as the list, without paging. | CSV export. |

### Order review routes

Relative to `/ekom/manager/orders/{orderId:guid}/reviews`. Guide: [Order reviews](../guides/order-reviews.md).

| Method | Route | Body or query | Purpose |
| --- | --- | --- | --- |
| `GET` | `/` | `includeResolved` (default `false`), `skip` (default `0`), `take` (default `50`, at most `200`) | The order's review issues. |
| `POST` | `/` | `{ "source", "code", "title", "message" }` | Raises a review issue. |
| `POST` | `/{reviewId:guid}/review` or `/{reviewId:guid}/resolve` | `{ "resolved": true, "comment": "..." }` | Records a review, and resolves the issue when `resolved` is `true` (the default). `409` when the issue cannot change. |

These routes return `403` when the user may not open the manager or the order's store, and `404` when the order is not found.

## Related pages

- [Headless storefronts](../guides/headless.md): how a storefront uses these routes, step by step.
- [Checkout](../guides/checkout.md): what happens during payment and completion.
- [.NET API](dotnet-api.md): the C# methods behind the routes.
- [Configuration reference](configuration-reference.md): the settings for body size, search fields and the integration key.
- [Models and behavior](models-and-behavior.md): the objects the routes return.

[Back to Reference](README.md)
