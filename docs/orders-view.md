# Orders View

The Orders view is the main operational screen in the Ekom manager.

It is where backoffice users and developers can browse orders, filter results, inspect summary metrics, open the order detail overlay, and trigger status changes.

## What the Orders view shows

The Orders view currently includes:

- order summary cards
- filter controls
- free-text search
- order results table
- inline status controls
- pagination
- export action

This makes it the central day-to-day screen for order operations in the manager.

## Where the data comes from

The Orders view is driven mainly by the manager search endpoint:

```text
GET /ekom/manager/SearchOrders
```

Supporting endpoints also provide:

- `GET /ekom/manager/StatusList`
- `GET /ekom/manager/stores`
- payment providers for the selected store through the manager resources layer

## Summary cards

At the top of the Orders view, the manager shows summary cards for the current result set:

- order count
- payments total
- average order amount

These values are recalculated when the current filters change.

## Filters

The Orders view supports filtering by:

- order status
- date from
- date to
- store
- payment provider
- product SKU
- tracking source
- tracking medium
- tracking campaign
- tracking term
- tracking content
- tracking click id

It also supports a free-text search field.

### Why this matters

This lets the manager work as both a simple order list and a more advanced operations search screen.

For example, you can narrow down orders by:

- store
- status
- payment provider
- tracking metadata

## Status filter behavior

The UI includes both:

- `Completed Orders`
- `All Orders`

plus the explicit status list returned by the manager API.

This lets users move quickly between broad operational views and specific status-based views.

## Store filter behavior

The store filter is populated from:

```text
GET /ekom/manager/stores
```

Only allowed stores are returned for the current backoffice user.

If a user does not have access to a store, that store does not appear in the manager filter list.

## Payment provider filter behavior

Payment providers are loaded for the currently selected store.

If the selected store changes:

- the manager refreshes the available payment providers
- invalid provider selections are cleared

This keeps the filter state aligned with the current store context.

## Search behavior

The Orders view includes a free-text search box.

Typing in the search field triggers a new search request and refreshes the current results.

This is useful for quickly locating orders by common searchable values without changing multiple filters.

## Orders table

The orders table currently shows:

- view action
- order number
- status
- customer name
- store
- created date
- payment total

Each row represents one order in the current result set.

## Opening an order

Each row has a `View` button.

That opens the order detail overlay after loading:

```text
GET /ekom/manager/OrderInfo/{orderId}
```

The selected order status is also synced into the manager state so the overlay can render and manage the status dropdown correctly.

## Inline status changes

The orders table allows direct status changes from the list view.

Each row includes a status dropdown bound to the available manager status list.

When the status changes, the manager sends an update through:

```text
POST /ekom/manager/changeOrderStatus
```

### Important behavior

- store access is validated for the order
- the order status is updated through the order API flow
- activity logs are written as part of that status-change behavior

## Pagination

The Orders view includes pagination when there is more than one result page.

The current implementation:

- shows a sliding page range
- includes condensed `...` entries for larger result sets
- reloads the results when the page changes

This keeps the table manageable for larger order volumes.

## Export action

The Orders view includes an `Export` button.

This is intended as part of the operational workflow for getting order data out of the manager.

If you document export behavior more deeply later, this can become its own dedicated page.

## Empty state

If the current filters return no results, the manager shows:

- `No orders found`

This is useful to call out in docs because it confirms the screen is filter-aware rather than broken.

## Loading state

While results are being loaded, the Orders view shows a loading message.

This is part of the normal manager search experience, especially on larger datasets.

## How the Orders view fits with the rest of the manager

The Orders view is the main entry point into:

- order detail view
- activity logs
- manual status management
- operational order search

In practice, many workflows begin here and continue into the order detail overlay.

## Why this page matters for developers

For developers, the Orders view is useful because it provides a fast way to verify:

- search behavior
- status transitions
- store access rules
- payment-provider filtering
- tracking-field filtering
- whether an order is reachable in the manager after a checkout flow

It is often the first place to check after changing order lifecycle logic.

## Common pitfalls

### Expecting all stores to appear

The manager only returns stores the current user is allowed to access.

### Assuming payment-provider filters are global

Payment-provider filter values are tied to the selected store.

### Treating the Orders view as a public API surface

This is a backoffice workflow view backed by manager endpoints, not a public storefront feature.

## Related pages

- [Manager Overview](manager-overview.md)
- [Order Detail View](order-detail-view.md)
- [Activity Logs](activity-logs.md)
- [Order Lifecycle](order-lifecycle.md)
