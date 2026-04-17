# Order Endpoints

This page documents the public HTTP order endpoints under:

- `/ekom/order`

These endpoints are useful when you are building a headless frontend, JavaScript cart flow, or other external integration that talks to Ekom over HTTP instead of using `Ekom.API.Order` directly.

## Base route

All endpoints on this page are under:

```text
/ekom/order
```

## Content types

Different endpoints accept different content types, but the order controller currently supports a mix of:

- `application/json`
- `application/x-www-form-urlencoded`
- `multipart/form-data`

Some endpoints also read query string values for provider and store alias fallback behavior.

## 1. Add product to order

### Endpoint

```text
POST /ekom/order/add
```

### Purpose

Adds a product to the current order.

This endpoint can also update quantity depending on the selected action.

### Supported content types

- `application/json`
- `application/x-www-form-urlencoded`
- `multipart/form-data`

### Example JSON request

```http
POST /ekom/order/add
Content-Type: application/json

{
  "productId": "00000000-0000-0000-0000-000000000001",
  "quantity": 1,
  "storeAlias": "Store",
  "variantId": "00000000-0000-0000-0000-000000000002",
  "action": "AddOrUpdate"
}
```

### Notes

- request body must be JSON object when sending JSON
- request size larger than the controller limit is rejected
- consent and tracking data can also be supplied in the request body
- custom request fields are preserved and passed as custom order data

### Response

Returns `200 OK` with the updated `IOrderInfo` payload.

## 2. Get order by id

### Endpoint

```text
GET /ekom/order/{orderId}
```

### Purpose

Returns a specific order by unique id.

### Example

```http
GET /ekom/order/11111111-1111-1111-1111-111111111111
```

### Response

- `200 OK` with the order
- `404 Not Found` if the order does not exist

### Important behavior

This can return completed/final orders as well, not just the current basket.

## 3. Get current order by store

### Endpoints

```text
GET /ekom/order
GET /ekom/order/storeAlias/{storeAlias}
```

### Purpose

Returns the current order for the active or specified store.

### Example

```http
GET /ekom/order/storeAlias/Store
```

### Response

- `200 OK` with the order
- `404 Not Found` if no current order exists

## 4. Get related products from current order

### Endpoints

```text
GET /ekom/order/relatedproducts/{count}
GET /ekom/order/relatedproducts/storeAlias/{storeAlias}/{count}
```

### Purpose

Returns related products based on the current order.

### Example

```http
GET /ekom/order/relatedproducts/storeAlias/Store/4
```

### Response

- `200 OK` with related products
- `404 Not Found` if there is no current order

## 5. Update customer information

### Endpoint

```text
POST /ekom/order/updatecustomer
```

### Purpose

Updates customer-related order data.

### Supported content types

- form data
- JSON

### Example JSON request

```http
POST /ekom/order/updatecustomer
Content-Type: application/json

{
  "storeAlias": "Store",
  "customerEmail": "customer@example.com",
  "customerName": "Jane Doe",
  "customerPhone": "+3541234567"
}
```

### Example with consent and tracking

```http
POST /ekom/order/updatecustomer
Content-Type: application/json

{
  "storeAlias": "Store",
  "customerEmail": "customer@example.com",
  "consent": {
    "analytics": true,
    "marketing": false
  },
  "tracking": {
    "landingUrl": "https://example.com/products/shoe"
  }
}
```

### Response

Returns `200 OK` with the updated order.

### Important behavior

- only JSON and form content types are supported
- first-time customer email can mark checkout as started in the core flow

## 6. Update tracking

### Endpoint

```text
POST /ekom/order/updatetracking
```

### Purpose

Attaches tracking information to the current order for a store.

### Content type

- `application/json`

### Example request

```http
POST /ekom/order/updatetracking
Content-Type: application/json

{
  "storeAlias": "Store",
  "tracking": {
    "landingUrl": "https://example.com/products/shoe",
    "referrerUrl": "https://google.com"
  },
  "consent": {
    "analytics": true,
    "marketing": false
  }
}
```

### Response

- `200 OK` with the updated order
- `400 Bad Request` if `storeAlias` or `tracking` is missing

## 7. Update shipping provider

### Endpoints

```text
POST /ekom/order/update/shippingprovider/
POST /ekom/order/updateshippingprovider
```

### Purpose

Assigns the shipping provider on the current order.

### Supported request shapes

- form data
- JSON
- query string fallback for provider/store alias values

### Example JSON request

```http
POST /ekom/order/updateshippingprovider
Content-Type: application/json

{
  "ShippingProvider": "00000000-0000-0000-0000-000000000010",
  "storeAlias": "Store"
}
```

### Response

- `200 OK` with the updated order
- `400 Bad Request` if required values are missing

### Important behavior

- provider id must be a valid `Guid`
- successful provider changes create an `Info` activity log entry

## 8. Update payment provider

### Endpoints

```text
POST /ekom/order/update/paymentprovider/
POST /ekom/order/updatepaymentprovider
```

### Purpose

Assigns the payment provider on the current order.

### Example JSON request

```http
POST /ekom/order/updatepaymentprovider
Content-Type: application/json

{
  "PaymentProvider": "00000000-0000-0000-0000-000000000020",
  "storeAlias": "Store"
}
```

### Response

- `200 OK` with the updated order
- `400 Bad Request` if required values are missing

### Important behavior

- successful provider changes create an `Info` activity log entry
- assigning the payment provider does not complete checkout

## 9. Remove order line

### Endpoint

```text
POST /ekom/order/removeorderline
```

### Purpose

Removes an order line by line id.

### Example request

```http
POST /ekom/order/removeorderline
Content-Type: application/json

{
  "lineId": "00000000-0000-0000-0000-000000000030",
  "storeAlias": "Store"
}
```

### Response

- `200 OK` with the updated order
- `400 Bad Request` if request data is missing or invalid

## 10. Update order line quantity

### Endpoint

```text
POST /ekom/order/Updateorderlinequantity
```

### Purpose

Sets a specific quantity on an existing order line.

### Example request

```http
POST /ekom/order/Updateorderlinequantity
Content-Type: application/json

{
  "lineId": "00000000-0000-0000-0000-000000000030",
  "quantity": 3,
  "storeAlias": "Store"
}
```

### Response

- `200 OK` with the updated order
- `400 Bad Request` if request data is missing or invalid

## 11. Change currency

### Endpoint

```text
POST /ekom/order/currency
```

### Purpose

Changes the current order currency for the current store and updates the currency cookie.

### Example request

```http
POST /ekom/order/currency?currency=USD
```

### Response

- `200 OK` with the updated order or `null` if no active order exists
- `404 Not Found` if the current store cannot be resolved

### Important behavior

This endpoint also updates the `EkomCurrency-{StoreAlias}` cookie.

## 12. Apply coupon to order

### Endpoint

```text
POST /ekom/order/coupon/apply
```

### Purpose

Applies a coupon to the current order.

### Example request

```http
POST /ekom/order/coupon/apply
Content-Type: application/json

{
  "coupon": "spring10",
  "storeAlias": "Store"
}
```

### Response

- `200 OK` when the coupon is applied
- `450` when the discount is not modified because a better discount was found
- `400 Bad Request` if coupon is empty

### Notes

This endpoint is rate limited by the `order-coupon` policy.

## 13. Remove coupon from order

### Endpoint

```text
POST /ekom/order/coupon/remove
```

### Purpose

Removes the coupon from the current order.

### Example request

```http
POST /ekom/order/coupon/remove?storeAlias=Store
```

### Response

- `200 OK`

## Common endpoint behavior

The order controller uses an API exception filter, so some domain exceptions are handled consistently outside the endpoint bodies.

### Things to keep in mind

- JSON endpoints expect valid JSON objects
- some endpoints support both JSON and form posts
- many order operations depend on a valid `storeAlias`
- provider update endpoints also allow query string fallback for provider/store values

## When to use these endpoints vs `API.Order`

Use these HTTP endpoints when:

- building a JavaScript/cart frontend
- building a headless frontend
- integrating from an external client

Use `Ekom.API.Order` when:

- writing server-side application logic inside the Umbraco/.NET app
- building custom services, scheduled jobs, or internal integrations

## Related pages

- [API.Order Reference](api-order-reference.md)
- [Quick Start](quick-start.md)
- [Order Lifecycle](order-lifecycle.md)
- [Checkout Flow Overview](checkout-flow-overview.md)
- [Activity Logs](activity-logs.md)
