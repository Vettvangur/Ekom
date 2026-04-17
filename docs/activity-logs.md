# Activity Logs

Activity logs provide a lightweight order history inside Ekom.

They are designed to give developers and backoffice users a clear audit trail of important order actions such as provider changes, status changes, checkout milestones, and custom integration events.

## What activity logs are

An activity log entry belongs to a specific order and contains:

- a message
- a timestamp
- a user/source name
- a log type

Activity logs are intended for order history and operational visibility.

They are not a replacement for application logging.

## Log types

Ekom currently supports three log types:

```csharp
OrderActivityLogType.Info
OrderActivityLogType.Success
OrderActivityLogType.Alert
```

### `Info`

Used for normal lifecycle updates.

Examples:

- provider selection
- order line added
- status change

### `Success`

Used for successful completion-style events.

Examples:

- `Order Completed.`
- `Order Completed. Offline payment.`

### `Alert`

Reserved for warning/error-style order history events.

This type is available for custom logs even if your current built-in flow mostly uses `Info` and `Success`.

## How activity logs are written

Activity logs are written through `IOrderActivityLogService`.

The most common developer-facing entry point is:

```csharp
await _order.AddActivityLogAsync(
    orderId,
    "ERP sync completed.",
    "BusinessCentral",
    OrderActivityLogType.Success,
    ct);
```

### Validation behavior

When adding a log:

- `orderId` must not be empty
- `message` must not be empty or whitespace
- the order must exist

If `userName` is omitted, the log service defaults it to `Customer`.

## Background batching behavior

Activity log writes are not inserted directly into SQL on every call.

Instead, Ekom queues them and persists them in background batches.

### Current dispatcher behavior

- bounded channel queue
- max queue size: `1000`
- max batch size: `50`
- flush interval: `500ms`
- full mode: `Wait`

### Why this matters

This avoids doing a SQL insert on hot paths such as:

- add to cart
- provider updates
- checkout updates

### Important behavior

Activity logs are eventually consistent.

That means a log may not appear immediately after the action that created it.

## Built-in activity logs currently written by Ekom

The current core flow writes activity logs for several built-in events.

## Order line added

When a new order line is created, Ekom writes:

- `Order line added. Product: {ProductTitle}`

Type:

- `Info`

## Shipping provider added

When the shipping provider changes, Ekom writes:

- `Shipping provider added. Provider: {ProviderTitle}`

Type:

- `Info`

## Payment provider added

When the payment provider changes, Ekom writes:

- `Payment provider added. Provider: {ProviderTitle}`

Type:

- `Info`

## Order status changed

When status is changed through the order flow, Ekom writes:

- `Order status changed. From: {OldStatus} To: {NewStatus}`

Type:

- `Info`

## Order completed

When checkout completion succeeds, Ekom writes one of:

- `Order Completed.`
- `Order Completed. Offline payment.`

Type:

- `Success`

## Writing custom activity logs

The simplest way to add your own order log entries is through `API.Order.AddActivityLogAsync(...)`.

### Example: integration success

```csharp
await _order.AddActivityLogAsync(
    orderId,
    "ERP sync completed.",
    "BusinessCentral",
    OrderActivityLogType.Success,
    ct);
```

### Example: integration warning

```csharp
await _order.AddActivityLogAsync(
    orderId,
    "ERP sync delayed. Will retry in background.",
    "BusinessCentral",
    OrderActivityLogType.Alert,
    ct);
```

### Example: informational audit event

```csharp
await _order.AddActivityLogAsync(
    orderId,
    "Packing slip generated.",
    "WarehouseService",
    OrderActivityLogType.Info,
    ct);
```

## Reading activity logs

The current core read path used by the manager UI goes through:

- `IOrderActivityLogService.GetOrderLogsAsync(...)`

The manager endpoint is:

- `GET /ekom/manager/OrderLogs/{orderId}`

This endpoint validates store access before returning the order logs.

## Manager UI behavior

The order manager UI displays activity logs in the order detail overlay.

Each entry shows:

- date and time
- message
- log type icon

Long messages are collapsed and can be expanded.

### Current icon meaning

- `Info` → information-style icon
- `Success` → success/check icon
- `Alert` → alert/warning icon

## When to use activity logs

Use activity logs when you want order-specific history that is useful to:

- backoffice users
- integrations
- support/debugging workflows
- audit-style order tracing

Good examples include:

- external integration milestones
- fraud or validation warnings
- manual operational actions
- shipment lifecycle steps

## When not to use activity logs

Do not use activity logs as a replacement for:

- application logs
- exception logs
- infrastructure monitoring
- high-volume diagnostic tracing

If something is only relevant to developers operating the application and not to the order history itself, it usually belongs in normal application logging instead.

## Common pitfalls

### Expecting logs immediately after write

Logs are queued and batched in the background.

### Writing logs for non-existent orders

`API.Order.AddActivityLogAsync(...)` validates that the order exists.

### Using activity logs for technical noise

Keep order activity logs meaningful for order history, not full debug traces.

### Forgetting log type

Choose the right type when writing custom logs:

- `Info` for normal lifecycle/audit events
- `Success` for successful milestones
- `Alert` for warning/error-like order history entries

## Related pages

- [API.Order Reference](api-order-reference.md)
- [Order Lifecycle](order-lifecycle.md)
- [Checkout Flow Overview](checkout-flow-overview.md)
- [Order Endpoints](order-endpoints.md)
