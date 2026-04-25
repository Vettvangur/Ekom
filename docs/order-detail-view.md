# Order Detail View

The Ekom manager order detail view is the backoffice overlay used to inspect a single order in more detail.

It is intended for operational users and developers who need to inspect the full order payload, customer information, providers, totals, status changes, and activity logs from the manager UI.

## What the order detail view shows

The current order detail overlay includes:

- order header information
- status controls
- billing details
- shipping details
- payment method
- shipping method
- order lines and totals
- activity logs

## How the order detail view is loaded

The manager uses the following endpoint to load the main order detail payload:

```text
GET /ekom/manager/OrderInfo/{orderId}
```

Before returning the order, Ekom validates that the current manager user is allowed to access the order’s store.

If the user does not have access to the store, the manager returns a forbidden response.

## Header section

The top section of the order detail view currently shows:

- order number
- order status
- unique id
- created date
- paid date
- store alias
- charged amount

This gives a quick summary of the current operational state of the order.

## Changing order status

The order detail overlay includes a status dropdown and a save action.

The manager sends status changes through:

```text
POST /ekom/manager/changeOrderStatus
```

### Request behavior

The manager sends:

- `orderId`
- `orderStatus`
- `notify`

The `notify` value controls whether events should be fired when the status changes.

### Important behavior

When a manager user changes status:

- access is validated for the order store
- the current backoffice username is passed into the order status flow
- an activity log entry is written

## Billing information section

The billing section shows customer information from:

- `order.customerInformation.customer`

This includes fields such as:

- name
- email
- address
- apartment
- city
- country
- zipcode
- phone

### Extra customer data

The overlay also renders extra customer properties.

It filters out default built-in keys and shows the remaining custom customer values as extra customer data.

This is useful when your checkout captures additional fields beyond the standard customer set.

## Shipping information section

The shipping section shows:

- shipping recipient information
- shipping address fields
- custom shipping properties

If there is no separate shipping information, the manager shows:

- `Same as billing address`

### Extra shipping data

Like the customer section, the shipping section also renders extra shipping properties that are not part of the standard built-in field set.

## Payment method section

If the order has a payment provider, the order detail view shows:

- payment provider title
- provider price when available
- extra custom payment data

The custom payment values come from the stored payment provider custom data.

## Shipping method section

If the order has a shipping provider, the order detail view shows:

- shipping provider title
- provider price when available
- extra custom shipping data

This is useful when shipping providers store additional information such as pickup notes or shipping-specific metadata.

## Order details table

The order lines table shows the line-level financial details for the order.

Each row includes:

- product title
- product SKU
- variant title and variant SKU when applicable
- quantity
- unit price including VAT
- VAT amount
- discount amount
- total including VAT

## Order summary totals

The footer of the order details table currently shows:

- sub total including VAT
- discount amount
- shipping total when a shipping provider exists
- VAT total
- total charged amount

This makes the view useful for support, finance checks, and general order inspection.

## Activity log section

The order detail overlay includes a dedicated activity log section beneath the order details table.

The logs are loaded separately through:

```text
GET /ekom/manager/OrderLogs/{orderId}
```

### Why logs are loaded separately

The manager keeps activity log loading separate from the main order detail payload.

This keeps the order-detail response focused while still making logs available in the UI.

### What the activity log shows

Each log entry currently shows:

- type icon
- date and time
- message
- expand/collapse control for long messages

### Log states in the UI

The current overlay handles:

- loading state
- error state
- empty state
- expanded/collapsed long messages

## Printing

The order detail view includes a print button.

When the print action is triggered, the manager temporarily loads a print stylesheet and then calls `window.print()`.

This is useful for printed order handling and operational workflows.

## How the UI gets its data

The order detail overlay combines:

- main order info from `OrderInfo/{orderId}`
- activity logs from `OrderLogs/{orderId}`
- manager status list/state already available in the manager app

This means the UI is composed from multiple pieces rather than one large all-in-one payload.

## Why this page matters for developers

For developers, the order detail view is useful because it gives a quick way to verify:

- customer and shipping data mapping
- provider assignment behavior
- custom data persistence
- totals and VAT behavior
- activity log output
- manager status-change flows

It is often the fastest way to validate whether a checkout or integration change is behaving correctly.

## Common pitfalls

### Expecting activity logs to be embedded in the main order detail payload

They are loaded through a separate endpoint.

### Forgetting manager store access restrictions

An order may exist, but the manager user can still be blocked from viewing it if they do not have access to the store.

### Assuming the overlay only shows standard fields

The view also renders extra customer, shipping, and provider custom data when present.

## Related pages

- [Manager Overview](manager-overview.md)
- [Activity Logs](activity-logs.md)
- [Order Lifecycle](order-lifecycle.md)
- [Checkout Flow](checkout-flow.md)
