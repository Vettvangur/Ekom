# Upgrading payments

From this version Ekom owns the payment contracts and records every payment on its order. The Ekom.Payments provider packages build on Ekom instead of the other way round. This page covers what a site changes when it upgrades. The model itself is described in [Payments](../guides/payments.md).

Ekom and the Ekom.Payments provider packages must be upgraded together. A provider package built for Ekom.Payments 1.0 does not work with this version of Ekom, and the new provider packages do not work with an older Ekom.

## Packages

| Site takes | Install |
| --- | --- |
| Offline payments only | The Ekom package for your Umbraco version, for example `Ekom.U17` |
| One online gateway | Ekom plus that provider, for example `Ekom.Payments.Borgun` |
| Several gateways | Ekom plus each provider package |

Remove these package references. They no longer exist:

- `Ekom.Payments.U10`, `Ekom.Payments.U17`, `Ekom.Payments.U18`, `Ekom.Payments.U19`
- `Ekom.Payments.AspNetCore`

`Ekom.Payments.Core` remains as a small shared library. Provider packages bring it in; do not reference it directly unless you write a provider.

Ekom used to bring the Ekom.Payments packages in for you. It no longer references them, so a site that relied on that must reference its provider packages itself. A site that forgets has no online providers and checkout fails with "Base Payment Provider not found".

## What keeps working without changes

- Provider nodes in Umbraco, their `basePaymentProvider` names and their settings.
- Configuration under `Ekom:Payments`.
- Provider callback addresses (`/ekom/payments/*response`).
- Everything gateways receive, including the reference a payment was started with. A payment a customer started before the upgrade still completes when its callback arrives after it.
- Receipt-page query strings such as `?reference=` and `?siminnPayOrderKey=`.
- `CheckoutEvents.PayAsync` handlers that read or change `PaymentSettings`. The type kept its `Ekom.Payments` namespace.
- Legacy order statuses and the events raised when they change.

## Code that must change

| Before | Now |
| --- | --- |
| `Ekom.Payments.Events.SuccessAsync` / `ErrorAsync` to follow payments | `Ekom.Events.OrderEvents.PaymentRecordedAsync`. The old events are only raised by a provider that has not been converted. |
| A provider's own `Events.Success` / `Events.Error` (Borgun, Valitor, Netgiro, Straumur, AltaPay, PayTrail, Síminn Pay, Teya) | Obsolete and never raised. Use `PaymentRecordedAsync`. |
| `EkomPayments.GetOrder(request)` | `IOrderPayments.GetAsync(paymentId)` for the order's payment. `PaymentOrderRetriever.GetOrder` still reads the old rows. |
| `Ekom.Payments.OrderStatus` rows read through `IOrderService` | `OrderPayment` and its transactions through `IOrderPayments` |
| `ValitorResponseHelper` returning `OrderStatus` | Returns `OrderPayment` |
| `SiminnPayStatusView(OrderStatus, …)` | Built from `OrderPayment` |
| Teya `PollingRequest.OrderId` | `PollingRequest.PaymentId` |
| Completing an order from a payment success handler | Not needed. Recording a capture completes the order's checkout. |

### Custom payment providers

A provider written against Ekom.Payments 1.0 needs to be recompiled, and should be converted:

1. Replace `IOrderService.InsertAsync` with `IOrderPayments.CreateAsync`, and send `payment.PaymentId` where the old row's `UniqueId` was sent.
2. In the callback, find the payment with `IOrderPayments.GetAsync`, check `payment.BelongsTo(...)`, verify the response, then call `RecordAsync` with what the response means: `Captured`, `Authorized`, `Failed`, `Cancelled` or `Refunded`.
3. Remove the code that marked the row paid, stored `PaymentData` and raised the success event.
4. Log with `PaymentLog.BeginScope`.

The `AsynchronousExample` provider in the Ekom.Payments repository is the reference. Until a provider is converted it keeps working: its requests and captures reach Ekom through a bridge in `Ekom.Payments.Core`, and its orders complete. Its failures are not recorded, because an error raised by an unconverted provider may be an unverifiable callback.

## Behaviour that changed

- **Payment status is set by providers.** It used to change only through the legacy status or a manager user. Now a recorded failure makes it `Failed`, an authorization `Authorized`, a refund `PartiallyRefunded` or `Refunded`.
- **Unverifiable callbacks record nothing.** They are logged. They are not treated as failed payments.
- **Straumur reports more.** A declined payment is recorded as `Failed`, a refund made at Straumur as `Refunded`, and with delayed capture the order is `Authorized` until Straumur captures. Without delayed capture the authorization still counts as the payment.
- **AltaPay confirms the order's latest AltaPay attempt**, not its oldest.
- **Teya consumer loans** resumes monitoring after a restart for loans requested within the last week. A loan still undecided after its window gets a note on the payment asking staff to check it with Teya. The borrower's national id is no longer stored.
- **Manual payment status changes** are part of the payment history and are not undone by a late failure from an older attempt.

## Old payment data

After startup a background job copies the rows of `EkomPaymentOrders` and `EkomPayments` onto their orders:

- each old row becomes a payment with a `Requested` transaction, and a `Captured` transaction when it was paid, carrying the card number, authorization code, transaction ID and card type that were stored;
- only history is added. Order statuses, activity logs, events, emails and checkout are not touched;
- signatures, hashes and card tokens inside old stored responses are not copied;
- an old payment whose order is not in this database is counted, logged with its row number, and left where it is;
- progress is saved in `EkomOrderPaymentMigration` per batch, so the job continues after a restart and can run any number of times;
- the old tables are not changed or dropped. Drop them yourself once you no longer need them for a rollback.

Look for `Copied old payments onto their orders` in the application log for the totals, and for a warning listing payments without an order.

## Rolling back

The upgrade only adds tables: `EkomOrderPayments`, `EkomOrderPaymentTransactions`, `EkomOrderPaymentMigration`. The old payment tables are untouched, so the previous versions of Ekom and Ekom.Payments run against the same database. Payments recorded while the new version ran exist only in the new tables.
