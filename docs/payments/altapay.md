# AltaPay

[Ekom docs](../README.md) / [Payment gateways](README.md) / AltaPay

AltaPay is a card payment gateway. The customer is sent to a payment page hosted by AltaPay and comes back to your site when the payment is done. The package is `Ekom.Payments.AltaPay`.

On this page:

- [Before you start](#before-you-start)
- [Set it up](#set-it-up)
- [Settings](#settings)
- [What the customer sees](#what-the-customer-sees)
- [What Ekom records](#what-ekom-records)
- [Capture, refund and cancel](#capture-refund-and-cancel)
- [Test it](#test-it)
- [Limits and things to know](#limits-and-things-to-know)
- [Related pages](#related-pages)

## Before you start

You need:

- Ekom 1.2 or later. Use version 1.1.0 or later of `Ekom.Payments.AltaPay`.
- An AltaPay merchant account with access to the merchant API. From AltaPay you need the API user name and password, the name of your terminal, and the base address of the merchant API.
- A shared secret that is set on your AltaPay terminal. AltaPay uses it to sign its answers, and Ekom uses the same secret to check them. Without it, Ekom records no AltaPay payments.

## Set it up

1. Install the package in your site project:

   ```bash
   dotnet add package Ekom.Payments.AltaPay
   ```

   Ekom finds the provider at startup. There is no setup code.

2. Create a payment provider node under the store's payment provider container, and set its `basePaymentProvider` to `altaPay`. Fill in `successUrl` and `errorUrl` too. See [Set up a payment provider](../guides/providers.md#set-up-a-payment-provider).

3. Fill in the settings in the [Settings](#settings) table, on the node or in `appsettings.json`.

4. Set the shared secret on your AltaPay terminal, and put the same value in `customerInformationSharedSecret`.

5. Decide where AltaPay gets its payment form from, and set `paymentFormUrl`. See [The payment form callback](#the-payment-form-callback).

You do not register callback addresses at AltaPay. The package sends them with every payment request:

| Address | Sent to AltaPay as | Used for |
| --- | --- | --- |
| `/ekom/payments/altaresponse` | `config[callback_ok]` and `config[callback_notification]` | A successful payment, from the customer's browser and from AltaPay's server. |
| `/ekom/payments/altaresponse/fail` | `config[callback_fail]` | A payment that did not go through. |
| The value of `paymentFormUrl` | `config[callback_form]` | The payment form AltaPay shows. |

The addresses are made absolute with the scheme and host of the checkout request, or with the host in `hostOverride`. If `PaymentSettings.ReportUrl` is set, for example in a `CheckoutEvents.PayAsync` handler, it replaces the notification address.

### The payment form callback

The package has a third route, `/ekom/payments/altaresponse/callback`. It accepts GET and POST, raises the `Ekom.Payments.AltaPay.Events.CallbackUrl` event, and returns the event's `Template` as the response body. If no handler sets `Template`, the body is empty.

To use it, set `paymentFormUrl` to `/ekom/payments/altaresponse/callback` and subscribe to the event once at startup:

```csharp
using Ekom.Payments.AltaPay;

Events.CallbackUrl += async (sender, args) =>
{
    // The package sends the store alias as the first transaction_info value.
    string? storeAlias = args.Request?.TransactionInfo.FirstOrDefault();

    args.Template = await paymentFormRenderer.RenderAsync(storeAlias, args.Request?.Language);
};
```

`args.Request` holds `OrderId` (AltaPay's `shop_orderid`, which is the Ekom order ID), `Amount`, `Currency`, `Language` and `TransactionInfo`. All handlers run at the same time and share one `args`, so use a single handler.

You can also point `paymentFormUrl` at a page of your own. It must be a path on your site, such as `/altapay-form`, not a full URL.

## Settings

Each alias is a property of `AltaSettings`, written in camelCase. Ekom reads it from the provider node, then from configuration. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

| Alias | Required | Default | What it does |
| --- | --- | --- | --- |
| `apiUserName` | Yes | none | User name for the AltaPay merchant API. Sent with basic authentication. |
| `apiPassword` | Yes | none | Password for the AltaPay merchant API. |
| `terminal` | Yes | none | The AltaPay terminal name, sent as `terminal`. A payment saved without it cannot be verified. |
| `baseAddress` | Yes | none | Base address of the AltaPay merchant API. The package posts to `createPaymentRequest` relative to it, so end the address with `/`. |
| `customerInformationSharedSecret` | Yes | none | The shared secret set on the AltaPay terminal. Ekom uses it to check the checksum on AltaPay's answers. |
| `paymentFormUrl` | Yes | none | A path on your site, sent as `config[callback_form]`. See [The payment form callback](#the-payment-form-callback). The payment request fails when it is empty. |
| `hostOverride` | No | none | A host name, for example `shop.example.com`, used in the callback addresses instead of the host of the checkout request. The scheme stays that of the checkout request. Use it when the site runs on an address AltaPay cannot reach, such as `localhost`. |

None of these settings hold one value per store or per language. To use a different AltaPay account in another store, create a second provider node and give it a `configurationKey`.

```json
{
  "Ekom": {
    "Payments": {
      "altaPay": {
        "apiUserName": "<api user name>",
        "apiPassword": "<api password>",
        "terminal": "<terminal name>",
        "baseAddress": "https://<altapay merchant api address>/",
        "customerInformationSharedSecret": "<shared secret>",
        "paymentFormUrl": "/ekom/payments/altaresponse/callback"
      }
    }
  }
}
```

Keep `apiPassword` and `customerInformationSharedSecret` out of source control, for example in the environment variable `Ekom__Payments__altaPay__apiPassword`.

## What the customer sees

1. The customer submits payment. The package sends a payment request to AltaPay with the terminal, the Ekom order ID as `shop_orderid`, the amount, the currency, the language, the callback addresses, the store alias, and the order lines.
2. AltaPay answers with the address of its payment page. The customer's browser is sent there.
3. The customer pays on AltaPay's page.
4. When the payment succeeds, AltaPay sends the browser to `/ekom/payments/altaresponse`. Ekom records the payment and sends the customer on to the provider's `successUrl`, with `?orderId=<order id>`.
5. When the payment fails, AltaPay sends the browser to `/ekom/payments/altaresponse/fail`. Ekom sends the customer to the error URL, which releases the stock holds and ends on the provider's `errorUrl`. See [Cancel, edit and retry a payment](../guides/checkout.md#cancel-edit-and-retry-a-payment).

On the way to the error URL, the package adds AltaPay's values to the query string: `transactionId`, `paymentId`, `reservedAmount`, `capturedAmount`, `refundedAmount`, `currency`, `paymentStatus`, `result`, `errorCode` and `errorMessage` when AltaPay sent them, and `altapayFailed=true`. If the payment has no error URL, the customer goes to `/?altapayerror=true` on your site.

If AltaPay does not return a payment page address, the customer goes straight to the error URL with `errorStatus=paymenterror` and an `errorMessage`. If the AltaPay call itself fails, checkout handles it as described in [If sending to payment fails](../guides/checkout.md#if-sending-to-payment-fails).

The language sent to AltaPay is `is` or `en`, taken from the provider node's `language`. Every other language is sent as `is`.

## What Ekom records

AltaPay refers to a payment by the Ekom order ID. Ekom answers for the latest AltaPay payment of that order.

| AltaPay answer | What Ekom does |
| --- | --- |
| Result `Success` with a correct checksum | Records `Captured` for the amount Ekom requested, once per payment. The transaction ID is AltaPay's `TransactionId`, the masked card number is AltaPay's `CreditCardMaskedPan`. Recording the payment completes the order. |
| Result `Success` with a missing or wrong checksum | Records nothing. Returns HTTP 500. |
| Payment saved without `terminal` or `customerInformationSharedSecret` | Records nothing. Returns HTTP 500 and logs an error. |
| Any other result at `/ekom/payments/altaresponse` | Records nothing. Returns HTTP 200 with the text `Payment not Valid`. |
| Any answer at `/ekom/payments/altaresponse/fail` | Records nothing. This answer carries no checksum. The customer is sent to the error URL. |
| An order with no AltaPay payment | Records nothing. Returns HTTP 404. |

AltaPay sends both a browser answer and a server notification for the same payment. The capture is stored once.

The capture also stores these details, which the Manager shows with the payment:

| Key | Label | Value |
| --- | --- | --- |
| `altaPaymentId` | AltaPay payment id | AltaPay's `PaymentId`. |
| `authType` | Authorization type | AltaPay's `AuthType`. |
| `cardStatus` | Card status | AltaPay's `CardStatus`. |
| `terminal` | Terminal | The terminal name. |

`RawData` holds AltaPay's parsed response.

The package never records `Authorized`, `Failed`, `Cancelled`, `Refunded` or `Voided`. A payment that fails at AltaPay keeps only its `Requested` transaction, so the payment status stays `Pending`.

## Capture, refund and cancel

The package asks AltaPay for `paymentAndCapture`, so the money is captured with the payment. There is no separate capture step.

Ekom cannot refund, void or cancel an AltaPay payment. Do it at AltaPay. Ekom does not hear about it, so the order's payment status stays `Paid`. A Manager user can set the payment status by hand. See [What Ekom stores](../guides/payments.md#what-ekom-stores).

The package also contains `AltaService.CaptureReservationAsync`, which calls AltaPay's `captureReservation`. Ekom never calls it. It is registered only when you call `services.AddAltaPay(configuration)`, which reads `BaseAddress`, `ApiUserName`, `ApiPassword` and `HostOverride` from `Ekom:Payments:altapay`. It records nothing on the order. If `HostOverride` is set in that section, `AddAltaPay` also sends it as the `Host` header of the requests to AltaPay.

## Test it

The package has no test mode setting. Which AltaPay environment you reach depends on `baseAddress`, `terminal` and the API user. Use the test values AltaPay gives you.

AltaPay must be able to reach the callback addresses. On a development machine, run the site behind a public address that forwards to it, and set `hostOverride` to that host.

To check a test payment:

1. Pay with a test card on AltaPay's page.
2. Look for `Alta Payment Response - SUCCESS` in the application log.
3. Open the order in the Manager. The payment shows a `Captured` transaction with AltaPay's transaction ID.

If the log shows `Failed checksum verification` or `No CustomerInformationSharedSecret`, the shared secret on the terminal and in `customerInformationSharedSecret` do not match, or one of them is missing.

## Limits and things to know

- **The checksum is the only proof.** It covers the amount Ekom requested, the currency, the order ID and the shared secret. A payment for a different amount does not pass the check and is not recorded.
- **The settings are saved with each payment.** The callback checks the answer with the secret that was in use when the payment started, not the current one. The saved settings include `apiPassword` and `customerInformationSharedSecret`, in the `EkomOrderPayments` table.
- **The latest attempt wins.** AltaPay sends back the order ID, not the payment ID. When an order has several AltaPay attempts, the answer is matched to the newest one.
- **`paymentFormUrl` must be a path.** A full URL such as `https://shop.example.com/form` makes the payment request fail, and so does an empty value.
- **Host names that start with `ekom`.** The package removes a double slash before `ekom` in every callback address. A site whose host name starts with `ekom`, such as `ekomshop.example`, gets broken callback addresses.
- **The error message is fixed.** When AltaPay returns no payment page, the `errorMessage` in the query string is a fixed English text that names another company. Show your own message on the error page instead.
- **Amounts always have two decimals,** also for currencies without minor units such as ISK. Each order line is sent with its `Price` as the unit price and its `Quantity`.
- **The cancel URL is not used.** The package has no cancel route. A payment that does not go through ends on the error URL.
- **Error emails.** When a callback throws and `Ekom:Payments:SendEmailAlerts` is `true` (the default), Ekom emails the exception and AltaPay's response through the Umbraco SMTP settings.
- **Old events.** `Events.Success` and `Events.Error` are no longer raised. Use `OrderEvents.PaymentRecordedAsync`. See [React to recorded payments](../guides/payments.md#react-to-recorded-payments).

## Related pages

- [Payments](../guides/payments.md): what Ekom stores for a payment and how the payment status is worked out.
- [Payment and shipping providers](../guides/providers.md): create the provider node and fill in its settings.
- [Checkout](../guides/checkout.md): how a payment is started, returned and completed.
- [Order reviews](../guides/order-reviews.md): orders whose payment a person must check.
- [Payment gateways](README.md): all gateways Ekom supports.

[Back to Payment gateways](README.md)
