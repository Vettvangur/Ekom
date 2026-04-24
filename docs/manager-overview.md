# Manager Overview

The Ekom manager is the backoffice UI for working with orders and analytics inside Umbraco.

It is designed for operational users and developers who need a built-in interface for inspecting orders, changing statuses, reviewing activity logs, and working with store-level order data.

## What the manager includes

The current manager experience includes two main views:

- `Orders`
- `Analytics`

From the manager, users can:

- list and search orders
- filter by date, status, store, payment provider, SKU, and tracking fields
- open order detail overlays
- change order status
- inspect order activity logs
- view analytics charts and most sold products

## Where the manager lives

The manager UI is delivered through the Ekom app plugin assets under:

```text
Ekom.Web/App_Plugins/Ekom/Manager/
```

The backoffice API surface for the manager lives under:

```text
/ekom/manager
```

## Manager access model

Manager access is controlled through Ekom configuration.

The main settings are:

- `Manager.SectionAccessGroup`
- `Manager.StoreGroupPermissions`

### Example configuration

```json
{
  "Ekom": {
    "Manager": {
      "SectionAccessGroup": "ekom",
      "StoreGroupPermissions": {
        "Store": [ "StoreGroup" ],
        "Store2": [ "Store2Group" ]
      }
    }
  }
}
```

### Access rules

- a user can access the Ekom manager if they belong to the configured section access group or a mapped store group
- store access is still enforced per store
- users only see stores they are allowed to access
- Umbraco administrators bypass normal store restrictions

## Orders view

The `Orders` view is the main operational view.

It shows:

- order count
- payment totals
- average order amount
- order list
- filters and search

### Current filter areas

The manager supports filtering on:

- order status
- date range
- store
- payment provider
- product SKU
- tracking fields such as source, medium, campaign, term, content, and click id

### Current order list behavior

Each row includes:

- order number
- status
- customer name
- store
- created date
- payment total

Users can open an order overlay from the list and can also change order status directly from the grid.

## Order detail overlay

The order detail UI is loaded from the manager order overlay.

From there, a user can inspect:

- order details
- customer information
- shipping and payment provider information
- status controls
- activity logs

The overlay is also where activity logs are rendered with:

- message
- date/time
- type icon
- expand/collapse for longer messages

## Activity logs in manager

The manager includes a dedicated endpoint for order activity logs:

```text
GET /ekom/manager/OrderLogs/{orderId}
```

The manager overlay requests these logs separately and renders them under the order detail view.

This keeps the activity log feature decoupled from the main order payload while still making it available in the UI.

## Analytics view

The `Analytics` tab provides summary analytics in the manager.

Current analytics capabilities include:

- sales revenue chart
- total orders chart
- average order value chart
- most sold products view

These views are filtered by:

- date range
- store
- order status

## Manager endpoints

Some of the key manager endpoints include:

- `GET /ekom/manager/AllOrders`
- `GET /ekom/manager/Order/{orderId}`
- `GET /ekom/manager/OrderInfo/{orderId}`
- `GET /ekom/manager/OrderLogs/{orderId}`
- `GET /ekom/manager/SearchOrders`
- `GET /ekom/manager/MostSoldProducts`
- `GET /ekom/manager/StatusList`
- `GET /ekom/manager/stores`
- `POST /ekom/manager/changeOrderStatus`

These endpoints are intended for the backoffice manager experience rather than for public storefront use.

## Status changes from manager

When a manager user changes status from the UI:

- access is validated against the order store
- the current backoffice username is passed into the order status update flow
- an activity log entry is written

This means manager-triggered status changes become part of the order history.

## Why the manager matters for developers

Even if you build a custom storefront or headless frontend, the Ekom manager is still useful because it gives you:

- operational visibility into orders
- a built-in way to inspect order data
- a place to validate activity logs and checkout flows
- a practical UI for support and internal teams

## Common pitfalls

### Treating manager endpoints as public APIs

The manager endpoints are backoffice-oriented and protected by Umbraco user authorization.

### Forgetting store access rules

A user may have access to the manager but still be blocked from orders in stores they are not allowed to access.

### Assuming order activity logs are included in every order payload

The manager loads order logs through a separate endpoint.

## Related pages

- [Activity Logs](activity-logs.md)
- [Order Lifecycle](order-lifecycle.md)
- [Order Endpoints](order-endpoints.md)
- [Configuration](configuration.md)
