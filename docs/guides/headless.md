[Ekom docs](../README.md) / [Guides](README.md) / Headless storefronts

# Headless storefronts

Ekom has HTTP endpoints for the catalog, the basket, payment and shipping choices, and checkout. Use them when your storefront is a separate front end, for example a JavaScript app, that talks to Ekom over HTTP instead of rendering Umbraco views.

These endpoints are part of the Ekom package. They have no version of their own: when you upgrade Ekom, test the requests and responses your front end uses. The controllers under `Ekom/Ekom.Core/Ekom/Controllers` are the source of truth for every route on this page.

On this page:

- [Store and basket context](#store-and-basket-context)
- [Typical checkout flow](#typical-checkout-flow)
- [Catalog endpoints](#catalog-endpoints)
- [Basket and order endpoints](#basket-and-order-endpoints)
- [Payment and shipping provider endpoints](#payment-and-shipping-provider-endpoints)
- [Checkout endpoints](#checkout-endpoints)
- [Coupons and discount quotes](#coupons-and-discount-quotes)
- [Errors, limits and security](#errors-limits-and-security)

## Store and basket context

Every basket belongs to one store. Some endpoints take the store alias in the route, the query string or the body. The others use the store of the current request, as Ekom resolves it. Send `storeAlias` everywhere an endpoint accepts it, so the result does not depend on how the request was resolved. See [Stores](stores.md).

Ekom finds the current basket through a cookie, one per store. When a store keeps baskets per member (`UserBasket`) and the customer is logged in, Ekom uses the member's basket instead. Either way, your client must send cookies with every request:

```js
const response = await fetch('/ekom/order/storeAlias/Store', {
  credentials: 'include'
});
```

If the front end runs on another origin, set up CORS and the cookie policy in your host application. Ekom does not do this for you, and it does not make cross-origin cookies safe on its own.

`GET /ekom/order/{orderId}` returns any order by its ID. Ekom does not check that the caller owns that order. Do not treat knowing an order ID as proof of ownership in your own code.

## Typical checkout flow

1. Load the context: `GET /ekom/api/stores` and `GET /ekom/api/countries`.
2. Show products with the `/ekom/catalog` endpoints.
3. Add products with `POST /ekom/order/add`. Keep the basket cookie it sets.
4. Save the customer's details with `POST /ekom/order/updatecustomer`. At least `customerName` and `customerEmail` are needed before checkout.
5. List shipping providers with `GET /ekom/provider/shippingproviders/{storeAlias}`, then save the choice with `POST /ekom/order/updateshippingprovider`.
6. List payment providers with `GET /ekom/provider/paymentsproviders/{storeAlias}`, then save the choice with `POST /ekom/order/updatepaymentprovider`.
7. Start payment with `POST /ekom/checkout/pay`.
8. Let the payment provider's server-side callback complete the order. Show the receipt for the order ID that comes back from the provider.

## Catalog endpoints

Base route: `/ekom/catalog`. In the table, `{id}` is a numeric ID or a GUID key; both forms exist for each of these routes.

| Method and path | Returns |
| --- | --- |
| `GET product/{id}` | One product. |
| `GET product/sku/{sku}` | One product, by SKU. |
| `GET product/route?route=...` | One product, by its URL route. |
| `GET` or `POST category/{id}` | One category. |
| `GET` or `POST category/route?route=...` | One category, by its URL route. |
| `GET` or `POST allproducts` | All products. Optional `ProductQuery` body. |
| `GET` or `POST products/{id}` | Products directly in a category. Optional `ProductQuery` body. |
| `GET` or `POST productsrecursive/{id}` | Products in a category and all its subcategories. Optional `ProductQuery` body. |
| `GET` or `POST productsrecursive/route?route=...` | The same, by the category's URL route. Optional `ProductQuery` body. |
| `GET` or `POST productsbyids` | Products by numeric ID, from `ProductQuery.Ids`. |
| `GET` or `POST productsbykeys` | Products by key, from `ProductQuery.Keys`. |
| `GET` or `POST productsbyskus` | Products by SKU, from `ProductQuery.Skus`. |
| `GET` or `POST productsearch` | Search results. `SearchRequest` body. |
| `GET` or `POST rootcategories` | The top-level categories. |
| `GET` or `POST allcategories` | All categories. |
| `GET` or `POST categoriesbyids` | Categories. Body: a JSON array of numeric IDs. |
| `GET` or `POST categoriesbykeys` | Categories. Body: a JSON array of GUID keys. |
| `GET` or `POST subcategories/{id}` | The direct subcategories. |
| `GET` or `POST subcategoriesrecursive/{id}` | All subcategories, at every level. |
| `GET` or `POST categoryfilters/{id}` | Filter data for a category. |
| `GET` or `POST relatedproducts/{key}/{count}` | Related products for one product key. |
| `GET` or `POST relatedproducts?ids={key}&ids={key}&count=4` | Related products for several product keys. `count` defaults to `4`. |
| `GET` or `POST relatedproductsbysku/{sku}/{count}` | Related products for one SKU. |
| `GET` or `POST relatedproductsbyskus?skus={sku}&skus={sku}&count=4` | Related products for several SKUs. `count` defaults to `4`. |
| `GET` or `POST metafields` | All metafields. |

The single product and category lookups return `404` when nothing is found. `productsrecursive/route`, `productsbyids`, `productsbykeys` and `productsbyskus` use `ProductQuery.StoreAlias` to set the store.

For a route that reads a body, send `POST` with `Content-Type: application/json`. Many clients and proxies drop a body on `GET`. See [Catalog and products](catalog-and-products.md) for what the models contain.

## Basket and order endpoints

Base route: `/ekom/order`.

| Method and path | What it does |
| --- | --- |
| `GET /ekom/order` | The current basket in the store of the request. |
| `GET storeAlias/{storeAlias}` | The current basket in a store. |
| `GET {orderId}` | A saved order, by its GUID. |
| `POST add` | Add a product, change a line, or add a group of linked lines. |
| `POST removeorderline` | Remove a line. Body: `lineId`, `storeAlias`. |
| `POST Updateorderlinequantity` | Set a line's quantity. Body: `lineId`, `quantity`, `storeAlias`. |
| `POST updatecustomer` | Save customer and shipping details, and optionally consent and tracking. |
| `POST updatetracking` | Replace the tracking and consent on the basket. See [Tracking and consent](tracking-and-consent.md#save-tracking-on-an-order). |
| `POST updateshippingprovider` (also `update/shippingprovider`) | Choose a shipping provider. |
| `POST updatepaymentprovider` (also `update/paymentprovider`) | Choose a payment provider. |
| `POST currency?currency=en-US` | Change the currency of the current store's basket. |
| `POST coupon/apply` | Apply a coupon code. |
| `POST coupon/remove?storeAlias=Store` | Remove the coupon code. |
| `POST giftcards` | Add a gift card by code. |
| `DELETE giftcards/{code}?storeAlias=Store` | Remove a gift card. |
| `GET relatedproducts/{count}` | Related products for the current basket. |
| `GET relatedproducts/storeAlias/{storeAlias}/{count}` | The same, for a given store. |

The basket reads return `404` when there is no basket. Most changing endpoints return the updated basket. The coupon endpoints return an empty body.

### Add a product

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

| Field | Required | Notes |
| --- | --- | --- |
| `productId` | Yes | The product key. |
| `variantId` | No | The variant key, for products with variants. |
| `storeAlias` | No | The store of the basket. Without it, Ekom uses the store of the request. Send it anyway. |
| `quantity` | No | Default `1`. A decimal number. |
| `action` | No | `AddOrUpdate` (default) adds to an existing line, and a negative quantity lowers it. `Set` sets the line to this quantity. `New` always adds a new line, even for the same product. |
| `linkedProducts` | No | Add-ons that belong to this line. See [Linked order lines](linked-order-lines.md). |
| `consent`, `tracking` | No | JSON only. See [Tracking and consent](tracking-and-consent.md#save-tracking-on-an-order). |

How the endpoint reads the request:

- It accepts JSON, URL-encoded forms and multipart forms. Other content types return `415`.
- A body larger than 64,000 bytes returns `400`.
- A JSON body must be one object. Anything else returns `400`.
- Every other field in the body is HTML-encoded and saved on the basket as custom data. Fields whose name starts with `orderline` are saved on the order line instead. See [Order custom data](order-custom-data.md).

### Change a line's quantity

```http
POST /ekom/order/Updateorderlinequantity
Content-Type: application/json

{
  "lineId": "00000000-0000-0000-0000-000000000030",
  "quantity": 3,
  "storeAlias": "Store"
}
```

The route is written as shown, with a capital U. ASP.NET Core matches routes without regard to case. `quantity` here is a whole number of 0 or more, while `add` takes decimals. A missing `storeAlias` returns `400` with "StoreAlias is missing".

### Save customer details

`updatecustomer` takes JSON or a form. Other content types return `400`. Checkout needs at least `customerName` and `customerEmail`. Other fields Ekom knows include `customerAddress`, `customerApartment`, `customerCity`, `customerCountry`, `customerRegion`, `customerZipCode`, `customerPhone`, and the matching `shipping...` fields such as `shippingAddress` and `shippingCountry`. In JSON, `consent` and `tracking` objects are read as consent and tracking, not as customer fields.

### Change the currency

`POST /ekom/order/currency?currency=en-US` uses the store of the current request; it takes no store alias. The value must match a configured `CurrencyValue` of that store exactly. Ekom then changes the basket's currency and sets the `EkomCurrency-{storeAlias}` cookie for 360 days. If the value does not match, the basket keeps its currency, but the cookie is still set.

### Gift cards

`POST /ekom/order/giftcards` takes `{ "giftcard": { "code": "GIFT-1234" }, "storeAlias": "Store" }`. Only the code is read. The amount comes from your `IGiftcardValidator`.

| Status | Meaning |
| --- | --- |
| `200` | Added. Returns the basket. |
| `400` | The code is empty or longer than 200 characters, `storeAlias` is empty, or the validator did not accept the code. |
| `403` | No `IGiftcardValidator` is registered ("Gift cards are not enabled."). |
| `404` | There is no basket. |

## Payment and shipping provider endpoints

Base route: `/ekom/provider`.

| Method and path | Returns |
| --- | --- |
| `GET paymentsproviders/{storeAlias?}?countryCode=IS&orderAmount=5000` | The payment providers that allow this country and amount. |
| `GET paymentsprovider/{key}` | One payment provider, in the store of the request. |
| `GET shippingproviders/{storeAlias?}?countryCode=IS` | The shipping providers that allow this country and the basket amount. |
| `GET shippingprovider/{key}` | One shipping provider, in the store of the request. |
| `GET zones` | All zones. |

For shipping providers, Ekom works out the amount itself: the current basket total, minus the price of the shipping provider already chosen. An unknown store returns `404`.

To choose a provider, send `ShippingProvider` or `PaymentProvider` (the provider key) and `storeAlias` to `updateshippingprovider` or `updatepaymentprovider`, as JSON, as a form, or in the query string. If either value is missing, the endpoint returns `400`. Choosing a provider recalculates the basket and returns it. It does not start payment.

See [Payment and shipping providers](providers.md).

## Checkout endpoints

### Start payment

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

The endpoint also accepts a form with the fields `PaymentProvider`, `ShippingProvider`, `StoreAlias`, `ReturnUrl` and `Culture`. Other fields in the body are passed to the payment provider as additional data.

The response depends on the payment provider and on what happened:

| Status | Meaning | What your client does |
| --- | --- | --- |
| `230` | The body is a JSON string that holds the provider's HTML, for example a form that posts to the provider. | Parse the JSON string, then write the HTML into a document. |
| `302` | A redirect to the provider's payment page. | Follow it. |
| `400` | The data is not valid, for example the customer has no name or email. If the chosen shipping provider no longer fits the basket, the body is a `ShippingValidationError` with the code `invalidShippingProvider`, a reason and a message. | Show the problem and let the customer fix it. |
| `409` | The basket cannot go to payment right now. The body has `code`, `message` and `canRetry`. | Show the message. Retry only when `canRetry` is `true`. |
| `530` | There is not enough stock. The body is a `StockError` with `OrderLineKey` (the line that is short) and `IsVariant`. | Show which line is short. |
| `429` | Too many requests from this IP address. | Wait and try again. |
| `500` | Any other error, including a JSON body that has the same property twice. | Log it and show a general error. |

Do not mark an order as paid because of something the browser reports. Only the payment provider's server-side callback confirms a payment. See [Checkout](checkout.md) and [Payments](payments.md).

`/ekom/mvcCheckout/pay` is the form-post version for Razor views. It needs an anti-forgery token and answers with redirects, so it is not meant for headless clients.

### Return from a cancelled or failed payment

`GET` or `POST /ekom/checkout/payment-return` is where a provider sends the customer back after a cancel or an error.

- `orderId` is required. Without it, the endpoint returns `400`.
- `outcome` defaults to `error`. With `outcome=cancel`, Ekom redirects to the provider's cancel URL. With any other value, it redirects to the provider's error URL.
- A `POST` is first redirected to a `GET` of the same route with the same values.
- Ekom restores the basket cookie, so the customer can try again.

A successful payment does not use this route. How a successful payment returns depends on the payment provider.

## Coupons and discount quotes

`POST /ekom/order/coupon/apply` takes `{ "coupon": "SPRING10", "storeAlias": "Store" }`. A `200` has an empty body: read the basket again to show the new totals. A `450` means the code was valid but did not change the basket. See [Apply and remove coupons](discounts/developers.md#apply-and-remove-coupons) for every status code.

`POST /ekom/order-discounts/calculate` works out what a coupon would do to a list of SKUs, without a basket. It needs a secret in `Ekom:OrderDiscountCalculation:ApiKey` and the same value in the `X-Ekom-Api-Key` header. Call it only from your own server, never from browser code. See [Quote a coupon without creating an order](discounts/developers.md#quote-a-coupon-without-creating-an-order).

## Errors, limits and security

**Errors.** Lookups that find nothing return `404`. Bad input returns `400`. Errors Ekom does not map to a status code return `500` with the text "An unexpected error occurred".

**Responses.** Product and order responses are Ekom's own models and are large. Build your client types from the responses of the Ekom version you run, rather than from the C# classes.

**Body size.** Requests to `/ekom/order`, `/ekom/checkout`, `/ekom/mvcCheckout`, `/ekom/catalog`, `/ekom/provider`, `/ekom/api` and `/ekom/order-discounts` larger than `Ekom:Api:MaxRequestBodySize` bytes return `413`. The default is `128000`; `0` turns the limit off. `POST /ekom/order/add` has its own limit of 64,000 bytes.

**Field limits.** Requests to `order/add`, `updatecustomer`, the provider updates and `checkout/pay` can hold at most 200 fields. A field name can have at most 100 characters and a value at most 4,096. A request over a limit returns `400` with a `message`.

**Rate limits.** Ekom limits requests per client IP address and returns `429` when a limit is reached:

| Policy | Routes | Limit |
| --- | --- | --- |
| `order-add` | `order/add` | 30 per minute |
| `order-update` | `updatecustomer`, `updatetracking`, `updateshippingprovider`, `updatepaymentprovider` | 60 per minute |
| `checkout-pay` | `checkout/pay`, `mvcCheckout/pay` | 30 per minute |
| `order-coupon` | `coupon/apply`, `coupon/remove`, `order-discounts/calculate`, `order-discounts/coupon/mark-used` | 13 at most, refilled by 10 per minute |
| `order-giftcard` | `POST giftcards` | 13 at most, refilled by 10 per minute |

Behind a proxy or load balancer, turn on forwarded headers in the host application. Otherwise every visitor has the proxy's IP address and they all share one limit. Rate limits are not authentication.

**Trust.** Check the store, order, return URL, provider fields, consent and customer identity on your server. Keep payment secrets and the order-discount API key on the server.

**Examples.** The `Bruno/` folder at the root of the repository has requests you can run with the Bruno API client. When an example and a controller disagree, the controller is right.

## Related pages

- [HTTP API reference](../reference/http-api.md): every Ekom route in one list.
- [Orders and baskets](orders-and-baskets.md): what the basket endpoints do in .NET.
- [Checkout](checkout.md): how payment and order completion work.
- [Discount APIs and behavior](discounts/developers.md): coupons and the discount quote endpoint.
- [Tracking and consent](tracking-and-consent.md): send consent and tracking with basket requests.
- [Stores](stores.md): how Ekom decides which store a request belongs to.

[Back to Guides](README.md)
