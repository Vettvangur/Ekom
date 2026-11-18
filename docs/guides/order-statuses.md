[Ekom docs](../README.md) / [Guides](README.md) / Order, payment, and fulfillment statuses

# Order, payment, and fulfillment statuses

Every Ekom order has three separate statuses. Each one answers one question:

- **Order status:** is the order still being worked on?
- **Payment status:** has it been paid?
- **Fulfillment status:** have its items been fulfilled?

Read the three together. For example, `Open / Paid / Packed` means the payment is complete and the items are packed, but they have not been handed over yet.

This page explains each status, how to change them in the manager of Umbraco 17, 18 and 19, how orders close automatically, and how to read and change the statuses from code. It also covers the older single status that Ekom keeps for compatibility, and how existing orders move from it to the three statuses.

On this page:

- [Order status](#order-status)
- [Payment status](#payment-status)
- [Fulfillment status](#fulfillment-status)
- [Examples](#examples)
- [Change statuses in the manager](#change-statuses-in-the-manager)
- [Automatic closing and reopening](#automatic-closing-and-reopening)
- [Find orders by status in the manager](#find-orders-by-status-in-the-manager)
- [Read and change statuses from code](#read-and-change-statuses-from-code)
- [The legacy status and migration](#the-legacy-status-and-migration)

## Order status

| Status | Meaning |
| --- | --- |
| `Draft` | An unfinished basket or checkout. It is not an order to work on yet. A checkout that failed or is waiting for payment can stay a Draft. Drafts are not in the default order list. |
| `Open` | An order that needs work. The payment can still be Pending or already Paid. Use the payment and fulfillment statuses to see what is left. |
| `Closed` | All work is done. A normal close needs Paid and Fulfilled. Reopen the order when more work is needed. Closed does not mean Cancelled. |
| `Cancelled` | The order is no longer being processed. It keeps its payment and fulfillment statuses. Cancelling does not void a payment or return money. |

Checkout and the order APIs move an accepted order from Draft to Open when its legacy status moves past the basket stage. The manager has no action that moves an order from Draft to Open, or from Cancelled to Open. Do not mark an unfinished checkout as Paid to make it show in **All orders**.

## Payment status

Ekom calculates the payment status from the payments recorded on the order. Payment providers record what happened (requested, authorized, captured, failed, cancelled, refunded), and Ekom works out the status. See [Payments](payments.md).

A manager user can also set the payment status by hand. Ekom records that as a manual entry in the order's payment history, so it replaces what was recorded before it. A manual change never contacts the payment provider.

| Status | Meaning |
| --- | --- |
| `Pending` | The payment is not recorded as complete or authorized. An accepted order can be Open / Pending. |
| `Authorized` | The money is authorized, but the capture is not recorded. Authorized is not Paid. |
| `Paid` | The payment counts as complete for the store's payment process. Together with Fulfilled, it can close the order automatically. |
| `PartiallyPaid` | Only part of the amount counts as paid. Shown as **Partially Paid**. |
| `Failed` | A payment attempt failed. Look at the provider and the checkout history before you decide what to do. |
| `Voided` | The authorization was voided. Choosing it by hand only records the status. It does not void anything at the provider. |
| `PartiallyRefunded`, `Refunded` | Part or all of the captured amount was returned. Ekom sets these when a provider records a refund, for example one made in the gateway's own portal. The manager cannot make refunds or set these statuses. |

## Fulfillment status

| Status | Meaning |
| --- | --- |
| `Unfulfilled` | No fulfillment is recorded. The payment can already be Paid. |
| `Picking` | The warehouse is picking the items. |
| `Picked` | The items are picked but not recorded as packed. |
| `Packed` | The items are packed and ready to send or collect. They have not been handed over. |
| `PartiallyFulfilled` | Some items are fulfilled, not all. Shown as **Partially Fulfilled**. |
| `Fulfilled` | All items are fulfilled, for example sent or handed to the customer. It does not mean the carrier confirmed delivery. |

There is no "ready for dispatch" fulfillment status. Use Packed for a packed order that is waiting to be handed over.

While the order is Open, the manager lets you correct the fulfillment status in either direction, for example from Packed back to Unfulfilled. A manual correction removes any recorded fulfilled quantities that no longer fit the new status.

Ekom does not split fulfillment across warehouses, and does not create separate shipping labels per group of items.

## Examples

| Situation | Order / Payment / Fulfillment |
| --- | --- |
| The customer has not finished checkout | Draft / Pending / Unfulfilled |
| An accepted invoice order waiting for payment | Open / Pending / Unfulfilled |
| A paid order waiting to be picked | Open / Paid / Unfulfilled |
| A paid order being packed | Open / Paid / Picked |
| Ready for the customer to collect | Open / Paid / Packed |
| All items handed over, payment still outstanding | Open / Pending / Fulfilled |
| Paid and fully fulfilled, with automatic closing on | Closed / Paid / Fulfilled |
| Cancelled before payment or fulfillment | Cancelled / Pending / Unfulfilled |

When you cancel an order that is paid or partly fulfilled, those statuses stay. Check the real payment and warehouse work separately.

## Change statuses in the manager

1. Open **Orders** and choose the store and currency.
2. On an Open order, use the **Payment** or **Fulfillment** menu in the table to change the status at once. Press Escape or click outside to close the menu.
3. In the order details, the **Order**, **Payment** and **Fulfillment** menus save as soon as you choose an option. There is no separate Save button.
4. Use the order controls to **Close**, **Cancel** or **Reopen** the order. To correct the payment or fulfillment of a Closed order, reopen it first.
5. Check the saved statuses and the activity log. If someone else changed the order in the meantime, reload it before you try again.

Every change checks that you have access to the order's store, that the order has not changed since you loaded it, and that no checkout or payment is holding the order. Choosing a different status does not get around a lock, or around an order whose history needs review.

### Trigger status events

Each status change can raise Ekom's status change events: `OrderStatusChangedAsync`, `PaymentStatusChangedAsync` and `FulfillmentStatusChangedAsync`, one for each status that changed. Integrations subscribe to these, for example to send an email when an order is fulfilled. See [Events](../reference/events.md).

The **Trigger status events** checkbox decides whether a change raises them. Leave it on when your integrations should hear about the change. Turn it off for a correction that should not repeat their work.

- The status is saved and logged either way.
- Status persistence handlers (`IOrderStatusPersistenceHandler`) still run when the change alters the legacy status. The checkbox does not turn them off.
- By default the checkbox is on for changes in the table and off in the order details. `Ekom:Manager:OrderStatusFireEventsByDefault` sets one starting value for both. You can still change the checkbox for each change. See the [configuration reference](../reference/configuration-reference.md#manager-order-status-events).

A manual status change does not charge, capture, void or refund money. It does not change payment amounts or the paid date, and it does not do any warehouse or shipping work. Handlers you have configured may do work of their own. When the real payment, shipping or reservation work has to happen, use the operation for that work.

## Automatic closing and reopening

Automatic closing is on by default. An Open order becomes Closed as soon as its payment is Paid and its fulfillment is Fulfilled. Picking, Picked, Packed and PartiallyFulfilled do not count. The manual **Close** action also needs Paid and Fulfilled.

```json
{
  "Ekom": {
    "AutoClosePaidAndFulfilledOrders": true,
    "Stores": {
      "Wholesale": {
        "AutoClosePaidAndFulfilledOrders": false
      }
    }
  }
}
```

Replace `Wholesale` with your store alias. The store setting wins over the global setting. When neither is set, automatic closing is on.

**Reopen** keeps the payment and fulfillment statuses, and turns off automatic closing for that order, so it does not close again straight away. Close it yourself when the work is done and it is Paid / Fulfilled. Cancelled orders never close automatically, even when they are Paid and Fulfilled.

## Find orders by status in the manager

- **All orders** shows Open, Closed and Cancelled orders. Drafts appear in the **Drafts** view, or when you choose **All** or **Draft** in the order status filter. Filtering on Pending payment or Unfulfilled does not bring in drafts.
- Counts, totals and exports follow the active filter, including store and currency. An export contains every matching order, not only the current page.
- Customer reports count Open and Closed orders, whatever their payment status. They leave out Draft and Cancelled orders unless you filter for them.
- **Needs review** lists orders with an open [order review](order-reviews.md). It is a view, not an order status, and it is hidden when no order needs review. The `RequiresReview` flag from the migration below is a different thing and does not create an order review.

## Read and change statuses from code

The three statuses are in `OrderStatusSnapshot` in the `Ekom.Models.Orders` namespace. Read it from `OrderInfo.Status` or `OrderData.Status`. `IOrderInfo` does not have it, so cast to `OrderInfo`.

```csharp
using Ekom.Models;
using Ekom.Models.Orders;

IOrderInfo? order = await orderApi.GetOrderAsync(orderId, ct);

if (order is OrderInfo { Status: var status }
    && status.PaymentStatus == PaymentStatus.Paid
    && status.FulfillmentStatus == FulfillmentStatus.Unfulfilled)
{
    // Paid and waiting to be picked.
}
```

`Ekom.Models.Orders.OrderStatus` (Draft, Open, Closed, Cancelled) and the legacy `Ekom.Utilities.OrderStatus` have the same name. When a file uses both namespaces, write the full name or use an alias.

`OrderStatusSnapshot` has:

| Property | What it holds |
| --- | --- |
| `OrderStatus`, `PaymentStatus`, `FulfillmentStatus` | The three statuses. `null` when the order's history does not say. |
| `RequiresReview` | `true` when the history is missing or contradicts itself. It is not a status. |
| `LegacyStatus` | The legacy status, as text. |
| `Revision` | A number that goes up every time Ekom saves the order or changes a status. You pass it back when you change a status. |
| `AutoCloseSuppressed` | `true` after Reopen, when automatic closing is off for this order. |
| `FulfilledQuantities` | The fulfilled quantity per order line key, when quantities were recorded. |

In JSON the statuses are written as names, such as `PartiallyPaid`.

### Change a status

Use `IOrderStateService.ChangeAsync` in C# (inject `Ekom.Services.IOrderStateService`; the example calls it `orderStates`), or `POST /ekom/manager/orderStates/{orderId}` over HTTP. Both check that the current backoffice user has manager access to the order's store. Code that runs without a signed-in backoffice user, such as a background job, is refused with `403`.

For that code, use `IOrderStateService.ChangeAsSystemAsync`. It takes the same actions and follows the same rules, but checks no access, so call it only from trusted server code:

```csharp
OrderStatusSnapshot updated = await orderStates.ChangeAsSystemAsync(
    orderId,
    new OrderSystemStateChange("Fulfill", Actor: "Warehouse sync"),
    ct);
```

`Actor` is required and is saved as the user name on the activity log entry. `ExpectedRevision` is optional here: leave it out to apply the change to the order as it is now. Status change events are raised unless you pass `Notify: false`. See [Use Ekom outside a web request](../examples/background-jobs.md#mark-an-order-as-shipped-from-a-queue-message) for a complete handler.

```json
{
  "expectedRevision": 3,
  "action": "PaymentPaid",
  "notify": false
}
```

```csharp
OrderStatusSnapshot updated = await orderStates.ChangeAsync(
    orderId,
    new OrderStateChangeRequest(ExpectedRevision: 3, Action: "Packed", Notify: true),
    ct);
```

- `expectedRevision` is the order's current `Revision`. Read it from the order. Do not guess it.
- `notify` is the **Trigger status events** checkbox. The default is `false`.
- Actions: `PaymentPending`, `PaymentAuthorized`, `PaymentPaid`, `PaymentPartiallyPaid`, `PaymentFailed`, `PaymentVoided`, `Unfulfilled`, `Picking`, `Picked`, `Packed`, `PartiallyFulfilled`, `Fulfill`, `Close`, `Cancel` and `Reopen`.
- `Reopen` needs a Closed order. Every other action needs an Open order.

`Fulfill` takes an optional `fulfilledQuantities`: a map from order line key to the total quantity fulfilled so far. Leave it out to fulfill every line in full. Each line must belong to the order, a quantity cannot be more than the ordered quantity, and it cannot go down. Ekom works out Fulfilled, PartiallyFulfilled or Unfulfilled from the quantities. The `PartiallyFulfilled` action only sets the status. It does not record shipments or line quantities.

Every action writes the change to the activity log and returns the updated snapshot. These are the errors. The HTTP endpoint returns the status code. In C#, `ChangeAsync` throws `EkomHttpException` with that status code, or `CheckoutConflictException` when a checkout or payment is holding the order.

| Status code | When |
| --- | --- |
| 400 | The action is unknown, the order is not in the status the action needs, Close without Paid and Fulfilled, or invalid quantities. |
| 403 | No manager access to the order's store. |
| 404 | No order with that ID. |
| 409 | The revision is out of date, a checkout or payment is holding the order, or the order's history needs review. |

After a `409`, reload the order and deal with the cause. Do not retry with a revision you made up.

## The legacy status and migration

Before the three statuses, Ekom had one status, `Ekom.Utilities.OrderStatus`. It is still available, together with the APIs that use it, such as `Order.UpdateStatusAsync`. When code changes the legacy status, Ekom translates the change to the three statuses. The legacy status cannot express every combination of the three, so prefer the three statuses in new code.

Orders saved before the three statuses existed keep their legacy status until they are migrated. Until then, Ekom works out their three statuses when it reads them. Reading does not save anything. An order is migrated the next time Ekom saves it, or when you run the migration below. An order whose history needs review is not migrated automatically.

### How legacy statuses map

This is the first mapping, before automatic closing is applied. A recorded paid date makes the payment Paid instead of Pending or Failed. `PaymentFailed` with a paid date is marked for review instead of being migrated.

| Legacy status | Order | Payment | Fulfillment | Notes |
| --- | --- | --- | --- | --- |
| `Incomplete` | Draft | Pending | Unfulfilled | Unfinished basket |
| `Wishlist` | Draft | Pending | Unfulfilled | Kept for compatibility |
| `WaitingForPayment` | Draft | Pending | Unfulfilled | Migrated only when no checkout or payment is in progress |
| `PaymentFailed` | Draft | Failed | Unfulfilled | A paid date means review |
| `Pending` | Open | Pending | Unfulfilled | Accepted order |
| `OfflinePayment` | Open | Paid | Unfulfilled | Includes older invoice orders |
| `ReadyForDispatch` | Open | Paid | Unfulfilled | Does not prove that anything was picked or packed |
| `ReadyForDispatchWhenStockArrives` | Open | Paid | Unfulfilled | There is no separate "waiting for stock" status |
| `ReadyForPickup` | Open | Paid | Packed | Waiting to be collected |
| `Dispatched` | Open | Paid | Fulfilled | Becomes Closed during migration when automatic closing is on |
| `Closed` | Closed | Paid | Fulfilled | |
| `Cancelled` | Cancelled | Pending | Unfulfilled | Payment and fulfillment already stored on the order are kept |
| `Returned` | Closed | Paid if there is a paid date, otherwise unknown | Fulfilled | Needs review. Not migrated automatically. A return does not mean a refund. |
| Unknown or invalid value | Unknown | Unknown | Unknown | Needs review. Not migrated automatically. |

When code sets the legacy status on an order that already has its three statuses, the payment column above applies only to the legacy statuses that mean "paid": `OfflinePayment`, `ReadyForDispatch` and `ReadyForDispatchWhenStockArrives`. Setting `Dispatched` or `ReadyForPickup` changes the fulfillment status and leaves the payment status as it is, so an unpaid order is not marked Paid and is not closed automatically.

`OfflinePayment` maps to Paid, including older invoice orders. That is a migration rule, not proof that the invoice was paid. For a new accepted order that is still waiting for payment, use Open / Pending until the store's payment process says otherwise.

For Cancelled orders, valid payment and fulfillment statuses and quantities already stored on the order are kept. Invalid values, quantities that do not fit, or a paid date that contradicts a stored payment status that is not Paid, mark the order for review. An order that a checkout is holding, or that has an unfinished payment attempt, is not migrated, whatever its legacy status.

### Migrate existing orders

1. Back up the database and rehearse on a copy first.
2. Preview with `GET /ekom/manager/statusMigration?afterReferenceId=0`, signed in to the backoffice. For each order, check `eligible`, `status.requiresReview` and the three statuses.
3. Apply the page you checked with `POST /ekom/manager/statusMigration?afterReferenceId=0`. Applying checks each order again. It does not reuse the preview.
4. While `hasMore` is `true`, repeat with `afterReferenceId` set to the `nextReferenceId` of the last response. A page holds at most 100 orders, from the stores you have access to. `migrated` tells you how many orders the page changed.
5. Orders that were skipped stay unmigrated. When you have fixed their history or their payment state, run the migration again from an earlier cursor. Starting again at `0` is safe, because migrated orders are left out.
6. Check counts, a sample of mapped orders and the automatic closing before you migrate the production database.

The migration keeps the legacy status and the original status, the stored amounts, dates, order data and history. It does not run payment, stock, email or status handlers again. The preview shows the result after automatic closing. The migration has been rehearsed on SQLite but not on SQL Server.

## Related pages

- [Manager orders](../manager/orders.md): the order list and order details where staff change statuses.
- [Payments](payments.md): how recorded payments set the payment status.
- [Order reviews](order-reviews.md): flag an order that a person must look at.
- [Events](../reference/events.md): the status change events and their arguments.
- [Configuration reference](../reference/configuration-reference.md): automatic closing and the manager event default.

[Back to Guides](README.md)
