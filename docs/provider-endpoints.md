# Provider Endpoints

This page documents the public HTTP provider endpoints used in headless setups.

These endpoints are useful when a frontend needs to load available shipping or payment providers before assigning one to the order.

## Base route

All endpoints on this page are under:

```text
/ekom/provider
```

## Payment providers

## Payment providers list

```http
GET /ekom/provider/paymentsproviders
```

### Purpose

Returns payment providers for the current store and optional checkout constraints.

### Query parameters

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `countryCode` | query | No | Country filter used against provider zones. |
| `orderAmount` | query | No | Amount filter used against provider range rules. |

### Example

```http
GET /ekom/provider/paymentsproviders?countryCode=is-IS&orderAmount=5000
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns payment providers. |

### Notes

- the Bruno collection uses `countryCode=is-IS`
- provider availability may depend on zone and amount rules

## Payment provider by guid

```http
GET /ekom/provider/paymentsprovider/{guid}
```

### Example

```http
GET /ekom/provider/paymentsprovider/916d8631-11b6-4247-ab6e-c0b5eac6b144
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the payment provider. |
| `404 Not Found` | Provider was not found. |

## Shipping providers

## Shipping providers list

```http
GET /ekom/provider/shippingproviders/{storeAlias}
```

### Purpose

Returns shipping providers for a store.

### Example

```http
GET /ekom/provider/shippingproviders/heilsa
```

### Request parameters

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `storeAlias` | route | Yes | Store alias used to load providers. |

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns shipping providers. |

## Shipping provider by guid

```http
GET /ekom/provider/shippingproviders/{guid}
```

### Example

```http
GET /ekom/provider/shippingproviders/30e170ca-db69-43cb-aa13-cc0ec1682ae7
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the shipping provider. |
| `404 Not Found` | Provider was not found. |

## Zones

## All zones

```http
GET /ekom/provider/zones
```

### Purpose

Returns all configured zones.

### Example

```http
GET /ekom/provider/zones
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns all zones. |

## When to use provider endpoints

Use these endpoints when you need to:

- show selectable payment methods
- show selectable shipping methods
- filter providers by country or amount
- inspect zone setup in a headless frontend or integration

## Notes

- Payment provider listing supports `countryCode` and `orderAmount` filters.
- Provider availability can still be affected by Ekom provider events and business rules.
- In a normal checkout flow, these endpoints are usually called before the order update endpoints that assign the chosen provider.

## Related pages

- [Headless Endpoints Overview](headless-endpoints-overview.md)
- [Order Endpoints](order-endpoints.md)
- [Checkout Endpoints](checkout-endpoints.md)
- [Provider API](provider-api.md)
