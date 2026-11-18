[Ekom docs](../README.md) / [Manager](README.md) / Activity logs

# Activity logs

An activity log is the history of one order: status changes, payments, shipments, reviews, reservation releases and other events that matter when someone looks at the order. Staff read it in Ekom Manager, and developers can add their own entries from code. It is not a replacement for application logs, error monitoring or detailed diagnostics.

**Who this page is for:** staff who read an order's history, and developers who write entries. [Read the log](#read-the-log) is for staff; the rest is for developers.

On this page:

- [Read the log](#read-the-log)
- [Entries Ekom writes](#entries-ekom-writes)
- [Add an entry from code](#add-an-entry-from-code)
- [When an entry is saved](#when-an-entry-is-saved)
- [Read entries from code](#read-entries-from-code)

## Read the log

Open an order in **Orders** and choose the **Activity log** tab. The entries are listed newest first, each with:

- **Date**: when the entry was written,
- **Type**: **Info**, **Success** or **Alert**,
- **Message**: what happened. Long messages are cut short; click **Show more** to read the whole message and **Show less** to fold it again.

An order without entries shows **No activity yet.** If the log cannot be loaded, the tab says **Unable to load activity log.**

Each entry also stores the name of the user or source that wrote it. The Manager does not show that name in the list, but it is part of the entry and is returned to code that reads the log.

You see the log only for orders in stores you have access to.

## Entries Ekom writes

Ekom writes entries for, among others:

- status changes made in the Manager, for example `PaymentPaid: Open/Pending/Unfulfilled -> Open/Paid/Unfulfilled`, with ". Closed automatically." added when the change closed the order,
- changes to order lines and a completed checkout,
- every payment outcome recorded on the order: requested, authorized, successful, failed, cancelled, voided, refunded, or a note. The message starts with the outcome, for example `Payment successful. Order number: ...`, and ends with the payment ID,
- shipment events and tracking number changes,
- order review notes and resolutions,
- expired stock reservations and manual reservation releases,
- selected GA4 and Meta tracking outcomes, when tracking is set up.

Which messages an order gets depends on the workflows and integrations the site uses.

### Payment cancellation and reservation release

When a payment is cancelled and its reservations are released, Ekom writes one entry before it releases anything, then updates that same entry with the outcome:

- On success the entry reads **Payment cancelled. Payment reservations released.**, with the attempt ID and the reason. It is saved together with the order reset.
- When the release came from the payment return URL, the entry also shows how the request arrived: `afterSubmit` is the time since the customer was sent to pay, `via` is `POST` for a provider form post and `GET` for a plain browser request, and `referer` is the host the browser came from, or `none`. The provider's `status`, `message`, `referenceNumber` and `transactionid` follow when it sent them. The return URL is not signed, so a `GET` with no provider referer can be the browser re-visiting the URL, not the customer pressing cancel. Treat these values as evidence, not proof.
- On a failed or uncertain release, the entry becomes an **Alert** with the attempt ID, the reason, the type of failure and what to check. Separate reconciliation warnings stay visible when someone must act.
- If the process stops, or the outcome cannot be saved, the entry keeps saying the release is in progress and its completion is not confirmed. It never claims success it cannot confirm.

If the first entry cannot be saved, nothing is released. A repeated callback for an attempt that is already released does not add another entry. Progress for each request and reservation goes to the diagnostic logs, not the activity log.

## Add an entry from code

Use `AddActivityLogAsync` on the order API.

```csharp
using Ekom.API;
using Ekom.Models;

await Order.Instance.AddActivityLogAsync(
    orderId,
    "Shipment booked with the warehouse. Pallet 42.",
    userName: "warehouse-integration",
    logType: OrderActivityLogType.Success,
    ct: cancellationToken);
```

| Parameter | Default | Notes |
| --- | --- | --- |
| `orderId` | Required | Must not be `Guid.Empty`, or `ArgumentException` is thrown. |
| `message` | Required | Must not be null, empty or whitespace, or `ArgumentException` is thrown. |
| `userName` | `null` | Stored trimmed. When null, empty or whitespace, Ekom stores `Customer`. |
| `logType` | `OrderActivityLogType.Info` | `Info`, `Success` or `Alert`. |
| `ct` | `default` | Cancellation token. |

Ekom loads the order first. If no order has that ID, it throws `OrderInfoNotFoundException` and writes nothing.

Use the types the same way Ekom does:

- `Info` for ordinary events and audit notes,
- `Success` for a finished step,
- `Alert` for a warning or failure on this order that a person should notice.

Do not put secrets or more personal data than needed in a message. Entries are kept with the order and every user with access to its store can read them.

Custom Manager actions and other integrations write entries the same way. See [Add your own order actions](orders.md#add-your-own-order-actions).

## When an entry is saved

`AddActivityLogAsync` checks the order and then puts the entry in a queue. A background job saves queued entries in batches of up to 50, about half a second after the first entry arrives. So the entry is not always visible in the Manager the moment the call returns.

The queue lives in memory and holds up to 1,000 entries. When it is full, the call waits until there is room. Entries still in the queue are lost if the application stops or crashes before they are saved, and a batch that fails to save is logged as an error and not retried. If your integration needs a guaranteed record, keep it in your own storage and use the activity log as the record that staff read.

Ekom's own entries for Manager status changes, payments and reservation releases are written directly with the change they describe, not through the queue.

## Read entries from code

The Manager reads the log from `GET /ekom/manager/OrderLogs/{orderId}`. It requires a backoffice user with access to the order's store, and is not meant as a public order-history API.

In your own services, inject `IOrderActivityLogService` and call `GetOrderLogsAsync(orderId, ct)`. It returns the entries newest first, each with `Message`, `UserName`, `Date` and `LogType`, and an empty list for `Guid.Empty`. This is a lower-level service: it does not check that the order exists or that the caller may see it. Do those checks yourself.

## Related pages

- [Orders](orders.md): open an order and its activity log.
- [Access and permissions](access-and-permissions.md): who can read which orders.
- [Order reviews](../guides/order-reviews.md): flag an order for a person to look at.
- [.NET API](../reference/dotnet-api.md): the rest of the order API.

[Back to Manager](README.md)
