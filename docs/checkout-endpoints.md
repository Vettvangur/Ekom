# Checkout Endpoints

This page documents the public HTTP checkout endpoints used in headless setups.

These endpoints are typically used after the current order has been prepared with order lines, customer data, shipping provider, and payment provider information.

## Base route

All endpoints on this page are under:

```text
/ekom/checkout
```

## Pay

```http
POST /ekom/checkout/pay
```

### Purpose

Submits the current order into the payment flow.

### Query parameters

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `culture` | query | No | Target culture for the payment flow. |

### Example JSON request

```http
POST /ekom/checkout/pay?culture=is-IS
Content-Type: application/json

{
  "PaymentProvider": "",
  "ShippingProvider": "",
  "CardNumber": "",
  "CVV": "",
  "Year": 2024,
  "Month": 10,
  "StoreAlias": "store",
  "Culture": "is-IS",
  "ReturnUrl": "/success-page",
  "customerTest": ""
}
```

### Common body fields

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `PaymentProvider` | body | No | Payment provider alias or identifier. |
| `ShippingProvider` | body | No | Shipping provider alias or identifier. |
| `CardNumber` | body | No | Provider-specific card field. |
| `CVV` | body | No | Provider-specific card field. |
| `Year` | body | No | Provider-specific expiry year. |
| `Month` | body | No | Provider-specific expiry month. |
| `StoreAlias` | body | No | Target store alias. |
| `Culture` | body | No | Checkout culture. |
| `ReturnUrl` | body | No | Return URL after payment flow. |

Some providers may also use payment-specific fields such as card details or custom request values.

### When to use

Use this after:

- the order exists
- customer information has been updated
- shipping and payment providers have been selected if needed

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns checkout or payment response data. |
| `400 Bad Request` | Request data is invalid for the payment flow. |

## Notes

- `Pay` is the handoff from order preparation into payment processing.
- Query and body both participate in the request shape.
- In headless setups, this usually comes after the order endpoints have already been used to build the checkout state.

## Related pages

- [Headless Endpoints Overview](headless-endpoints-overview.md)
- [Order Endpoints](order-endpoints.md)
- [Provider Endpoints](provider-endpoints.md)
- [Order API](api-order-reference.md)
