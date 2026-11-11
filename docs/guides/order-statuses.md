# Order, payment, and fulfillment statuses

Ekom 1.0 separates three questions: **Is the order still being worked on? Has it been paid? Have its items been fulfilled?** Read the three statuses together. For example, `Open / Paid / Packed` means payment is recorded as complete, the items are packed, and fulfillment is not yet complete.

This guide describes the status model and the new manager in Umbraco 17 and 18. The legacy enum and APIs remain available for compatibility; the Umbraco 13 manager retains its legacy interface.

## Order status

| Status | Meaning and use |
| --- | --- |
| `Draft` | An unfinished basket or checkout, not yet an operational order. A failed or waiting checkout can remain Draft. Excluded from the default order list. |
| `Open` | An order that needs operational work. It may still be Pending payment or already Paid. Use the separate payment and fulfillment statuses to decide what remains. |
| `Closed` | Operational work is complete. Normal closing requires Paid and Fulfilled. Reopen explicitly when more work is needed. Closed does not mean Cancelled. |
| `Cancelled` | The order is no longer being processed. Cancellation retains payment and fulfillment history; it does not mean a payment was voided or money was returned. |

Checkout and existing order APIs move accepted orders from Draft to Open as their legacy status advances. The new manager status-action endpoint does not offer a generic Draft-to-Open or Cancelled-to-Open action. Do not mark an unfinished checkout Paid just to make it appear in All orders.

## Payment status

These values describe the recorded payment classification. Manually changing a status does not perform a payment-provider operation.

| Status | Meaning and use |
| --- | --- |
| `Pending` | Payment is not yet recorded as complete or authorized. An accepted order can be Open / Pending. |
| `Authorized` | Funds are authorized, but capture is not recorded as complete. Authorization is not the same as Paid. |
| `Paid` | Payment is classified as complete under the store's payment process. Together with Fulfilled, this can close the order automatically. |
| `PartiallyPaid` | Only part of the amount is classified as paid. Displayed as **Partially Paid**. |
| `Failed` | A payment attempt failed. Check the provider and checkout history before deciding what to do next. |
| `Voided` | A payment authorization has been voided. Selecting this manually records the classification; it does not void an authorization at the provider. |
| `PartiallyRefunded`, `Refunded` | Reserved model values for partial/full refund classification. Refund execution and refund status editing are not available in this manager release. |

For legacy compatibility, `OfflinePayment` maps to Paid, including historical invoice-based orders. This migration convention is not proof that an invoice has been collected. For new accepted orders still awaiting payment, use Open / Pending until the store's payment process determines otherwise.

## Fulfillment status

| Status | Meaning and use |
| --- | --- |
| `Unfulfilled` | No fulfillment is recorded. Payment can already be Paid. |
| `Picking` | Warehouse picking is in progress. |
| `Picked` | Items have been picked but are not yet recorded as packed. |
| `Packed` | Items are packed and ready for dispatch or pickup. This does not mean they have been handed over. |
| `PartiallyFulfilled` | Some items have been fulfilled, but the order is not fully fulfilled. Displayed as **Partially Fulfilled**. |
| `Fulfilled` | All items have been fulfilled, for example dispatched or handed to the customer. This describes fulfillment, not carrier-confirmed delivery. |

There is no new `ReadyForDispatch` status. Use Packed for packed orders awaiting handover. Legacy ReadyForDispatch maps to Unfulfilled because that historical status alone does not establish that picking or packing happened. Legacy ReadyForPickup maps to Packed.

The manager allows corrections in either direction while the order is Open, including Packed back to Unfulfilled. A manual summary correction clears incompatible previously recorded fulfillment quantities. Multi-warehouse split fulfillment and separate shipment labels per group are not included.

## Everyday examples

| Situation | Order / Payment / Fulfillment |
| --- | --- |
| Customer has not finished checkout | Draft / Pending / Unfulfilled |
| Accepted invoice order awaiting payment | Open / Pending / Unfulfilled |
| Paid order waiting to be picked | Open / Paid / Unfulfilled |
| Paid order being packed | Open / Paid / Picked |
| Ready for customer pickup | Open / Paid / Packed |
| All items handed over, payment still outstanding | Open / Pending / Fulfilled |
| Paid and fully fulfilled, with automatic closing enabled | Closed / Paid / Fulfilled |
| Cancelled before payment or fulfillment | Cancelled / Pending / Unfulfilled |

Cancelling a paid or partly fulfilled order preserves those dimensions. Review the actual payment and warehouse work separately.

## Change statuses in the manager

1. Open Orders and select the correct store and currency.
2. On an Open order, use the Payment or Fulfillment dropdown in the table for an immediate update. Escape or clicking outside dismisses the menu.
3. In order details, payment selection saves immediately; choose the fulfillment action and use Save for the pending status edit. Save pending edits before changing payment or navigating to another order.
4. Use the order controls to Close, Cancel, or explicitly Reopen a Closed order. Reopen first before correcting payment or fulfillment on a Closed order.
5. Check the saved statuses and activity log. If another user or polling has exposed a newer revision, reload before retrying.

Updates enforce store permissions, status revisions, and checkout/payment locks. A lock or unresolved historical conflict cannot be bypassed by choosing a different status.

### Trigger status events

The **Trigger status events** checkbox controls optional legacy status-change notifications. Use it when configured integrations should receive the change. Turn it off for a manual correction that should not repeat those notifications.

The status is still saved and logged with either choice. Persistence handlers still run when the projected legacy status changes; the checkbox does not disable them. Notifications are also conditional on that legacy value changing, so not every independent payment or fulfillment edit emits a legacy status event.

By default, table updates enable notifications and detail updates leave them unchecked. `Ekom:Manager:OrderStatusFireEventsByDefault` sets a common default; the editor can change the checkbox for an individual update. See [configuration](../reference/configuration-reference.md#manager-order-status-events).

Manual status edits do not charge, capture, void, or refund money, rewrite payment amounts or paid dates, or themselves execute warehouse/shipping work. Separately configured handlers may perform application-specific actions. Use the actual payment, shipping, or reservation operation when that work is required.

## Automatic closing and reopening

Automatic closing defaults to enabled. An Open order becomes Closed when Payment is Paid and Fulfillment is Fulfilled. Picking, Picked, Packed, and PartiallyFulfilled do not qualify. A manual Close action also requires Paid and Fulfilled.

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

Replace `Wholesale` with the store alias. The store setting overrides the global setting; absent both, the default is true.

Explicit Reopen preserves payment and fulfillment and suppresses automatic closing for that order, so it does not immediately close again. Close it explicitly once work is complete and it is Paid / Fulfilled. Cancelled orders are not automatically closed, even when both other dimensions indicate completion.

## Find orders and understand totals

- **All** includes Open, Closed, and Cancelled orders. Drafts appear only when explicitly selected in the Order status filter, even if you filter Pending payment or Unfulfilled fulfillment.
- Counts, totals, and exports follow the active filter scope, including store and currency. Exports include all matching results, not only the current page.
- Customer reporting includes qualifying Open and Closed orders even with Pending payment. It excludes Draft orders by default.
- **Needs review** is an order-alert view, not another OrderStatus. It is hidden when there are no reviews to look at. Migration `RequiresReview` is a separate historical-evidence flag; it does not itself create an order-review alert.

## Legacy migration mapping

The following table shows the initial mapping before configured automatic closing. A recorded paid date takes precedence over Pending/Failed defaults; PaymentFailed with a paid date is flagged for review rather than silently migrated.

| Legacy status | Order | Payment | Fulfillment | Notes |
| --- | --- | --- | --- | --- |
| `Incomplete` | Draft | Pending | Unfulfilled | Unfinished basket |
| `Wishlist` | Draft | Pending | Unfulfilled | Retained compatibility mapping |
| `WaitingForPayment` | Draft | Pending | Unfulfilled | Eligible only when no active checkout/payment prevents migration |
| `PaymentFailed` | Draft | Failed | Unfulfilled | Paid-date conflict requires review |
| `Pending` | Open | Pending | Unfulfilled | Accepted order |
| `OfflinePayment` | Open | Paid | Unfulfilled | Includes historical invoice-based orders |
| `ReadyForDispatch` | Open | Paid | Unfulfilled | Does not prove fulfillment |
| `ReadyForDispatchWhenStockArrives` | Open | Paid | Unfulfilled | No separate new waiting-for-stock status |
| `ReadyForPickup` | Open | Paid | Packed | Awaiting pickup |
| `Dispatched` | Open | Paid | Fulfilled | Becomes Closed during migration when automatic closing is enabled |
| `Closed` | Closed | Paid | Fulfilled | Explicit agreed legacy mapping |
| `Cancelled` | Cancelled | Pending | Unfulfilled | Preserve recorded payment/fulfillment evidence; conflicts require review |
| `Returned` | Closed | Paid if a paid date exists; otherwise unknown | Fulfilled | Requires review; not automatically migrated and does not imply a refund |
| Unknown/invalid value | Unknown | Unknown | Unknown | Requires review; not automatically migrated |

For Cancelled orders, valid stored payment and fulfillment statuses and quantities are preserved. Invalid values, inconsistent quantities, or a paid date contradicting a recorded non-Paid payment status block migration. Active checkout ownership and unsettled payment attempts also block migration, independently of the legacy status name.

### Rollout procedure

1. Back up the database and rehearse on a copy. Reading a projected status does not itself backfill the order.
2. Preview with `GET /ekom/manager/statusMigration?afterReferenceId=0` using an authorized backoffice session. Inspect `Eligible`, `Status.RequiresReview`, and the resulting three statuses.
3. Apply the reviewed page with `POST /ekom/manager/statusMigration?afterReferenceId=0`. Apply rechecks eligibility; it does not blindly apply a previous preview.
4. Continue using each response's `NextReferenceId` while `HasMore` is true. Pages contain at most 100 orders accessible to the current user.
5. Revisit skipped records from an earlier cursor after resolving their evidence or payment state. Starting again at zero is safe: already migrated orders are excluded.
6. Verify counts, sample mappings, and automatic-close behavior before enabling the rollout for the production database.

Migration preserves the legacy value and original status, stored amounts, dates, payloads, and evidence. It does not replay payment, stock, email, or status handlers. Preview includes the configured automatic-close result. SQLite rehearsals are documented; SQL Server rehearsal has not been performed. See [rehearsal evidence](../plans/status-migration-rehearsal.md).

## Developer usage

Read `OrderData.Status` or concrete `OrderInfo.Status` for the independent `OrderStatusSnapshot` in `Ekom.Models.Orders`. JSON uses enum names such as `PartiallyPaid`. Null dimensions plus `RequiresReview` mean insufficient or conflicting historical evidence, not an additional status. Existing `Ekom.Utilities.OrderStatus` and legacy APIs remain available; legacy changes are projected into the canonical state and cannot represent every new dimension independently.

Use `IOrderStateService.ChangeAsync` or the authenticated `POST /ekom/manager/orderStates/{orderId}` endpoint to change statuses with the current snapshot revision:

```json
{
  "ExpectedRevision": 3,
  "Action": "PaymentPaid",
  "Notify": false
}
```

Use the order's actual revision instead of the example value. Supported actions are `PaymentPending`, `PaymentAuthorized`, `PaymentPaid`, `PaymentPartiallyPaid`, `PaymentFailed`, `PaymentVoided`, `Unfulfilled`, `Picking`, `Picked`, `Packed`, `PartiallyFulfilled`, `Fulfill`, `Close`, `Cancel`, and `Reopen`. All except Reopen require Open; Reopen requires Closed.

`Fulfill` optionally accepts `FulfilledQuantities`, a dictionary of order-line GUIDs to cumulative quantities. Omit it to fulfill every line. Quantities must belong to the order, cannot exceed ordered quantities, and cannot decrease within this action. The resulting summary is calculated from the quantities. A manual `PartiallyFulfilled` action only sets the summary; it does not allocate shipments or invent line quantities.

Status actions log changes and return the updated snapshot. A stale revision or protected checkout produces a conflict response. Reload and resolve the cause rather than retrying with an invented revision.
