# Headless Endpoints Overview

Ekom exposes a set of HTTP endpoints that can be used to build headless storefronts, custom checkout flows, and external integrations.

These endpoints are useful when your frontend or client application talks to Ekom over HTTP instead of using the C# APIs directly.

## Base approach

Most headless flows in Ekom follow this shape:

1. load stores and countries
2. load categories and products
3. load the current order
4. add or update order lines
5. update customer information
6. select shipping and payment providers
7. submit the order to checkout/payment

## Endpoint groups

### Store and country endpoints

Use these first when your frontend needs environment or store-level context.

- list available stores
- list countries

See: [Store and Country Endpoints](store-country-endpoints.md)

### Catalog endpoints

Use these to render category pages, product pages, listing pages, filters, and related products.

- products by id, guid, sku, or route
- all products and filtered product lists
- categories, root categories, subcategories, and category filters
- product search
- related products

See: [Catalog Endpoints](catalog-endpoints.md)

### Order endpoints

Use these to manage the cart or current order in a headless flow.

- current order
- add to order
- remove orderline
- update orderline quantity
- update customer information
- update payment or shipping provider
- apply or remove coupons
- change currency

See: [Order Endpoints](order-endpoints.md)

### Checkout endpoints

Use these when the order is ready to move into payment processing.

- pay

See: [Checkout Endpoints](checkout-endpoints.md)

### Provider endpoints

Use these to load available payment and shipping providers before assigning one to an order.

- payment providers
- shipping providers
- zones

See: [Provider Endpoints](provider-endpoints.md)

## Store context

Many endpoints depend on store context.

Depending on the endpoint, store context may come from:

- a `storeAlias` header
- a `storeAlias` query parameter
- a `StoreAlias` property in the request body
- the current request context

The Bruno collection in this repository uses `storeAlias` heavily, so that is a good reference for how the endpoints are expected to be called.

## Content types

Ekom endpoints use a mix of request formats depending on the endpoint.

Common patterns include:

- `GET` with route and query parameters
- `POST` with JSON body
- `application/x-www-form-urlencoded`
- `multipart/form-data`

Order and checkout flows typically use JSON bodies.

## Bruno collection

This repository contains a Bruno collection under `Bruno/` that can be used as the source of truth for example requests.

The collection currently includes endpoint coverage for:

- API
- Catalog
- Order
- Checkout
- Providers
- Klaviyo

Klaviyo endpoints are plugin-specific and should be documented on the `Ekom.Klaviyo` page rather than as part of the core headless endpoint set.

## Recommended reading order

For a new headless implementation, read the endpoint docs in this order:

1. [Store and Country Endpoints](store-country-endpoints.md)
2. [Catalog Endpoints](catalog-endpoints.md)
3. [Order Endpoints](order-endpoints.md)
4. [Provider Endpoints](provider-endpoints.md)
5. [Checkout Endpoints](checkout-endpoints.md)

## Related pages

- [Store API](store-api.md)
- [Catalog API](catalog-api.md)
- [Order API](api-order-reference.md)
- [Provider API](provider-api.md)
