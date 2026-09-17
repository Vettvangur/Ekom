# Activity logs

Activity logs are order-specific operational history. Use them for information that is useful when reviewing an order, not as a replacement for application logs, exception monitoring, or high-volume diagnostics.

Each entry contains:

- the order ID;
- a message;
- a user or source name;
- the entry date;
- one of the `Info`, `Success`, or `Alert` log types.

The manager loads logs separately when an order is opened and returns them newest first. The server checks access to the order's store before returning the entries. An order without entries displays **No activity yet.**

The Umbraco 13 AngularJS manager visually distinguishes the three log types and can expand long messages. The shared Umbraco 17/18 order view currently renders the date and message. The full entry model still contains the user name and log type in all supported packages.

## Add an entry

Use the public order API:

```csharp
using Ekom.API;
using Ekom.Models;

await Order.Instance.AddActivityLogAsync(
    orderId,
    "Warehouse shipment created.",
    userName: "warehouse-service",
    logType: OrderActivityLogType.Success,
    ct: cancellationToken);
```

`AddActivityLogAsync` requires a non-empty order ID and a non-blank message. It loads the order first and throws `OrderInfoNotFoundException` if that order does not exist. If `userName` is null, empty, or whitespace, Ekom stores `Customer`; otherwise it stores the trimmed value. The default log type is `Info`.

Use the types consistently:

- `Info` for ordinary lifecycle or audit information.
- `Success` for a completed milestone.
- `Alert` for an order-specific warning or failure that an operator should notice.

Do not put secrets or unnecessary personal data in an activity message. Entries are persisted as part of the order's operational history and are visible to authorized backoffice users.

## Asynchronous persistence

The public API validates the order and then queues the write. A background dispatcher persists entries in batches, so the call completing does not guarantee that the manager can read the entry immediately.

The dispatcher uses a bounded in-memory queue. Application shutdown or a process failure before a queued entry is persisted can therefore lose that pending entry. If an integration needs a durable transaction or delivery guarantee, use its own persistence mechanism and treat the activity log as the operator-facing record.

## Built-in entries

Ekom writes activity entries from core order and checkout workflows, including status and order-line changes and successful checkout. Tracking integrations also write selected GA4 and Meta outcomes. The exact messages visible on an order depend on the workflows and integrations used by the application.

Custom manager actions and external integrations can add their own entries with `Order.Instance.AddActivityLogAsync`.

## Reading entries

The backoffice uses `GET /ekom/manager/OrderLogs/{orderId}`. It is an authenticated manager endpoint and is not intended as a public order-history API.

Application services that need the core read path can inject `IOrderActivityLogService` and call `GetOrderLogsAsync`. Unlike the public `Order.Instance.AddActivityLogAsync` method, direct use of the service is a lower-level API; callers are responsible for their own order and authorization checks.

## Related pages

- [Orders](orders.md)
- [Access and permissions](access-and-permissions.md)
