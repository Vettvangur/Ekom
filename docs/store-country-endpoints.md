# Store and Country Endpoints

This page documents the general store and country endpoints that are useful in headless setups.

These endpoints are typically used early in a frontend flow to resolve store options and country data before catalog and checkout interactions begin.

## All stores

```http
GET /ekom/api/stores
```

### Purpose

Returns all stores available in Ekom.

### When to use

Use this when your headless frontend needs to:

- choose a store
- preload store metadata
- build multi-store UI flows

### Request parameters

This endpoint does not require request parameters.

### Example

```http
GET /ekom/api/stores
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns a list of stores. |

## Countries

```http
GET /ekom/api/countries
```

### Purpose

Returns all countries known to Ekom.

### When to use

Use this when your frontend needs to:

- populate country selectors
- support checkout address entry
- resolve country-based provider filtering

### Request parameters

This endpoint does not require request parameters.

### Example

```http
GET /ekom/api/countries
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns a list of countries. |

## Notes

- These endpoints are read-only.
- They are usually called before catalog browsing or checkout interactions begin.

## Related pages

- [Headless Endpoints Overview](headless-endpoints-overview.md)
- [Catalog Endpoints](catalog-endpoints.md)
- [Provider Endpoints](provider-endpoints.md)
