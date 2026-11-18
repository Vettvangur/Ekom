# Upgrade payments

[Ekom docs](../README.md) / [Upgrading](README.md) / Upgrade payments

Ekom now owns the payment contracts and records every payment on its order. Previously the Ekom.Payments packages owned the payment model, and Ekom depended on them. Now each `Ekom.Payments.<Provider>` package builds on Ekom instead. This page covers what a site changes when it moves to this model. The model itself is described in [Payments](../guides/payments.md).

This change is not tied to an Umbraco version. Follow this page together with the Umbraco version page if you upgrade both at the same time.

On this page:

- [Who needs this page](#who-needs-this-page)
- [What changes](#what-changes)
- [Before you start](#before-you-start)
- [Upgrade the site](#upgrade-the-site)
- [What happens to old payment data](#what-happens-to-old-payment-data)
- [Convert a custom payment provider](#convert-a-custom-payment-provider)
- [Check that it worked](#check-that-it-worked)
- [Roll back](#roll-back)
- [What Ekom does not do for you](#what-ekom-does-not-do-for-you)

## Who needs this page

Your site needs this page if it references any `Ekom.Payments` package from version 1.0 or earlier, if it handles `Ekom.Payments.Events.SuccessAsync` or `ErrorAsync`, or if it has its own payment provider.

Ekom and the Ekom.Payments provider packages must be upgraded together. Ekom records payments on the order from version 1.2.0. The provider packages built for it are version 1.1.0, and they require Ekom 1.2.0 or later. Ekom.Payments packages 1.0 and earlier do not work with Ekom 1.2.0 or later, and the 1.1.0 provider packages do not work with an earlier Ekom.

## What changes

### Packages

| Site takes | Install |
| --- | --- |
| Offline payments only | The Ekom package for your Umbraco version, for example `Ekom.U17`. Nothing else. |
| One online gateway | Ekom plus that provider's package at version 1.1.0, for example `Ekom.Payments.Borgun`. |
| Several gateways | Ekom plus each provider package at version 1.1.0. |

These provider packages exist at version 1.1.0:

| Gateway | Package |
| --- | --- |
| Borgun (Teya cards) | `Ekom.Payments.Borgun` |
| Valitor | `Ekom.Payments.Valitor` |
| Netgiro | `Ekom.Payments.Netgiro` |
| Straumur | `Ekom.Payments.Straumur` |
| AltaPay | `Ekom.Payments.AltaPay` |
| PayTrail | `Ekom.Payments.PayTrail` |
| Siminn Pay | `Ekom.Payments.SiminnPay` |
| Teya Consumer Loans | `Ekom.Payments.TeyaConsumerloans` |

There are no separate packages per Umbraco version. One package per gateway is used with Ekom on Umbraco 13, 17, 18 and 19: the packages target .NET 8 and reference `Ekom.Core`, not an Umbraco package. The [Ekom.Payments repository](https://github.com/Vettvangur/Ekom.Payments) holds their source.

These packages no longer exist. Remove every reference to them:

- `Ekom.Payments.U10` (Umbraco 13), `Ekom.Payments.U17`, `Ekom.Payments.U18`, `Ekom.Payments.U19`
- `Ekom.Payments.AspNetCore`

`Ekom.Payments.Core` remains as a small shared library, also at version 1.1.0. Every provider package brings it in. Reference it directly only if your own provider uses it, as the `AsynchronousExample` provider does.

Previously Ekom brought the Ekom.Payments packages in for you. It no longer references them, so the site must reference its provider packages itself. A site without them has no online providers, and checkout fails with `PaymentProviderNotFoundException` and the message `Base Payment Provider not found. DLL possibly missing. Name: <name>`.

### What keeps working without changes

- Provider nodes in Umbraco, their `basePaymentProvider` names and their settings.
- Configuration under `Ekom:Payments`: `SendEmailAlerts`, and provider settings kept under `Ekom:Payments:<base provider name>`. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).
- Provider callback addresses under `/ekom/payments/`.
- Everything gateways receive, including the reference a payment was started with. A payment a customer started before the upgrade still completes when its callback arrives after it.
- Receipt page query strings such as `?reference=` and `?siminnPayOrderKey=`.
- `CheckoutEvents.PayAsync` handlers that read or change `PaymentSettings`. The type kept its `Ekom.Payments` namespace.
- The legacy order status (`Ekom.Utilities.OrderStatus`) is still set.

The events raised when an order status changes are a separate change in the same Ekom version: `OrderEvents.OrderStatusChangedAsync`, `PaymentStatusChangedAsync` and `FulfillmentStatusChangedAsync` replace `OrderStatusChanging` and `OrderStatusChanged`. See [Order, payment, and fulfillment statuses](../guides/order-statuses.md).

### Code that must change

| Before | Now |
| --- | --- |
| `Ekom.Payments.Events.SuccessAsync` and `ErrorAsync` to follow payments | `Ekom.Events.OrderEvents.PaymentRecordedAsync`. The old events are raised only by a provider that has not been converted, and `SuccessAsync` no longer completes orders. |
| A provider's own `Events.Success` and `Events.Error` (Borgun, Valitor, Netgiro, Straumur, AltaPay, PayTrail, Siminn Pay, Teya Consumer Loans) | Marked obsolete and never raised. Use `PaymentRecordedAsync`. |
| `EkomPayments.GetOrder(request)` | `IOrderPayments.GetAsync(paymentId)` for the order's payment. `PaymentOrderRetriever.GetOrder` still reads the old rows. |
| `Ekom.Payments.OrderStatus` rows read through `IOrderService` | `OrderPayment` and its transactions, through `IOrderPayments`. |
| `ValitorResponseHelper` returning `OrderStatus` | Returns `OrderPayment`. |
| `SiminnPayStatusView(OrderStatus, ...)` | Built from `OrderPayment`. |
| Teya `PollingRequest.OrderId` | `PollingRequest.PaymentId`. |
| Completing an order from a payment success handler | Remove it. Recording a capture or authorization completes the order's checkout. |

### Behaviour that changed

- **Payment status is set by providers.** Previously it changed only through the legacy status or a manager user. Now a recorded failure makes it `Failed`, an authorization `Authorized`, and a refund `PartiallyRefunded` or `Refunded`.
- **Unverifiable callbacks record nothing.** They are logged. They are not treated as failed payments.
- **Straumur reports more.** A declined payment is recorded as `Failed` and a refund made at Straumur as `Refunded`. With delayed capture the order is `Authorized` until Straumur captures. Without delayed capture the authorization counts as the payment.
- **AltaPay confirms the order's latest AltaPay attempt**, not its oldest.
- **Teya Consumer Loans** resumes monitoring after a restart for loans requested within the last 7 days. A loan still undecided after its window gets a note on the payment asking staff to check it with Teya. The borrower's national id is no longer stored.
- **Manual payment status changes** are part of the payment history, and a late failure from an older attempt does not undo them.

## Before you start

1. Back up the database. The upgrade only adds tables, but the backup lets you compare and roll back.
2. List the `Ekom.Payments` packages the site references, and the base payment provider of each payment provider node in Umbraco.
3. Search the site's code for `Ekom.Payments.Events`, `Events.Success`, `Events.Error`, `EkomPayments.GetOrder`, `IOrderService` and `OrderStatus`. Each hit is in the table above.
4. Check that each base payment provider you use is one of the eight packages under [Packages](#packages). Any other provider has no 1.1.0 package in the Ekom.Payments repository. Convert it as described in [Convert a custom payment provider](#convert-a-custom-payment-provider), or ask its author.

## Upgrade the site

1. Update the Ekom package to version 1.2.0 or later.
2. Remove the package references listed under [Packages](#packages).
3. Add one `Ekom.Payments.<Provider>` package for each online gateway the site uses, at version 1.1.0.
4. Change the code found in step 3 of [Before you start](#before-you-start), as the table under [Code that must change](#code-that-must-change) shows.
5. If the site has its own provider, convert it as described in [Convert a custom payment provider](#convert-a-custom-payment-provider).
6. Build, and start the site against a copy of the production database. Ekom creates the payment tables at startup and then copies old payments in the background.
7. Run the checks in [Check that it worked](#check-that-it-worked), then repeat on production.

## What happens to old payment data

After the site has started, a background job copies the rows of the old `EkomPaymentOrders` and `EkomPayments` tables onto their orders:

- Each old row becomes a payment with a `Requested` transaction, and also a `Captured` transaction if it was paid. The captured transaction carries the card number, authorization code, transaction ID and card type that were stored.
- Only history is added. Order statuses, activity logs, events, emails and checkout are not touched.
- Signatures, hashes and card tokens inside old stored responses are not copied.
- An old payment whose order is not in this database is counted, logged with its row number, and left in the old table.
- The job works in batches of 200 rows. Progress is saved in the `EkomOrderPaymentMigration` table after each batch, so the job continues where it stopped after a restart. It can run any number of times without copying a payment twice.
- If the job fails, it logs `Copying old payments onto their orders failed. It continues at the next startup.` The site keeps running.
- The old tables are not changed or dropped. Drop them yourself once you no longer need them for a rollback.

When the job has copied something, the log has a line that starts with `Copied old payments onto their orders`, with the number copied, the number already present, and the number without an order. If some payments have no order, a warning lists the first 20 of their `ReferenceId` values.

## Convert a custom payment provider

A provider written against Ekom.Payments 1.0 must be recompiled against Ekom 1.2.0, because `IPaymentProvider`, `PaymentSettings` and the other payment contracts moved from `Ekom.Payments.Core` into Ekom. They kept the `Ekom.Payments` namespace. Then convert it:

1. Replace `IOrderService.InsertAsync` with `IOrderPayments.CreateAsync`. Send `payment.PaymentId` to the gateway where the old row's `UniqueId` was sent.
2. In the callback, find the payment with `IOrderPayments.GetAsync`, check `payment.BelongsTo(...)`, and verify the response. Then call `RecordAsync` with what the response means: `Captured`, `Authorized`, `Failed`, `Cancelled` or `Refunded`.
3. Remove the code that marked the row paid, stored `PaymentData` and raised the success event.
4. Log with `PaymentLog.BeginScope`, so log lines carry the order number, order id and payment id.

The [`AsynchronousExample` provider](https://github.com/Vettvangur/Ekom.Payments/tree/main/PaymentProviders/Ekom.Payments.AsynchronousExample) in the Ekom.Payments repository shows a converted provider. The repository's [README](https://github.com/Vettvangur/Ekom.Payments#writing-a-provider) lists the same steps.

Until a provider is converted, it keeps working through a bridge in `Ekom.Payments.Core` 1.1.0:

- A row it creates with `IOrderService.InsertAsync` is also stored as a payment on the order.
- A success it raises through `Ekom.Payments.Events` is recorded as a capture, which completes the order.
- Its failures are not recorded, because an error raised by an unconverted provider can come from a callback that could not be verified.

## Check that it worked

1. The log has the `Copied old payments onto their orders` line, or no payment errors if the site had no old payments.
2. Open an order paid before the upgrade in the Ekom section. Its payment shows the provider and, if it was paid, a captured transaction.
3. Place a test order with each online provider. The order completes, and its payment shows the transaction ID from the gateway.
4. Cancel a test payment at the gateway. The order is not completed.
5. Check that handlers of `OrderEvents.PaymentRecordedAsync` run for those test payments.

## Roll back

The upgrade only adds tables: `EkomOrderPayments`, `EkomOrderPaymentTransactions` and `EkomOrderPaymentMigration`. The old payment tables are not changed, so the previous versions of Ekom and Ekom.Payments run against the same database. Payments recorded while the new version ran exist only in the new tables, and the old version does not see them.

## What Ekom does not do for you

- Add the provider packages to the site.
- Convert custom providers or code that handled the old events.
- Copy a payment whose order is not in the database.
- Drop the old payment tables.

## Related pages

- [Payments](../guides/payments.md): how payments are recorded and how to read them in your code.
- [Payment and shipping providers](../guides/providers.md): set up the payment options a customer can choose.
- [Checkout](../guides/checkout.md): how a recorded payment completes an order.
- [Events](../reference/events.md): `PaymentRecordedAsync` and the other order events.
- [Before and after an upgrade](overview.md): the checks that apply to every upgrade.

[Back to Upgrading](README.md)
