[Ekom docs](../README.md) / [Guides](README.md) / Payments

# Payments

Ekom keeps a record of every payment on an order: each attempt at a payment provider and everything that happened to it. From that record Ekom works out the order's payment status, completes the checkout when a payment goes through, and shows the history in the Manager. Read this page when you add a payment gateway, write a payment provider, or pass payment details on to another system.

A store that only takes offline payments, such as pay on delivery, needs nothing but Ekom. Each online gateway is one extra package, for example `Ekom.Payments.Borgun`. To set up the payment options a customer can choose, see [Payment and shipping providers](providers.md). For the request flow, see [Checkout](checkout.md).

On this page:

- [Names used on this page](#names-used-on-this-page)
- [What Ekom stores](#what-ekom-stores)
- [How the payment status is worked out](#how-the-payment-status-is-worked-out)
- [When a payment completes the order](#when-a-payment-completes-the-order)
- [Read the payment in your own code](#read-the-payment-in-your-own-code)
- [Record payments from a gateway](#record-payments-from-a-gateway)
- [Add a payment provider package](#add-a-payment-provider-package)
- [React to recorded payments](#react-to-recorded-payments)
- [Find a payment in the logs](#find-a-payment-in-the-logs)
- [See payments in the Manager](#see-payments-in-the-manager)
- [Moving from Ekom.Payments 1.0](#moving-from-ekompayments-10)

## Names used on this page

| Name | What it is | Where it comes from |
| --- | --- | --- |
| Order number | The number customers and staff see. Every payment log line and activity entry starts with it. | `OrderData.OrderNumber` |
| Order ID | The order's GUID. | `OrderData.UniqueId` |
| Payment ID | One payment attempt at a provider. An order can have several. The gateway receives it as its reference. | `OrderPayment.PaymentId` |
| Transaction ID | The gateway's own reference for the payment. | The gateway's response |
| Attempt ID | The checkout attempt the payment was started from. See [Cancel, edit and retry a payment](checkout.md#cancel-edit-and-retry-a-payment). | `OrderPayment.AttemptId` |

## What Ekom stores

Ekom creates two tables at startup.

`EkomOrderPayments` has one row per payment attempt: the payment ID, the order ID and order number, the attempt ID, the provider node's key and name, the base provider type (for example `borgun` or `offline`), the amount and ISO currency code, the gateway's transaction ID once it is known, and the settings the payment was requested with. Card numbers, CVV and card expiry are never stored.

`EkomOrderPaymentTransactions` has one row for each thing that happened to a payment. Rows are only added, never changed.

| Type | Meaning |
| --- | --- |
| `Requested` | The customer was sent to the provider. |
| `Authorized` | The amount is reserved on the card and will be captured later. |
| `Captured` | The payment went through. |
| `Failed` | The provider reported that the payment did not go through. |
| `Cancelled` | The customer or the merchant cancelled the payment at the provider. |
| `Voided` | An authorization was released. |
| `Refunded` | Money was returned. The row holds the refunded amount. |
| `Manual` | A Manager user set the payment status. It never moves money. |
| `Note` | Information that is not an outcome, such as "monitoring ended, check with the provider". It never changes the payment status. |

A transaction holds the provider's data in three layers:

1. **Common fields** that any provider can fill: `TransactionId`, `AuthorizationCode`, `MaskedCardNumber`, `CardBrand`, `PaymentMethod`.
2. **`Details`**: labelled values the provider wants staff to see, such as an invoice number. They are stored with the transaction, so the Manager can show them without knowing the provider.
3. **`RawData`**: the provider's response as JSON, without signatures, secrets or full card numbers. Read it as your own type with `transaction.GetData<T>()`.

## How the payment status is worked out

Each time a transaction is recorded, Ekom calculates the order's payment status from all its transactions and saves it on the order. Code that reads `OrderInfo.Status.PaymentStatus`, and the order list filters, read that saved value. They do not read the payment tables.

| Transactions on the order | Payment status |
| --- | --- |
| Captures that add up to the order total or more | `Paid` |
| Captures that add up to less than the order total | `PartiallyPaid` |
| Captures, and refunds that add up to less than was captured | `PartiallyRefunded` |
| Captures, and refunds that add up to what was captured or more | `Refunded` |
| No capture, and an authorization that has not been voided | `Authorized` |
| No capture and no open authorization, and the latest transaction is a failure | `Failed` |
| No capture and no open authorization, and the latest transaction is a void | `Voided` |
| Anything else: only requests, cancellations or notes | `Pending` |

This has two effects you need to know:

- A late failure from an abandoned attempt cannot lower the status of an order that another attempt paid.
- A manual status set in the Manager wins over everything recorded before it. After it, only a capture, refund or void changes the status again. A later failure or cancellation is kept in the history only.

Ekom does not write the calculated status to an order that has not been moved to the separate order, payment and fulfillment statuses yet, or whose saved statuses are inconsistent and need review. Its transactions are still stored. See [Order, payment, and fulfillment statuses](order-statuses.md).

## When a payment completes the order

When a `Captured` or `Authorized` transaction is recorded and the order status is still `WaitingForPayment`, Ekom completes the order's checkout. Providers do not call completion themselves. [How a paid order is completed](checkout.md#how-a-paid-order-is-completed) describes what completion does.

- If the gateway sends the same callback twice, the transaction is stored once. If the first callback stopped before the order was completed, the second one finishes the completion.
- An order completed by an authorization moves on through the order status exactly like a paid order. Its payment status stays `Authorized` until a capture is recorded.
- If the payment belongs to a checkout attempt that was released, expired, replaced or changed, Ekom does not complete the order. See [A payment that arrives after its holds were released](checkout.md#a-payment-that-arrives-after-its-holds-were-released).

### Payments that do not match their order

In two cases Ekom opens an [order review](order-reviews.md), so the order appears in the Manager's **Needs review** view until a person resolves it:

| Code | When |
| --- | --- |
| `amount-mismatch` | The paid amount differs from the order total by 1 currency unit or more. For a capture, Ekom compares all captures of the order. For an authorization, it compares that authorization. |
| `paid-checkout-changed` | A payment arrived for a checkout attempt that was changed or released, so the order was not completed. |

Nothing is blocked or reversed. The payment is recorded as the gateway reported it.

### Offline payments

The offline checkout records a payment with provider type `offline`, payment method `Offline` and a `Captured` transaction, and then completes the order itself. Offline orders are therefore `Paid` when checkout finishes. This records that checkout was accepted, not that money was received.

## Read the payment in your own code

Code that sends a completed order on, for example to an ERP system, often needs the authorization code, card type or transaction ID. The completion event carries the payment the order was paid with, so you do not need to query the database.

```csharp
using Ekom.Events;

CheckoutEvents.CompleteCheckoutAsync += async (sender, args, ct) =>
{
    var payment = args.Payment; // null when no payment is recorded on the order
    if (payment == null) return;

    await erp.SendOrderAsync(args.OrderInfo, new ErpPayment
    {
        Provider = payment.Provider,           // the provider node name, for example "Card payment"
        ProviderType = payment.ProviderType,   // for example "borgun" or "offline"
        TransactionId = payment.TransactionId,
        AuthorizationCode = payment.AuthorizationCode,
        CardType = payment.CardBrand,
        MaskedCard = payment.MaskedCardNumber,
        Method = payment.PaymentMethod,
        Amount = payment.Amount,
        Currency = payment.Currency,
        PaidAtUtc = payment.CapturedUtc ?? payment.AuthorizedUtc,
        ContractNumber = payment.GetDetail("contractNumber"), // one of the provider's own details
    }, ct);
};
```

`args.Payment` is an `OrderPaymentAttempt`:

| Member | Holds |
| --- | --- |
| `Provider`, `ProviderType` | The provider node name, and the base provider that handled the payment. |
| `Status` | This attempt's own status: `Paid`, `Authorized`, `Failed`, `Cancelled`, `Pending`, `PartiallyRefunded`, `Refunded` or `Voided`. |
| `TransactionId`, `AuthorizationCode` | The gateway's reference and authorization code. |
| `CardBrand`, `MaskedCardNumber`, `PaymentMethod` | The card or method, when the provider reports it. The newest transaction that has a value wins. |
| `Amount`, `Currency` | The amount requested and its ISO currency code. |
| `CreatedUtc`, `AuthorizedUtc`, `CapturedUtc` | When the payment was requested, authorized and captured. |
| `Details`, `GetDetail(key)` | The provider's own labelled values. `GetDetail` ignores upper and lower case and returns `null` when the key is missing. |
| `GetData<T>()` | The provider's latest response read as your own type, for fields that have no property. |
| `PaymentId`, `AttemptId`, `Events` | The payment ID, its checkout attempt, and every transaction recorded on it. |

`args.Payment` is the order's captured payment. When there is none, it is an authorized payment, and otherwise the latest attempt. If Ekom cannot read the payment, it logs an error and leaves `args.Payment` as `null`, so a paid order still completes.

Outside the event, read the same payment from the order ID:

```csharp
using Ekom.API;
using Ekom.Payments;
using Ekom.Services;

OrderPaymentAttempt? payment = await Order.Instance.GetPaymentAsync(orderId, ct);

// or, with IOrderPayments injected:
OrderPaymentAttempt? current = await orderPayments.GetCurrentPaymentAsync(orderId, ct);
```

`Order.GetPaymentAsync` throws `ArgumentException` for an empty order ID. Which values are filled depends on the provider. An offline payment has its provider, amount and payment method only.

## Record payments from a gateway

`Ekom.Services.IOrderPayments` is the one entry point for payment providers and custom integrations. Record a payment in two steps: create it when the customer is sent to the gateway, and record what happened when the gateway answers.

```csharp
using Ekom.Models.Payments;
using Ekom.Payments;
using Ekom.Services;
using System.Text.Json;

// When the customer is sent to the gateway.
OrderPayment payment = await payments.CreateAsync(
    paymentSettings,       // the PaymentSettings Ekom gave your provider
    "mygateway",           // your base provider type
    totalAmount,
    myGatewaySettings,     // your own settings, stored for the callback
    httpContext: httpContext);

// In the gateway's callback, after you have verified it.
await payments.RecordAsync(new PaymentTransaction
{
    PaymentId = payment.PaymentId,
    Type = PaymentTransactionType.Captured,
    TransactionId = response.TransactionId,
    AuthorizationCode = response.AuthorizationCode,
    MaskedCardNumber = response.MaskedCard,
    Details = [new PaymentDetail("invoice", "Invoice number", response.InvoiceNumber, Copyable: true)],
    RawData = JsonSerializer.Serialize(new { response.TransactionId, response.Status, response.InvoiceNumber }),
});
```

The `CreateAsync` overload above is an extension method in `Ekom.Payments`. It reads the order, the attempt ID and the store from `PaymentSettings`, and stores the settings without card details. It also stores the customer's IP address and user agent when you pass `httpContext`.

| Member | Use |
| --- | --- |
| `CreateAsync` | Stores a payment attempt and its `Requested` transaction. Throws `ArgumentException` when the order ID is empty. |
| `RecordAsync` | Stores a transaction once, recalculates the payment status, writes the activity log, raises `OrderEvents.PaymentRecordedAsync`, and completes checkout when it applies. Returns a `PaymentRecordResult`. |
| `GetAsync` | Finds a payment by its payment ID. |
| `GetByProviderReferenceAsync` | Finds the latest payment by a lookup key the provider stored when it created the payment, for gateways that do not send the payment ID back. |
| `GetOpenPaymentsAsync` | A provider's payments requested after a given time that have nothing recorded since the request. A provider that polls its gateway uses it to pick up unfinished payments after a restart. |
| `GetForOrderAsync` | All payments of an order, as the Manager shows them. |
| `GetCurrentPaymentsAsync` | The current payment of many orders, loaded together. |
| `FindOrderAsync` | The order of a payment ID or a gateway transaction ID. |

`RecordAsync` throws `InvalidOperationException` when the payment ID does not exist, and `ArgumentException` for a `Manual` transaction. Manual status changes are made in the Manager.

When you leave a value out of `PaymentTransaction`, Ekom uses the payment's amount for `Amount`, the provider name for `Actor`, and the current time for `OccurredUtc`.

### Rules for a payment provider

- **Verify first, then record.** A callback you cannot verify says nothing about the payment. Log it and record nothing. Do not record it as `Failed`.
- **Record a failure only when the gateway says so** in a response you have verified.
- **Store each capture once.** Pass the same `IdempotencyKey` for every delivery of the same capture. Without a key, Ekom uses the payment ID, the type and the transaction ID, so a repeated callback with the same transaction ID is stored once.
- **Check the provider type.** `payment.BelongsTo("mygateway")` returns `false` for a payment that another provider started. Refuse it in your callback.
- **Keep secrets out of `RawData`:** signatures, hashes, card tokens and national ID numbers.
- **Start log lines with the order.** `PaymentLog.BeginScope(logger, payment)` adds `OrderNumber`, `OrderUniqueId`, `PaymentId`, `PaymentProvider` and `PaymentAttemptId` to every log line in the scope. `PaymentLog.Template` is the shared message prefix.

## Add a payment provider package

A payment provider is a class that implements `Ekom.Payments.IPaymentProvider` and has a constant named `_ppNodeName` that holds its base provider name. Ekom finds it when checkout asks for that name. The name lookup ignores upper and lower case. Ekom creates the provider through dependency injection for each payment request.

If no installed provider has the name, checkout fails with `PaymentProviderNotFoundException` ("Base Payment Provider not found"). You can also register a provider type by hand with `EkomPayments.RegisterPaymentProvider(name, type)`.

A package that needs its own services implements `Ekom.Payments.IPaymentModule`. The class needs a public constructor without parameters. At startup Ekom finds every module in the assemblies that reference Ekom and calls it, so installing the package is all a site does.

```csharp
using Ekom.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class MyGatewayModule : IPaymentModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        => services.AddHttpClient<MyGatewayClient>();
}
```

Providers read their settings from the payment provider node with `Ekom.Payments.IUmbracoService.PopulatePaymentProviderProperties`. Ekom's Umbraco packages implement this service.

## React to recorded payments

`OrderEvents.PaymentRecordedAsync` is raised after a transaction is stored and the payment status is recalculated. The arguments hold `Payment`, `Transaction`, `PreviousPaymentStatus` and `PaymentStatus`.

```csharp
OrderEvents.PaymentRecordedAsync += async (sender, args, ct) =>
{
    if (args.Transaction.Type == PaymentTransactionType.Refunded)
    {
        await accounting.BookRefundAsync(args.Payment.OrderNumber, args.Transaction.Amount, ct);
    }
};
```

The event is not raised for a repeated callback that stores nothing. It runs before checkout completion. If your handler throws, the transaction stays stored, the exception reaches the provider's callback, and the order is not completed in that call. A repeated callback then completes it.

## Find a payment in the logs

Every transaction that `RecordAsync` stores also writes an entry to the order activity log, in the same database transaction. The request itself (`Requested`) does not. For example:

```text
Payment successful. Order number: IS-1042; Amount: 24990 ISK; Payment provider: Card payment; Transaction ID: TX-7741; PaymentId: b83d2f6a-91c4-4e0b-8d57-3a1f6c0e41c7.
```

Captures and authorizations are logged as `Success`, failures as `Alert`, and everything else as `Info`.

Application log entries carry the same values as structured fields, including `OrderNumber`, so you can follow one payment by filtering on it. Failures are logged as warnings, everything else as information.

## See payments in the Manager

The order drawer shows the order's current payment: provider, status, transaction ID, authorization and capture times, amount, authorization code, card, and the provider's details. Below it is a list of every attempt with its transactions. `GET /ekom/manager/orderPayments/{orderId}` returns the same data. See [Orders in the Manager](../manager/orders.md).

## Moving from Ekom.Payments 1.0

Ekom and the payment provider packages must be upgraded together. After startup, a background job copies the rows of the old `EkomPaymentOrders` and `EkomPayments` tables onto their orders as payment history. It does not change order statuses and does not drop the old tables. [Upgrade payments](../upgrading/payments.md) lists the package, code and behaviour changes.

## Related pages

- [Checkout](checkout.md): how a payment is started and how a paid order is completed.
- [Payment and shipping providers](providers.md): set up the payment provider nodes.
- [Order, payment, and fulfillment statuses](order-statuses.md): how the payment status fits with the other statuses.
- [Order reviews](order-reviews.md): orders whose payment a person must check.
- [Events](../reference/events.md): every event Ekom raises.
- [Upgrade payments](../upgrading/payments.md): moving from Ekom.Payments 1.0.

[Back to Guides](README.md)
