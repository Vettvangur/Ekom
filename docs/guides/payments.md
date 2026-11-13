# Payments

Ekom owns everything about an order's payments: the contracts a payment provider implements, the record of what happened to each payment, and the payment status derived from that record. Payment providers plug into it.

A site that only takes offline payments needs Ekom alone. Each online gateway is one extra package, for example `Ekom.Payments.Borgun`. See [Providers](providers.md) for configuring provider nodes and [Checkout](checkout.md) for the request flow.

## Identifiers

| Name | What it is | Where it comes from |
| --- | --- | --- |
| Order number | What customers and staff see. Every payment log line and message starts with it. | `OrderData.OrderNumber` |
| Order id | The order's Guid. | `OrderData.UniqueId` |
| Payment id | One payment attempt at a provider. An order can have several. Gateways receive it as their reference. | `OrderPayment.PaymentId` |
| Transaction ID | The gateway's own reference for the payment. | The provider's response |
| Attempt id | The Ekom checkout attempt the payment was started from. | `OrderPayment.AttemptId` |

Before payments were recorded on the order, the payment id was the "unique id" of an Ekom.Payments row, and logs called it an order id. The values are the same; only the name is now exact.

## What is stored

Two tables, created at startup.

`EkomOrderPayments` has one row per payment attempt: the payment id, the order id and order number, the provider node name and base provider type, the amount and ISO currency code, the gateway's transaction ID once known, and the payment and provider settings the request was made with. Card details are never stored.

`EkomOrderPaymentTransactions` has one row per thing that happened to a payment. Rows are only ever added.

| Type | Meaning |
| --- | --- |
| `Requested` | The customer was sent to the provider. |
| `Authorized` | The amount is reserved and will be captured later. |
| `Captured` | The payment went through. |
| `Failed` | The provider reported that the payment did not go through. |
| `Cancelled` | The customer or the merchant cancelled the payment at the provider. |
| `Voided` | An authorization was released. |
| `Refunded` | Money was returned. The row carries the refunded amount. |
| `Manual` | A manager user set the payment status. It never moves money. |
| `Note` | Something worth knowing that is not an outcome, such as "monitoring ended, check with the provider". It never changes the payment status. |

A transaction keeps provider data in three layers:

1. Common columns that every provider can fill: `TransactionId`, `AuthorizationCode`, `MaskedCardNumber`, `CardBrand`, `PaymentMethod`.
2. `Details`: a list of labelled values the provider chooses to show staff, such as an invoice number or a contract number. They are stored with the transaction, so the manager shows them without knowing the provider.
3. `RawData`: the provider's response as JSON, without signatures, secrets or full card numbers. Read it as a typed object with `transaction.GetData<T>()`.

## Payment status is calculated

The order's payment status is worked out from its transactions each time one is recorded, and written to the order. Code that reads `OrderInfo.Status.PaymentStatus` or filters the order list does not touch the payment tables.

| Transactions on the order | Payment status |
| --- | --- |
| A capture covering the order total | `Paid` |
| Captures covering part of the total | `PartiallyPaid` |
| A capture and refunds for less than was captured | `PartiallyRefunded` |
| A capture and refunds for all of it | `Refunded` |
| An authorization that has not been voided, no capture | `Authorized` |
| No capture or authorization; the latest outcome is a failure | `Failed` |
| No capture or authorization; the latest outcome is a void | `Voided` |
| Only requests, cancellations or notes | `Pending` |

Two consequences are worth knowing:

- A late failure from an abandoned attempt cannot downgrade an order that another attempt paid for.
- A manual change overrides everything recorded before it. After it, only a capture, refund or void from a provider changes the status again; a later failure or cancellation is kept as history only.

Orders that have not been moved to the independent statuses yet keep the status of their legacy record; their transactions are still stored.

## Checkout completion

When a `Captured` or `Authorized` transaction is recorded for an order that is still waiting for its payment, Ekom completes that order's checkout. Providers do not call completion themselves, and Ekom no longer listens for an Ekom.Payments success event.

- A gateway that repeats its callback does not store the transaction twice. If the first delivery stopped before the order was completed, the repeat finishes the completion.
- An order completed by an authorization moves on through the legacy status exactly as a paid order does, but its payment status stays `Authorized` until the capture is recorded.
- If the capture belongs to a checkout attempt that was already released, completion reports that the payment needs reconciliation instead of completing. See [Checkout](checkout.md).

Offline checkout records a payment with a `Captured` transaction and completes the order itself, so offline orders are `Paid` at checkout as before. This records that checkout was accepted, not that funds were received.

## Recording payments: `IOrderPayments`

`Ekom.Services.IOrderPayments` is the single entry point for providers and custom integrations.

```csharp
// When the customer is sent to the gateway
var payment = await payments.CreateAsync(
    paymentSettings,          // what Ekom handed the provider
    "mygateway",              // the base payment provider type
    total,
    myGatewaySettings,        // stored for the callback
    httpContext: httpContext);

// In the gateway's callback, after verifying it
await payments.RecordAsync(new PaymentTransaction
{
    PaymentId = payment.PaymentId,
    Type = PaymentTransactionType.Captured,
    TransactionId = response.TransactionId,
    AuthorizationCode = response.AuthorizationCode,
    MaskedCardNumber = response.MaskedCard,
    Details = [new PaymentDetail("invoice", "Invoice number", response.Invoice, Copyable: true)],
    RawData = JsonSerializer.Serialize(response.WithoutSignature()),
});
```

| Member | Use |
| --- | --- |
| `CreateAsync` | Stores a payment attempt and its `Requested` transaction. The extension in `Ekom.Payments` builds it from `PaymentSettings`. |
| `RecordAsync` | Stores a transaction once, recalculates the payment status, writes the activity log, raises `PaymentRecordedAsync`, and completes checkout when it applies. |
| `GetAsync` | Finds a payment by its id. |
| `GetByProviderReferenceAsync` | Finds a provider's latest payment by the provider's own lookup key, for gateways that do not echo the payment id. |
| `GetOpenPaymentsAsync` | A provider's payments with nothing recorded since their request. A provider that polls its gateway uses it to pick unfinished payments up again after a restart. |
| `GetForOrderAsync` | The order's payments as the manager shows them. |
| `FindOrderAsync` | The order of a payment id or a gateway transaction ID. |

### Rules for a provider

- **Verify first, then record.** A callback that cannot be verified says nothing about the payment. Log it and record nothing; do not record it as `Failed`.
- **Record a failure only when the gateway says so** in a verified response.
- **One capture per payment.** Pass the same `IdempotencyKey` for every delivery of a capture. Without one, the key is the payment id, the type and the transaction ID.
- **Check the provider type.** `payment.BelongsTo("mygateway")` refuses another provider's payment in your callback.
- **Leave secrets out of `RawData`:** signatures, hashes, card tokens, national ids.
- **Lead logs with the order.** `PaymentLog.BeginScope(logger, payment)` adds `OrderNumber`, `OrderUniqueId`, `PaymentId`, `PaymentProvider` and `PaymentAttemptId` to every log line, and `PaymentLog.Template` is the shared message prefix.

### Registering a provider package

A provider is any `Ekom.Payments.IPaymentProvider` with a constant named `_ppNodeName` holding its base provider name. Ekom finds it when checkout asks for that name.

A package that needs its own services implements `Ekom.Payments.IPaymentModule`. Ekom finds every implementation in assemblies that reference Ekom and calls it during startup, so installing the package is all a site does.

```csharp
public sealed class MyGatewayModule : IPaymentModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        => services.AddHttpClient<MyGatewayClient>();
}
```

Providers read their settings from the payment provider node with `Ekom.Payments.IUmbracoService.PopulatePaymentProviderProperties`, which Ekom's Umbraco packages implement.

## Events

`OrderEvents.PaymentRecordedAsync` is raised after a transaction was stored and the status recalculated. Its arguments carry the payment, the transaction, and the payment status before and after. It replaces `Ekom.Payments.Events.SuccessAsync` and `ErrorAsync` for following payments.

## Activity log and application logs

Every recorded transaction except the request writes an order activity entry in the same database transaction, for example:

```text
Payment successful. Order number: IS-1042; Amount: 24990 ISK; Payment provider: Borgun; Transaction ID: TX-7741; PaymentId: b83d…41c7.
```

Application logs use the same order-first fields, so a whole payment can be followed by filtering on `OrderNumber`.

## Manager

The order drawer shows the order's current payment (provider, status, transaction ID, authorized and captured times, amount, authorization code, card, and the provider's details) and an expandable list of every attempt with its events. `GET /ekom/manager/orderPayments/{orderId}` returns the same data. See [Manager orders](../manager/orders.md).

## Payments from Ekom.Payments 1.0 and earlier

After startup a background job copies the rows of the old `EkomPaymentOrders` and `EkomPayments` tables onto their orders as payment history. See [Upgrading payments](../upgrading/payments.md).
