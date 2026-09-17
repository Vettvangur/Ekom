# Headless storefronts

Ekom's public controllers support catalog browsing, cookie-backed baskets, provider selection and checkout over HTTP. They are application endpoints, not a separately versioned API product; pin the Ekom package version and test request/response models during upgrades.

## Store and basket context

Store context varies by route and can come from a route segment, request body, or Ekom's current request resolution. Prefer an explicit `storeAlias` in every headless flow.

The current basket is identified by Ekom's order cookie. Browser clients must keep cookies across requests:

```js
const response = await fetch('/ekom/order/storeAlias/Store', {
  credentials: 'include'
});
```

For cross-origin frontends, configure ASP.NET Core CORS and cookie policies in the host application. Ekom does not make cross-origin credentials safe automatically. Protect customer/order lookup endpoints according to the site's authorization requirements; possession of an order ID must not be treated as proof of ownership by custom code.

## Typical flow

1. `GET /ekom/api/stores` and `GET /ekom/api/countries` for initial context.
2. Browse `/ekom/catalog` routes.
3. `POST /ekom/order/add` and retain the basket cookie.
4. `POST /ekom/order/updatecustomer`.
5. List `/ekom/provider/shippingproviders/{storeAlias}` and select one.
6. List `/ekom/provider/paymentsproviders/{storeAlias}` and select one.
7. `POST /ekom/checkout/pay`.
8. Let the trusted payment integration complete the order; render the receipt from the returned/callback order ID.

## Catalog endpoints

Base route: `/ekom/catalog`.

| Method and path | Request/result |
| --- | --- |
| `GET /product/{guid|int}` | One product. |
| `GET /product/sku/{sku}` | One product by SKU. |
| `GET /product/route?route=...` | One product by route. |
| `GET|POST /category/{guid|int}` | One category. |
| `GET|POST /category/route?route=...` | One category by route. |
| `GET|POST /allproducts` | Optional `ProductQuery` body. |
| `GET|POST /products/{guid|int}` | Direct category products. |
| `GET|POST /productsrecursive/{guid|int}` | Recursive category products. |
| `GET|POST /productsrecursive/route?route=...` | Recursive products by category route. |
| `GET|POST /productsbyids` | `ProductQuery.Ids`. |
| `GET|POST /productsbykeys` | `ProductQuery.Keys`. |
| `GET|POST /productsbyskus` | `ProductQuery.Skus`. |
| `GET|POST /productsearch` | `SearchRequest` body. |
| `GET|POST /rootcategories` | Root categories. |
| `GET|POST /allcategories` | All categories. |
| `GET|POST /categoriesbyids` | JSON integer array. |
| `GET|POST /categoriesbykeys` | JSON GUID array. |
| `GET|POST /subcategories/{guid|int}` | Direct children. |
| `GET|POST /subcategoriesrecursive/{guid|int}` | All descendants. |
| `GET|POST /categoryfilters/{guid|int}` | Filter metadata. |
| `GET|POST /relatedproducts/{guid}/{count}` | Related products. |
| `GET|POST /relatedproducts?ids=...&count=...` | Related products from keys. |
| `GET|POST /relatedproductsbysku/{sku}/{count}` | Related by SKU. |
| `GET|POST /relatedproductsbyskus?skus=...&count=...` | Related from SKUs. |
| `GET|POST /metafields` | Metafields. |

For routes with a `[FromBody]` model, use `POST application/json`; GET bodies are not portable across clients/proxies. See [Catalog and products](catalog-and-products.md) for model behavior.

## Order endpoints

Base route: `/ekom/order`.

| Method and path | Purpose |
| --- | --- |
| `GET /` | Current order in resolved store. |
| `GET /storeAlias/{storeAlias}` | Current order for a store. |
| `GET /{orderId}` | Persisted order by GUID. |
| `POST /add` | Add/update a line or add a linked group. |
| `POST /removeorderline` | Remove a line. |
| `POST /Updateorderlinequantity` | Set line quantity; route casing shown as declared. |
| `POST /updatecustomer` | Update customer/shipping values and optional consent/tracking. |
| `POST /updatetracking` | Replace tracking/consent before final status. |
| `POST /updateshippingprovider` | Select shipping provider. |
| `POST /updatepaymentprovider` | Select payment provider. |
| `POST /currency?currency=en-US` | Change the order currency using an exact configured `CurrencyValue`. |
| `POST /coupon/apply` | Apply coupon. |
| `POST /coupon/remove?storeAlias=Store` | Remove coupon. |
| `POST /giftcards` | Add a gift card model. |
| `DELETE /giftcards/{code}?storeAlias=Store` | Remove gift card. |
| `GET /relatedproducts/{count}` | Related products from current basket. |

Add a product:

```http
POST /ekom/order/add
Content-Type: application/json

{
  "productId": "cb6906db-c156-4c88-814c-6cc181bd1ae3",
  "variantId": "5abc5a27-cf22-4438-bcb6-f12c2a8564c9",
  "storeAlias": "Store",
  "quantity": 1,
  "action": "AddOrUpdate"
}
```

`action` supports the current `OrderAction` string values, including `AddOrUpdate`, `Set`, and `New`. The endpoint accepts JSON, URL-encoded form, or multipart form, rejects bodies over 64,000 bytes, and applies the `order-add` rate-limit policy. JSON must contain exactly one object. Custom root fields are HTML-encoded and passed as custom data; line properties are limited by the order service's `orderline*` convention.

Update a line:

```http
POST /ekom/order/Updateorderlinequantity
Content-Type: application/json

{
  "lineId": "00000000-0000-0000-0000-000000000030",
  "quantity": 3,
  "storeAlias": "Store"
}
```

The request model currently binds quantity as an integer. Product add quantities are decimal.

Update customer information with JSON or form data. The built-in checkout expects at least `customerName` and `customerEmail`; common fields also include `customerAddress`, `customerApartment`, `customerCity`, `customerCountry`, `customerRegion`, `customerZipCode`, `customerPhone`, and corresponding `shipping*` values.

## Providers

Base route: `/ekom/provider`.

| Method and path | Purpose |
| --- | --- |
| `GET /paymentsproviders/{storeAlias?}?countryCode=IS&orderAmount=5000` | Payment methods filtered by zone/range. |
| `GET /paymentsprovider/{guid}` | One payment method in current store. |
| `GET /shippingproviders/{storeAlias?}?countryCode=IS` | Shipping methods, using current basket amount. |
| `GET /shippingprovider/{guid}` | One shipping method in current store. |
| `GET /zones` | Zones. |

Provider selection endpoints accept JSON/form values named `ShippingProvider` or `PaymentProvider` plus `storeAlias`. Selection recalculates and returns the order; it does not submit payment.

## Checkout

```http
POST /ekom/checkout/pay?culture=en-US
Content-Type: application/json

{
  "paymentProvider": "00000000-0000-0000-0000-000000000020",
  "shippingProvider": "00000000-0000-0000-0000-000000000010",
  "storeAlias": "Store",
  "culture": "en-US",
  "returnUrl": "/receipt"
}
```

The endpoint may return a JSON-serialized provider HTML string (`230`), a redirect response (`300` is converted to an HTTP redirect), invalid data (`400`), or stock failure (`530`). A headless client must be prepared for the selected provider's handoff model; for `230`, decode the JSON string before writing the provider markup to a document. Do not mark an order paid from browser state; verify provider callbacks server-side. See [Checkout](checkout.md).

`GET|POST /ekom/checkout/payment-return` is the built-in error/cancel return path and requires `orderId`; its `outcome` defaults to `error`. Successful provider completion is integration-specific.

## Discount calculation integration

`POST /ekom/order-discounts/calculate` quotes one coupon against supplied SKU lines without creating a basket. It requires `Ekom:OrderDiscountCalculation:ApiKey` and matching `X-Ekom-Api-Key`, and is rate limited. This secret belongs on a trusted backend, never in public browser JavaScript. See [Discounts](discounts.md).

## Errors, serialization and security

- Single catalog/order lookups generally return `404`; invalid request models return `400` or the API exception filter's problem response.
- Product/order responses are rich Ekom models. Generate client types from observed pinned-version payloads rather than assuming every C# property serializes.
- Keep `credentials: include` (or an equivalent cookie jar) for the complete basket flow.
- Rate limiting exists on add and coupon routes, but it is not authentication.
- Validate store, order, return URL, provider-specific fields, consent and customer identity at the site's trust boundary.
- Keep payment and order-discount API secrets server-side.
- The repository's `Bruno/` collection contains runnable examples, but controller source is authoritative when examples diverge.
