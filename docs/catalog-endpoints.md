# Catalog Endpoints

This page documents the public HTTP catalog endpoints used in headless setups.

These endpoints are useful for rendering category pages, product pages, product listings, search results, filters, and related products.

## Base route

All endpoints on this page are under:

```text
/ekom/catalog
```

## Product endpoints

## All products

```http
POST /ekom/catalog/allproducts
```

### Purpose

Returns a product listing with optional filters, paging, and ordering.

### Example JSON request

```http
POST /ekom/catalog/allproducts
Content-Type: application/json

{
  "propertyFilters": {},
  "metaFilters": {},
  "page": 1,
  "pageSize": 24,
  "orderBy": "DateDesc"
}
```

### Common body fields

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `propertyFilters` | body | No | Property-based filters. |
| `metaFilters` | body | No | Metafield-based filters. |
| `page` | body | No | Page number. |
| `pageSize` | body | No | Page size. |
| `orderBy` | body | No | Product ordering. |

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns a product response with products, paging, and filters. |

## Product by guid

```http
GET /ekom/catalog/product/{guid}
```

### Example

```http
GET /ekom/catalog/product/44947ae7-6145-4a1b-9870-088a0ad4baba
storeAlias: Store
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the product. |
| `404 Not Found` | Product was not found. |

## Product by id

```http
GET /ekom/catalog/product/{id}
```

### Example

```http
GET /ekom/catalog/product/1257
storeAlias: Store
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the product. |
| `404 Not Found` | Product was not found. |

## Product by route

```http
GET /ekom/catalog/product/route?route={route}
```

### Example

```http
GET /ekom/catalog/product/route?route=/products/shoe
storeAlias: Store
```

### Request parameters

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `route` | query | Yes | Product route. |

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the product. |
| `404 Not Found` | Product was not found. |

## Product by sku

```http
GET /ekom/catalog/product/sku/{sku}
```

### Example

```http
GET /ekom/catalog/product/sku/AS-1012B753-001
storeAlias: Store
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the product. |
| `404 Not Found` | Product was not found. |

## Product search

```http
POST /ekom/catalog/productsearch
```

### Purpose

Searches catalog products using the configured search service.

### Example JSON request

```http
POST /ekom/catalog/productsearch
Content-Type: application/json

{
  "SearchQuery": "shoe",
  "StoreAlias": "Store",
  "Page": 1,
  "PageSize": 24
}
```

### Request fields

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `SearchQuery` | body | Yes | Search text. |
| `StoreAlias` | body | No | Target store alias. |
| `Page` | body | No | Page number. |
| `PageSize` | body | No | Page size. |

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns a product response with search results. |

## Products by ids

```http
POST /ekom/catalog/productsbyids
```

### Example JSON request

```http
POST /ekom/catalog/productsbyids
Content-Type: application/json

{
  "Ids": [1228, 1257],
  "Page": 1,
  "PageSize": 24,
  "StoreAlias": "Store"
}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns a product response. |

## Products by keys

```http
POST /ekom/catalog/productsbykeys
```

### Example JSON request

```http
POST /ekom/catalog/productsbykeys
Content-Type: application/json

{
  "Keys": [
    "343f79e7-c082-4a8e-b5c1-74cd0db82687"
  ],
  "Page": 1,
  "PageSize": 24,
  "StoreAlias": "Store"
}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns a product response. |

## Products by skus

```http
POST /ekom/catalog/productsbyskus
```

### Example JSON request

```http
POST /ekom/catalog/productsbyskus
Content-Type: application/json

{
  "Skus": ["SKU-1", "SKU-2"],
  "Page": 1,
  "PageSize": 24,
  "StoreAlias": "Store"
}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns a product response. |

## Products recursive by id

```http
POST /ekom/catalog/productsrecursive/{id}
```

### Purpose

Returns products from a category and all recursive descendants.

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns a product response. |

## Products recursive by guid

```http
POST /ekom/catalog/productsrecursive/{guid}
```

## Products recursive by route

```http
POST /ekom/catalog/productsrecursive/route?route={route}
```

### Example JSON request

```http
POST /ekom/catalog/productsrecursive/route?route=/shop/shoes
Content-Type: application/json

{
  "Page": 1,
  "PageSize": 24,
  "StoreAlias": "Store"
}
```

### Request parameters

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `route` | query | Yes | Category route. |

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns a product response. |

## Related products by product key

```http
GET /ekom/catalog/relatedproducts/{productKey}/{count}
```

### Example

```http
GET /ekom/catalog/relatedproducts/ecc9dcf1-4910-4c89-b3d8-a8e4ff3d65b2/4
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns related products. |

## Related products by multiple product keys

```http
GET /ekom/catalog/relatedproducts?ids={guid1},{guid2}&count={count}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns related products. |

## Related products by sku

```http
GET /ekom/catalog/relatedproductsbysku/{sku}/{count}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns related products. |

## Related products by skus

```http
GET /ekom/catalog/relatedproductsbyskus?skus={sku1},{sku2}&count={count}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns related products. |

## Category endpoints

## All categories

```http
GET /ekom/catalog/allcategories
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns all categories. |

## Root categories

```http
GET /ekom/catalog/rootcategories
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns root categories. |

## Category by guid

```http
GET /ekom/catalog/category/{guid}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the category. |
| `404 Not Found` | Category was not found. |

## Category by id

```http
GET /ekom/catalog/category/{id}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the category. |
| `404 Not Found` | Category was not found. |

## Category by route

```http
GET /ekom/catalog/category/route?route={route}
```

### Example

```http
GET /ekom/catalog/category/route?route=/shop/shoes
storeAlias: Store
```

### Request parameters

| Name | Source | Required | Description |
| --- | --- | --- | --- |
| `route` | query | Yes | Category route. |

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns the category. |
| `404 Not Found` | Category was not found. |

## Categories by ids

```http
GET /ekom/catalog/categoriesbyids
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns matching categories. |

## Categories by keys

```http
GET /ekom/catalog/categoriesbykeys
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns matching categories. |

## Category filters by guid

```http
GET /ekom/catalog/categoryfilters/{guid}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns category filters. |

## Category filters by id

```http
GET /ekom/catalog/categoryfilters/{id}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns category filters. |

## Subcategories by guid

```http
GET /ekom/catalog/subcategories/{guid}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns subcategories. |

## Subcategories by id

```http
GET /ekom/catalog/subcategories/{id}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns subcategories. |

## Recursive subcategories by guid

```http
GET /ekom/catalog/subcategoriesrecursive/{guid}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns recursive subcategories. |

## Recursive subcategories by id

```http
GET /ekom/catalog/subcategoriesrecursive/{id}
```

### Response

| Status | Meaning |
| --- | --- |
| `200 OK` | Returns recursive subcategories. |

## Notes

- The Bruno collection shows both route-based and identifier-based catalog access patterns.
- Product list endpoints typically use `POST` with a JSON body so filters, paging, and store context can be passed together.
- Some read endpoints use the `storeAlias` header in Bruno examples.

## Related pages

- [Headless Endpoints Overview](headless-endpoints-overview.md)
- [Store and Country Endpoints](store-country-endpoints.md)
- [Catalog API](catalog-api.md)
- [Order Endpoints](order-endpoints.md)
