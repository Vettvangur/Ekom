# HTTP API Reference

These routes are ASP.NET Core controller endpoints. They are separate from the in-process C# facades documented in [.NET API](dotnet-api.md). Route casing is shown as declared; ASP.NET Core matching is normally case-insensitive.

## Security and request context

The catalog, general API, provider, order, and `/ekom/checkout` controllers have no authorization attribute in Ekom source. Ekom also does not require a common custom header on those controllers. Protect or restrict them at the host/proxy when the data or operation should not be anonymous.

Cart endpoints identify the active cart through Ekom's order cookie and store context. A `storeAlias` is a route/body/query value only where listed; there is no verified general `storeAlias` header contract.

Exceptions handled by `ApiExceptionFilter` may return problem responses in addition to the explicit statuses below. Rate-limited routes return `429 Too Many Requests`.

## General data

| Method | Route | Response |
| --- | --- | --- |
| `GET` | `/ekom/api/countries` | All `Country` records. |
| `GET` | `/ekom/api/stores` | All configured `IStore` records. |

No authentication attribute or required custom header is declared for these routes.

## Catalog

Base route: `/ekom/catalog`. Single lookups return `404` when absent. Collection routes return `ProductResponse` or a collection. Routes declared for both `GET` and `POST` bind complex values from the body; use JSON `POST` for reliable `ProductQuery`/array binding.

| Verbs | Route | Input |
| --- | --- | --- |
| `GET` | `/product/{id:guid}` | Product key. |
| `GET` | `/product/{id:int}` | Umbraco integer ID. |
| `GET` | `/product/sku/{sku}` | SKU. |
| `GET` | `/product/route?route=...` | Route query value. |
| `GET`, `POST` | `/productsrecursive/route?route=...` | Optional `ProductQuery` body. |
| `GET`, `POST` | `/productsrecursive/{categoryId:int}` | Optional `ProductQuery` body. |
| `GET`, `POST` | `/productsrecursive/{categoryKey:guid}` | Optional `ProductQuery` body. |
| `GET`, `POST` | `/products/{categoryId:int}` | Direct products; optional query body. |
| `GET`, `POST` | `/products/{categoryKey:guid}` | Direct products; optional query body. |
| `GET`, `POST` | `/productsbyids` | Required `ProductQuery` body with `Ids`; null body is `400`. |
| `GET`, `POST` | `/productsbykeys` | Required body with `Keys`; null body is `400`. |
| `GET`, `POST` | `/productsbyskus` | Required body with `Skus`; null body is `400`. |
| `GET`, `POST` | `/allproducts` | Optional `ProductQuery` body. |
| `GET`, `POST` | `/category/{id:int}` or `/category/{id:guid}` | Category identifier. |
| `GET`, `POST` | `/category/route?route=...` | Empty route is `400`. |
| `GET`, `POST` | `/categoriesbykeys` | `Guid[]` body; empty/null is `400`. |
| `GET`, `POST` | `/categoriesbyids` | `int[]` body. |
| `GET`, `POST` | `/rootcategories`, `/allcategories` | No body required. |
| `GET`, `POST` | `/subcategories/{id:int|guid}` | Direct children. |
| `GET`, `POST` | `/subcategoriesrecursive/{id:int|guid}` | All descendants. |
| `GET`, `POST` | `/categoryfilters/{id:int|guid}` | Filter groups. |
| `GET`, `POST` | `/relatedproducts/{id:guid}/{count:int}` | Related products. |
| `GET`, `POST` | `/relatedproducts?ids={id}&ids={id}&count=4` | Repeated `ids` query values. |
| `GET`, `POST` | `/relatedproductsbysku/{sku}/{count:int}` | One SKU. |
| `GET`, `POST` | `/relatedproductsbyskus?skus=A&skus=B&count=4` | Repeated `skus`. |
| `GET`, `POST` | `/productsearch` | Required `SearchRequest` body. |
| `GET`, `POST` | `/metafields` | All metafields. |

Example:

```http
POST /ekom/catalog/allproducts
Content-Type: application/json

{
  "storeAlias": "store",
  "page": 1,
  "pageSize": 24,
  "orderBy": "DateDesc",
  "propertyFilters": {},
  "metaFilters": {}
}
```

## Providers

Base route: `/ekom/provider`.

| Method | Route | Behavior |
| --- | --- | --- |
| `GET` | `/paymentsproviders/{storeAlias?}?countryCode=IS&orderAmount=100` | Filtered payment providers; missing store is `404`. |
| `GET` | `/paymentsprovider/{id:guid}` | Provider in current store. A missing provider may serialize as `null`; only a missing store explicitly returns `404`. |
| `GET` | `/shippingproviders/{storeAlias?}?countryCode=IS` | Uses the current cart amount excluding its shipping price for range filtering. |
| `GET` | `/shippingprovider/{id:guid}` | Provider in current store. |
| `GET` | `/zones` | All zones. |

The plural payment route is spelled `paymentsproviders`; the singular route is `paymentsprovider`.

## Orders and carts

Base route: `/ekom/order`. These routes have no authorization attribute. In particular, `GET /ekom/order/{orderId}` reads by GUID regardless of order status; expose it only under an appropriate host-level access policy if order identifiers must be confidential.

### Reads

| Method | Route | Result |
| --- | --- | --- |
| `GET` | `/` or `/storeAlias/{storeAlias?}` | Current cart; `404` if none. |
| `GET` | `/{orderId:guid}` | Any order by key; `404` if absent. |
| `GET` | `/relatedproducts/{count:int}` | Related products for current cart. |
| `GET` | `/relatedproducts/storeAlias/{storeAlias?}/{count:int}` | Same for selected store. |

### Mutations

| Method | Route | Body/query |
| --- | --- | --- |
| `POST` | `/add` | JSON or form `OrderRequest`; 64 KB controller limit; rate policy `order-add` (30/minute per remote IP). |
| `POST` | `/giftcards` | `GiftcardRequest`. |
| `DELETE` | `/giftcards/{code}?storeAlias=...` | Gift card code and required query alias. |
| `POST` | `/updatecustomer` | JSON object or form fields; `consent` and `tracking` are modeled separately. |
| `POST` | `/updatetracking` | JSON `OrderTrackingUpdateRequest`. |
| `POST` | `/update/shippingprovider/` or `/updateshippingprovider` | JSON/form/query provider ID and store alias. |
| `POST` | `/update/paymentprovider/` or `/updatepaymentprovider` | JSON/form/query provider ID and store alias. |
| `POST` | `/removeorderline` | JSON `OrderlineRequest` (`lineId`, `storeAlias`). |
| `POST` | `/Updateorderlinequantity` | JSON `OrderlineRequest`; quantity is non-negative integer in this HTTP model. |
| `POST` | `/currency?currency=en-US` | Changes cart currency using an exact configured `CurrencyValue` and writes the `EkomCurrency-{StoreAlias}` cookie. |
| `POST` | `/coupon/apply` | JSON `CouponRequest`; returns `450` if a better discount prevents modification; rate limited. |
| `POST` | `/coupon/remove?storeAlias=...` | Removes current order coupon; rate limited. |

`POST /add` accepts `productId`, optional `variantId`, `storeAlias`, decimal `quantity`, `action`, consent/tracking, and `linkedProducts`. Unknown top-level JSON/form values become HTML-encoded line custom data except reserved tracking fields. Linked child custom data is also encoded. See [Linked order lines](../guides/linked-order-lines.md).

## Checkout

### API-style payment

`POST /ekom/checkout/pay?culture=...` accepts either JSON or form data matching `PaymentRequest`: nullable payment/shipping provider GUIDs, card fields, expiry, store alias, return URL, culture, nonce, and provider-specific additional fields. No authorization or antiforgery attribute is declared on this route.

The response follows `CheckoutResponse.HttpStatusCode`: `400` becomes a bad request, `300` redirects to the response URL, and other values are written as the HTTP status with a JSON body.

### Payment callback

`GET|POST /ekom/checkout/payment-return` merges query and form values. `orderId` is required; `outcome` defaults to `error`. Ekom loads the order/payment provider, restores the order cookie, and redirects to that provider's configured cancel URL for `cancel`, otherwise its error URL. This is a callback/redirect endpoint, not a general successful-payment completion endpoint.

### MVC form payment

`POST /ekom/mvcCheckout/pay` model-binds `PaymentRequest` from a form and has `[ValidateAntiForgeryToken]`. It returns provider HTML/redirects and maps errors into return-URL query values. This is the only checkout route with controller-level antiforgery verification in current source.

## Order-discount integration

Base route: `/ekom/order-discounts`. All three routes require an exact `X-Ekom-Api-Key` header matching `Ekom:OrderDiscountCalculation:ApiKey`; no configured key means all requests receive `401 Unauthorized`.

| Method | Route | Body | Result |
| --- | --- | --- | --- |
| `POST` | `/calculate` | `OrderDiscountCalculationRequest` with coupon, store, and SKU lines | Non-mutating calculation result; coupon rate policy applies. |
| `POST` | `/update-stock` | `{ "key": "...", "value": -1, "coupon": "..." }` | Updates discount/coupon stock; zero is `400`. |
| `POST` | `/coupon/mark-used` | `{ "coupon": "code" }` | Marks the coupon used; coupon rate policy applies. |

```http
POST /ekom/order-discounts/calculate
Content-Type: application/json
X-Ekom-Api-Key: configured-secret

{
  "couponCode": "spring10",
  "storeAlias": "store",
  "lines": [
    { "clientLineId": "1", "sku": "SKU-1", "quantity": 2 }
  ]
}
```

## Backoffice and manager APIs

Routes under `/ekom/backoffice` and `/ekom/manager` are implementation APIs for Ekom's Umbraco UI, not storefront APIs. Their actions are decorated with `UmbracoUserAuthorize` and manager operations additionally enforce configured store permissions. Clients must use an authenticated Umbraco backoffice session; Ekom does not define an API-key header for these routes.

### Backoffice routes

All routes below are relative to `/ekom/backoffice`.

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/GetNonEkomDataTypes` | Property-editor data types. |
| `GET` | `/DataType/{id:guid}` | Data type by key. |
| `GET` | `/DataType/{contentTypeAlias}/propertyAlias/{propertyAlias}` | Data type for a content property. |
| `GET` | `/Metafields` | Metafield definitions. |
| `GET` | `/Languages` | All languages. |
| `GET` | `/Languages/{id}` | Languages supported by stores enabled for a node. |
| `GET` | `/Stores` | All stores, briefly cached for the editor. |
| `GET` | `/Stores/{id}` | Stores enabled for a node. |
| `POST` | `/Cache` | Refreshes Ekom caches. |
| `GET` | `/Config` | Current `Configuration` projection. |
| `GET` | `/Stock/{id:guid}` | Global/current-context primary stock. |
| `GET` | `/Stock/{id:guid}/StoreAlias/{storeAlias}` | Store primary stock. |
| `GET` | `/WarehouseStock/{id:guid}` | Warehouse editor data for a product/variant node's SKU. |
| `PATCH` | `/stock/{id:guid}/value/{stock}` | Increment global/current-context stock. |
| `PATCH` | `/stock/{id:guid}/StoreAlias/{storeAlias}/value/{stock}` | Increment store stock. |
| `PUT` | `/stock/{id:guid}/value/{stock}` | Set global/current-context stock. |
| `PUT` | `/stock/{id:guid}/StoreAlias/{storeAlias}/value/{stock}` | Set store stock. |
| `POST` | `/coupon/{couponCode}/NumberAvailable/{numberAvailable}/discountId/{id:guid}` | Insert one coupon. |
| `POST` | `/coupon/generate/discountId/{id:guid}` | Generate coupons from `CouponGenerationRequest`. |
| `GET` | `/coupon/export/discountId/{id:guid}` | Download coupon CSV. |
| `DELETE` | `/coupon/{couponCode}/discountId/{id:guid}` | Remove a coupon. |
| `GET` | `/coupon/discountId/{id:guid}?query=&page=1&pageSize=20` | Paged coupons for a discount. |

### Manager routes

All routes below are relative to `/ekom/manager`. Routes taking `store`, or loading an order first, enforce `IManagerAccessService` store access in addition to backoffice authentication.

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/AllOrders` | Orders restricted to allowed stores. |
| `GET` | `/Order/{orderId}` | Persisted order data. |
| `GET` | `/OrderInfo/{orderId}` | Hydrated `IOrderInfo`. |
| `GET` | `/OrderLogs/{orderId}` | Activity log entries. |
| `GET` | `/OrderActions/{orderId}` | Available registered manager actions. |
| `POST` | `/OrderActions/{orderId}/{actionKey}` | Execute an action; may return JSON or a file. |
| `GET` | `/SearchOrders` | Filtered/paged manager search; filters are query parameters. |
| `GET` | `/ExportOrders` | CSV export; `includeOrderLines` controls line rows. |
| `GET` | `/MostSoldProducts` | Aggregates for date/store/status, optionally paged. |
| `GET` | `/StatusList` | Available status values. |
| `GET` | `/stores` | Stores available to the current manager. |
| `POST` | `/changeOrderStatus?orderId=...&orderStatus=...&notify=...` | Changes status; `notify` controls order events. |
| `POST` | `/UpdateCustomerInformation` | Updates customer/shipping fields for an order. |
| `POST` | `/Order/{orderId}/OrderLines` | Adds/sets a line from `OrderLineAddRequest`. |
| `DELETE` | `/Order/{orderId}/OrderLines/{lineId}` | Removes an order line. |
| `GET` | `/charts` | Revenue/order/average aggregates for date/store/status. |

## Related reference

- [.NET API](dotnet-api.md)
- [Configuration](configuration-reference.md)
- [Models and behavior](models-and-behavior.md)
