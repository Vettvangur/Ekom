# Order Endpoints

This page documents the public HTTP order endpoints used in headless setups.

These endpoints are useful when you are building a headless frontend, JavaScript cart flow, or other external integration that talks to Ekom over HTTP instead of using `Ekom.API.Order` directly.

## Base route

All endpoints on this page are under:

```text
/ekom/order
```

## Content types

Order endpoints use a mix of request formats depending on the endpoint.

Common patterns include:

- `application/json`
- `application/x-www-form-urlencoded`
- `multipart/form-data`

Many order endpoints also depend on a valid `storeAlias` supplied through route values, query string, headers, or request body.

## Reading orders

## Current order

```http
GET /ekom/order
GET /ekom/order/storeAlias/{storeAlias}
```

### Purpose

Returns the current order for the active or specified store.

### Request parameters

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `storeAlias` | route | No | Target store alias for the second route. |

### Example

```http
GET /ekom/order/storeAlias/Store
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the current order. |
| `404 Not Found` | No current order exists. |

## Order by guid

```http
GET /ekom/order/{orderId}
```

### Purpose

Returns a specific order by unique id.

### Request parameters

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `orderId` | route | Yes | Order unique id. |

### Example

```http
GET /ekom/order/11111111-1111-1111-1111-111111111111
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the order. |
| `404 Not Found` | The order does not exist. |

### Notes

This can return completed or final orders as well, not just the current basket.

## Related products from current order

```http
GET /ekom/order/relatedproducts/{count}
GET /ekom/order/relatedproducts/storeAlias/{storeAlias}/{count}
```

### Purpose

Returns related products based on the current order.

### Request parameters

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `count` | route | Yes | Number of related products to return. |
| `storeAlias` | route | No | Target store alias for the second route. |

### Example

```http
GET /ekom/order/relatedproducts/storeAlias/Store/4
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns related products. |
| `404 Not Found` | No current order exists. |

## Cart operations

## Add to order

```http
POST /ekom/order/add
```

### Purpose

Adds a product to the current order.

This endpoint can also update quantity depending on the selected action.

### Request fields

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `ProductId` | body | Yes | Product key. |
| `VariantId` | body | No | Variant key. |
| `StoreAlias` | body | No | Target store alias. |
| `Quantity` | body | Yes | Quantity to add or set. |
| `Action` | body | No | `AddOrUpdate`, `Set`, or `New`. |

### Example JSON request

```http
POST /ekom/order/add
Content-Type: application/json

{
  "ProductId": "cb6906db-c156-4c88-814c-6cc181bd1ae3",
  "VariantId": "5abc5a27-cf22-4438-bcb6-f12c2a8564c9",
  "StoreAlias": "store",
  "Quantity": 1,
  "Action": "AddOrUpdate"
}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the updated order. |
| `400 Bad Request` | Request data is missing or invalid. |

### Notes

- request body must be a JSON object when sending JSON
- request size larger than the controller limit is rejected
- consent and tracking data can also be supplied in the request body
- custom request fields are preserved and passed as custom order data

## Remove orderline

```http
POST /ekom/order/removeorderline
```

### Purpose

Removes an order line by line id.

### Request fields

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `LineId` | body | Yes | Order line id. |
| `StoreAlias` | body | Yes | Target store alias. |

### Example JSON request

```http
POST /ekom/order/removeorderline
Content-Type: application/json

{
  "LineId": "00000000-0000-0000-0000-000000000030",
  "StoreAlias": "Store"
}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the updated order. |
| `400 Bad Request` | Request data is missing or invalid. |

## Update orderline quantity

```http
POST /ekom/order/Updateorderlinequantity
```

### Purpose

Sets a specific quantity on an existing order line.

### Request fields

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `LineId` | body | Yes | Order line id. |
| `Quantity` | body | Yes | New quantity. |
| `StoreAlias` | body | Yes | Target store alias. |

### Example JSON request

```http
POST /ekom/order/Updateorderlinequantity
Content-Type: application/json

{
  "LineId": "00000000-0000-0000-0000-000000000030",
  "Quantity": 3,
  "StoreAlias": "Store"
}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the updated order. |
| `400 Bad Request` | Request data is missing or invalid. |

## Order data

## Update customer information

```http
POST /ekom/order/updatecustomer
```

### Purpose

Updates customer-related order data.

### Request fields

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `StoreAlias` | body | No | Target store alias. |
| `customerEmail` | body | No | Customer email. |
| `customerName` | body | No | Customer name. |
| `customerPhone` | body | No | Customer phone. |
| `consent` | body | No | Consent payload. |
| `tracking` | body | No | Tracking payload. |

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

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the updated order. |

### Notes

- only JSON and form content types are supported
- first-time customer email can mark checkout as started in the core flow

## Update shipping provider

```http
POST /ekom/order/update/shippingprovider/
POST /ekom/order/updateshippingprovider
```

### Purpose

Assigns the shipping provider on the current order.

### Request fields

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `ShippingProvider` | body/query | Yes | Shipping provider guid. |
| `StoreAlias` | body/query | No | Target store alias. |
| `customshipping` | body | No | Custom provider-specific payload. |
| `ekomUpdateInformation` | body | No | Additional update information. |
| `customerDeliveryDate` | body | No | Delivery-specific value. |

### Example JSON request

```http
POST /ekom/order/updateshippingprovider
Content-Type: application/json

{
  "ShippingProvider": "00000000-0000-0000-0000-000000000010",
  "StoreAlias": "Store"
}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the updated order. |
| `400 Bad Request` | Required values are missing or invalid. |

### Notes

- provider id must be a valid `Guid`
- successful provider changes create an `Info` activity log entry

## Update payment provider

```http
POST /ekom/order/update/paymentprovider/
POST /ekom/order/updatepaymentprovider
```

### Purpose

Assigns the payment provider on the current order.

### Request fields

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `PaymentProvider` | body/query | Yes | Payment provider guid. |
| `StoreAlias` | body/query | No | Target store alias. |
| `custompayment` | body | No | Custom provider-specific payload. |
| `ekomUpdateInformation` | body | No | Additional update information. |
| `customerDeliveryDate` | body | No | Delivery-specific value. |

### Example JSON request

```http
POST /ekom/order/updatepaymentprovider
Content-Type: application/json

{
  "PaymentProvider": "00000000-0000-0000-0000-000000000020",
  "StoreAlias": "Store"
}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the updated order. |
| `400 Bad Request` | Required values are missing or invalid. |

### Notes

- successful provider changes create an `Info` activity log entry
- assigning the payment provider does not complete checkout

## Change currency

```http
POST /ekom/order/currency?currency={currency}
```

### Purpose

Changes the current order currency for the current store and updates the currency cookie.

### Request parameters

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `currency` | query | Yes | Target currency code. |

### Example

```http
POST /ekom/order/currency?currency=USD
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the updated order, or `null` if no active order exists. |
| `404 Not Found` | The current store cannot be resolved. |

### Notes

This endpoint also updates the `EkomCurrency-{StoreAlias}` cookie.

## Coupons

## Apply coupon to order

```http
POST /ekom/order/coupon/apply
```

### Purpose

Applies a coupon to the current order.

### Request fields

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `coupon` | body | Yes | Coupon code. |
| `storeAlias` | body | No | Target store alias. |

### Example JSON request

```http
POST /ekom/order/coupon/apply
Content-Type: application/json

{
  "coupon": "spring10",
  "storeAlias": "Store"
}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Coupon applied. |
| `400 Bad Request` | Coupon is empty or request is invalid. |
| `450` | Discount was not modified because a better discount was found. |

### Notes

This endpoint is rate limited by the `order-coupon` policy.

## Remove coupon from order

```http
POST /ekom/order/coupon/remove
```

### Purpose

Removes the coupon from the current order.

### Request parameters

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `storeAlias` | query | No | Target store alias. |

### Example

```http
POST /ekom/order/coupon/remove?storeAlias=Store
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Coupon removed. |

## Common endpoint behavior

The order controller uses an API exception filter, so some domain exceptions are handled consistently outside the endpoint bodies.

### Things to keep in mind

- JSON endpoints expect valid JSON objects
- some endpoints support both JSON and form posts
- many order operations depend on a valid `storeAlias`
- provider update endpoints also allow query string fallback for provider or store values

## When to use these endpoints vs `API.Order`

Use these HTTP endpoints when:

- building a JavaScript cart frontend
- building a headless frontend
- integrating from an external client

Use `Ekom.API.Order` when:

- writing server-side application logic inside the Umbraco/.NET app
- building custom services, scheduled jobs, or internal integrations

## Related pages

- [Headless Endpoints Overview](headless-endpoints-overview.md)
- [Checkout Endpoints](checkout-endpoints.md)
- [Provider Endpoints](provider-endpoints.md)
- [Order API](api-order-reference.md)
- [Order Lifecycle](order-lifecycle.md)
- [Activity Logs](activity-logs.md)
