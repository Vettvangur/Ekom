# PayTrail

[Ekom docs](../README.md) / [Payment gateways](README.md) / PayTrail

PayTrail is a Finnish payment service. The customer is sent to a payment page hosted by PayTrail, chooses a payment method there, and comes back to your site when the payment is done. The package is `Ekom.Payments.PayTrail`.

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

- Ekom 1.2 or later. Use version 1.1.0 or later of `Ekom.Payments.PayTrail`.
- A PayTrail merchant account. From PayTrail you need the account ID and the secret key. The package signs every request to PayTrail with the secret key, and checks every answer from PayTrail with it.

## Set it up

1. Install the package in your site project:

   ```bash
   dotnet add package Ekom.Payments.PayTrail
   ```

   Ekom finds the provider at startup. There is no setup code.

2. Create a payment provider node under the store's payment provider container, and set its `basePaymentProvider` to `payTrail`. Fill in `successUrl` and `cancelUrl` too. See [Set up a payment provider](../guides/providers.md#set-up-a-payment-provider).

3. Fill in the settings in the [Settings](#settings) table, on the node or in `appsettings.json`.

You do not register a callback address at PayTrail. The package sends the addresses with every payment:

| Address | Sent to PayTrail as | Used for |
| --- | --- | --- |
| `/ekom/payments/paytrailresponse` | `redirectUrls.success` and `redirectUrls.cancel` | The customer's browser coming back from PayTrail. |
| `/ekom/payments/paytrailresponse?callback=true` | `callbackUrls.success` and `callbackUrls.cancel` | PayTrail's server calling your site. |

The addresses are made absolute with the scheme and host of the checkout request. If `PaymentSettings.ReportUrl` is set, for example in a `CheckoutEvents.PayAsync` handler, it replaces `/ekom/payments/paytrailresponse` in both.

## Settings

Each alias is a property of `PayTrailSettings`, written in camelCase. Ekom reads it from the provider node, then from configuration. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

| Alias | Required | Default | What it does |
| --- | --- | --- | --- |
| `accountId` | Yes | none | Your PayTrail account ID, sent in the `checkout-account` header. |
| `secretKey` | Yes | none | Your PayTrail secret key. Signs the requests to PayTrail and checks the signature on its answers. |
| `apiBaseUrl` | Yes | none | Address of the PayTrail API. The package posts to `/payments` on this host. The settings class names `https://services.paytrail.com` for both test and production. |
| `algorithm` | Yes | none | The signing algorithm: `sha256` or `sha512`. Sent in the `checkout-algorithm` header. |
| `platformName` | No | none | Sent in the `platform-name` header when it has a value. |
| `debugLog` | No | `false` | `true` logs the full create-payment request and response, and every answer from PayTrail with the signature removed, at Information level. The payloads contain customer data. Turn it on only while you look for a problem. |

The payment request fails when `accountId`, `secretKey`, `apiBaseUrl` or `algorithm` is empty.

None of these settings hold one value per store or per language. To use a different PayTrail account in another store, create a second provider node and give it a `configurationKey`.

```json
{
  "Ekom": {
    "Payments": {
      "payTrail": {
        "accountId": "<account id>",
        "secretKey": "<secret key>",
        "apiBaseUrl": "https://services.paytrail.com",
        "algorithm": "sha256",
        "debugLog": false
      }
    }
  }
}
```

Keep `secretKey` out of source control, for example in the environment variable `Ekom__Payments__payTrail__secretKey`.

You can also set the values in code, in a `CheckoutEvents.PayAsync` handler:

```csharp
using Ekom.Events;
using Ekom.Payments.PayTrail;

CheckoutEvents.PayAsync += (sender, args, ct) =>
{
    args.PaymentSettings.SetPayTrailSettings(new PayTrailSettings
    {
        AccountId = "<account id>",
        SecretKey = secretKeyFromYourVault,
        ApiBaseUrl = new Uri("https://services.paytrail.com"),
        Algorithm = "sha256",
    });
    return Task.CompletedTask;
};
```

Values you leave empty in code are still read from the node and configuration.

## What the customer sees

1. The customer submits payment. The package creates a payment at PayTrail with the order data, and PayTrail answers with a transaction ID and the address of its payment page.
2. The customer's browser is sent to PayTrail's payment page.
3. The customer pays, or cancels, on PayTrail's page.
4. PayTrail sends the browser back to `/ekom/payments/paytrailresponse` with its signed `checkout-*` values. Separately, PayTrail's server calls the same address with `callback=true`.
5. When the status is `ok`, Ekom records the payment and sends the customer on to the provider's `successUrl`, with `?orderId=<order id>`.
6. For every other status, the customer is sent to the cancel URL. Ekom releases the stock holds and the customer ends on the provider's `cancelUrl`. See [Cancel, edit and retry a payment](../guides/checkout.md#cancel-edit-and-retry-a-payment).

The provider's `errorUrl` is not used by this package.

If PayTrail refuses to create the payment, checkout handles it as described in [If sending to payment fails](../guides/checkout.md#if-sending-to-payment-fails).

What the package sends to PayTrail:

| Field | Value |
| --- | --- |
| `stamp` | The Ekom payment ID. PayTrail sends it back, and Ekom finds the payment by it. |
| `reference` | The order number. When the order has none, the payment's sequence number. |
| `amount` | The sum of the payment items, in minor units. |
| `currency` | The store's currency code. |
| `language` | `FI`, `SV` or `EN`, from the provider node's `language`. Every other language is sent as `EN`. |
| `items` | One item for each payment item. See [Limits and things to know](#limits-and-things-to-know). |
| `customer` | The customer's email, phone, and name split into first name (the first word) and last name (the rest). |

## What Ekom records

Ekom checks the signature first. The signature is an HMAC over the `checkout-*` values, made with your secret key.

| PayTrail answer | What Ekom does |
| --- | --- |
| Status `ok`, correct signature, amount matches | Records `Captured` for the sum of the payment items, once per payment. The transaction ID is `checkout-transaction-id`, the payment method is `checkout-provider`. Recording the payment completes the order. |
| Status `ok`, correct signature, amount does not match | Records nothing. Returns HTTP 400 and logs a warning. |
| Status `fail`, correct signature | Records `Failed` with the message "PayTrail reported the payment as failed". |
| Any other status, such as `pending` or `delayed` | Records nothing. These are not final. |
| Wrong signature | Records nothing. Returns HTTP 401. |
| Missing `signature`, `checkout-stamp`, `checkout-status` or `checkout-amount` | Records nothing. Returns HTTP 400. |
| A stamp that matches no PayTrail payment | Records nothing. Returns HTTP 404. |

The browser return and the server callback carry the same answer. The capture is stored once. A repeated `fail` with the same transaction ID is also stored once.

Each recorded transaction also stores these details, which the Manager shows with the payment:

| Key | Label | Value |
| --- | --- | --- |
| `reference` | PayTrail reference | `checkout-reference`. |
| `status` | PayTrail status | `checkout-status`. |

`RawData` holds all values PayTrail sent, with the signature removed.

The package never records `Authorized`, `Cancelled`, `Refunded` or `Voided`.

## Capture, refund and cancel

A payment that PayTrail reports as `ok` is recorded as captured. There is no separate capture step.

Ekom cannot refund or cancel a PayTrail payment. Do it at PayTrail. Ekom does not hear about it, so the order's payment status stays `Paid`. A Manager user can set the payment status by hand. See [What Ekom stores](../guides/payments.md#what-ekom-stores).

## Test it

The package has no test mode setting. Which account you test with depends on `accountId` and `secretKey`.

The package README lists the test account from PayTrail's documentation:

```text
accountId: 375917
secretKey: SAIPPUAKAUPPIAS
```

Use these only on a test site.

Both the browser return and the server callback record the payment. On a development machine that PayTrail cannot reach, the browser return still records the payment.

To check a test payment:

1. Pay on PayTrail's page.
2. Look for `PayTrail Payment Response - SUCCESS` in the application log.
3. Open the order in the Manager. The payment shows a `Captured` transaction with PayTrail's transaction ID.

If the log shows `Signature mismatch`, the secret key does not match the account. If it shows `Amount mismatch`, see the amount rules below. Turn on `debugLog` to see what was sent and received.

## Limits and things to know

- **Minor units.** PayTrail takes amounts as whole numbers in the currency's smallest unit. The package multiplies by 100, except for `ISK`, `JPY` and `KRW`, and rounds half away from zero.
- **Payment items.** By default Ekom sends one payment item with the whole amount. You can change the items in `CheckoutEvents.PaymentOrderItemsPreparingAsync`. See [What Ekom does when you submit payment](../guides/checkout.md#what-ekom-does-when-you-submit-payment). For each item the package sends:
  - `unitPrice` and `units`: the line total divided by `Quantity` when that gives a whole number of minor units. Otherwise `units` is 1 and `unitPrice` is the line total.
  - `vatPercentage`: worked out from the item's VAT amount as `VAT / (GrandTotal - VAT) * 100`, rounded to one decimal. `VAT` must be the VAT included in `GrandTotal`. When either is zero or less, the rate is 0.
  - `productCode`: the item's position, starting at 0.
  - `description`: the item's title.

  If the items do not add up to the total after rounding, the package changes the last item to make up the difference and sets its `units` to 1.
- **The amount must match.** For an `ok` answer, `checkout-amount` must equal the sum of the payment items saved with the payment, in minor units. Otherwise nothing is recorded and the customer sees HTTP 400.
- **A pending payment ends on the cancel URL.** For `pending` and `delayed`, the customer is sent to the cancel URL, which releases the stock holds and makes the basket editable again. If PayTrail later reports the payment as `ok`, Ekom records it and opens an order review. See [A payment that arrives after its holds were released](../guides/checkout.md#a-payment-that-arrives-after-its-holds-were-released).
- **The settings are saved with each payment.** The callback checks the answer with the secret key that was in use when the payment started, not the current one. The saved settings include `secretKey`, in the `EkomOrderPayments` table.
- **The transaction ID from the create step is not stored.** The package logs it. The transaction ID on the order comes from PayTrail's answer.
- **Not implemented:** refunds, token payments, rendering PayTrail's payment method buttons, and payment reports.
- **Error emails.** When the response controller throws and `Ekom:Payments:SendEmailAlerts` is `true` (the default), Ekom emails the exception and PayTrail's values, without the signature, through the Umbraco SMTP settings.
- **Old events.** `Events.Success` and `Events.Error` are no longer raised. Use `OrderEvents.PaymentRecordedAsync`. See [React to recorded payments](../guides/payments.md#react-to-recorded-payments).

## Related pages

- [Payments](../guides/payments.md): what Ekom stores for a payment and how the payment status is worked out.
- [Payment and shipping providers](../guides/providers.md): create the provider node and fill in its settings.
- [Checkout](../guides/checkout.md): how a payment is started, returned and completed.
- [Order reviews](../guides/order-reviews.md): orders whose payment a person must check.
- [Payment gateways](README.md): all gateways Ekom supports.

[Back to Payment gateways](README.md)
