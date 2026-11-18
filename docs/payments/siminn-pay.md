# Siminn Pay

[Ekom docs](../README.md) / [Payment gateways](README.md) / Siminn Pay

Siminn Pay is an Icelandic payment app. Ekom creates a payment order at Siminn Pay for the customer's phone number, the customer accepts it in the Siminn Pay app on their phone, and Siminn Pay tells your site the result. The package is `Ekom.Payments.SiminnPay`.

On this page:

- [Before you start](#before-you-start)
- [Set it up](#set-it-up)
- [Settings](#settings)
- [What the customer sees](#what-the-customer-sees)
- [Show the payment status on the success page](#show-the-payment-status-on-the-success-page)
- [What Ekom records](#what-ekom-records)
- [Capture, refund and cancel](#capture-refund-and-cancel)
- [Test it](#test-it)
- [Limits and things to know](#limits-and-things-to-know)
- [Related pages](#related-pages)

## Before you start

You need:

- Ekom 1.2 or later.
- The package `Ekom.Payments.SiminnPay`.
- From Siminn Pay: the address of its API, an API key, and the shared secret that Siminn Pay signs its notifications with.
- A site that Siminn Pay's servers can reach. Ekom records the payment only from Siminn Pay's notification.
- A checkout that collects the customer's phone number. Siminn Pay sends the payment request to that number.

## Set it up

1. Install the package in your site project:

   ```bash
   dotnet add package Ekom.Payments.SiminnPay
   ```

2. Create a payment provider node under the store's payment provider container, and set its `basePaymentProvider` property to `siminnPay`. If `basePaymentProvider` is empty, Ekom uses the node name, so a node named `siminnPay` also works. See [Set up a payment provider](../guides/providers.md#set-up-a-payment-provider) for the node, its success URL, and its zone and amount range.
3. Fill in the settings in the table below, on the provider node or in configuration. `secret` must be on the node or in configuration, not set in code. See [Limits and things to know](#limits-and-things-to-know).
4. Make the success page show the payment status while the customer answers in the app. See [Show the payment status on the success page](#show-the-payment-status-on-the-success-page).

You do not register a notification address at Siminn Pay. Ekom sends it with every payment order, as `callbackUrl`. The address is:

```text
https://<your site>/ekom/payments/siminnpayresponse
```

Ekom builds it from the scheme and host of the checkout request. When `PaymentSettings.ReportUrl` has a value, for example set in a `CheckoutEvents.PayAsync` handler, Ekom sends that address instead.

## Settings

Ekom reads each setting from the property with that alias on the provider node, then from `Ekom:Payments:siminnPay` in configuration. A value set in code with `paymentSettings.SetSiminnPaySettings(...)` in a `CheckoutEvents.PayAsync` handler comes first. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

| Alias | Required | Default | What it does |
| --- | --- | --- | --- |
| `apiUrl` | Yes | None | The base address of the Siminn Pay API. End it with `/`: Ekom adds paths such as `paymentorder/order` to it. |
| `apiKey` | Yes | None | Your Siminn Pay API key. Ekom exchanges it for an access token before each call to the API. |
| `secret` | Yes | None | The shared secret for notifications. Ekom checks the `HMAC` of each notification with it. Without it, every notification is refused and no payment is recorded. |
| `restrictToLoan` | No | `false` | Sent to Siminn Pay as `restrictToLoan`. |
| `paymentType` | No | `loan` | Sent to Siminn Pay as `paymentType`. |
| `timeToLive` | No | `60` | Sent to Siminn Pay as `timeToLive`. A value of `0` or less sends `60`. |
| `currency` | No | `ISK` | Sent to Siminn Pay as `currency`. |

None of these settings hold one value per store or per language. A node has one value of each.

To change `paymentType`, `timeToLive` or `currency`, set them in code:

```csharp
using Ekom.Events;
using Ekom.Payments.SiminnPay;
using Ekom.Payments.SiminnPay.Model;

CheckoutEvents.PayAsync += (sender, args, ct) =>
{
    args.PaymentSettings.SetSiminnPaySettings(new SiminnPaySettings
    {
        TimeToLive = 120,
    });
    return Task.CompletedTask;
};
```

The empty settings, such as `apiKey`, are still filled from the node and configuration.

The settings class has more properties: `terminal`, `baseAddress`, `hostOverride`, `paymentFormUrl`, `customerInformationSharedSecret` and `referenceId`. The package does not use them.

Keep the key and the secret out of source control, for example in user secrets or environment variables:

```json
{
  "Ekom": {
    "Payments": {
      "siminnPay": {
        "apiUrl": "https://siminn-pay-api.example/",
        "apiKey": "your-siminn-pay-api-key",
        "secret": "your-siminn-pay-shared-secret"
      }
    }
  }
}
```

## What the customer sees

1. The customer submits payment in your checkout. Ekom needs the customer's phone number, `CustomerInfo.PhoneNumber`. Without it, checkout fails.
2. Ekom creates a payment order at Siminn Pay with the amount, the title of the first payment item as description, the order ID as `referenceId`, and the phone number as the recipient, with a notification to the phone.
3. Ekom stores a new payment, with Siminn Pay's order key as its reference.
4. Ekom sends the customer straight to the provider node's `successUrl`, with `orderId=<order id>` and `siminnPayOrderKey=<order key>` added. The customer has not paid yet.
5. The customer accepts or declines the payment in the Siminn Pay app.
6. Siminn Pay sends a notification to `/ekom/payments/siminnpayresponse`. Ekom verifies it, records the outcome and, for a payment, completes the order.

The customer never leaves your site for a payment page, and the package uses neither the cancel URL nor the error URL. Your success page tells the customer what happened.

If Siminn Pay does not know the phone number, it answers with HTTP 404 and the package throws `SiminnPayApiUnknownPhoneException`, so checkout fails. See [If sending to payment fails](../guides/checkout.md#if-sending-to-payment-fails).

### How the redirect reaches the browser

The package does not return a page. It sets an HTTP redirect on the checkout response and returns an empty string.

- With the form endpoint `/ekom/mvcCheckout/pay`, or with your own controller that returns an empty result for status `230` and an empty body, the browser follows the redirect. See the [storefront example](../examples/storefront.md#take-the-customer-through-checkout).
- With the JSON endpoint `/ekom/checkout/pay`, Ekom replaces the redirect status with `230` and the body is an empty JSON string, so the browser does not follow it.

## Show the payment status on the success page

While the customer answers in the app, the success page can ask Ekom for the status:

```http
GET /ekom/payments/siminnpayresponse/status?siminnPayOrderKey=<order key>
```

Ekom asks Siminn Pay for the order's status and returns JSON with these values:

| Value | What it is |
| --- | --- |
| `PayStatus` | Siminn Pay's status code. See the table below. |
| `Amount` | The payment amount. |
| `CreatedAt` | When the payment was requested, in UTC. |
| `Expires` | `CreatedAt` plus 1 minute. It does not follow `timeToLive`. |
| `NetPaymentOrderId` | The Ekom payment ID. |

The names follow your site's JSON settings, so they can arrive in camelCase.

The endpoint answers HTTP 400 without `siminnPayOrderKey`, and HTTP 404 when the order key does not belong to a Siminn Pay payment on this site. It reads the status only. It records nothing, so the order is completed by the notification, not by this request.

Siminn Pay status codes:

| Code | Name | Meaning |
| --- | --- | --- |
| `0` | `PaymentSuccessful` | The customer paid. |
| `201` | `WaitingForCustomer` | Created, waiting for the customer. |
| `211` | `CustomerAcceptedWaitingForConfirm` | The customer accepted, waiting for confirmation. |
| `301` | `WaitingForAcquirer` | Waiting for the payment to be accepted. |
| `121` | `WaitingForNewAmount` | Waiting for an updated amount. |
| `202` | `CancelledByCustomer` | The customer cancelled. |
| `102` | `CancelledByMerchant` | The merchant cancelled. |
| `402` | `Expired` | The payment order expired. |
| `303` | `AcquirerError` | The payment was rejected. |
| `4` | `UnknownError` | Siminn Pay reported an unknown error. |

## What Ekom records

Siminn Pay sends its notification to `/ekom/payments/siminnpayresponse`. Ekom finds the payment by Siminn Pay's `orderKey` and checks the notification before it records anything:

- `HMAC` must equal HMAC-SHA256, with `secret` as the key, of the order key, the amount as a whole number with decimals cut off, and `expiresAt` formatted as `dd.MM.yyyy HH:mm:ss`. Upper and lower case are ignored.
- `amount` must equal the requested amount exactly.

| Siminn Pay status | Recorded on the order |
| --- | --- |
| `PaymentSuccessful` | `Captured`, for the full payment amount |
| `CancelledByCustomer` | `Cancelled`, "Cancelled by the customer" |
| `CancelledByMerchant` | `Cancelled`, "Cancelled by the merchant" |
| `Expired` | `Failed`, "The payment request expired" |
| `AcquirerError` | `Failed`, "The acquirer reported an error" |
| `UnknownError` | `Failed` |
| Any other status | Nothing. The customer has not answered yet. |

A `Captured` record completes the order. See [When a payment completes the order](../guides/payments.md#when-a-payment-completes-the-order). A payment is captured once, however often Siminn Pay sends the notification.

Each record holds:

- **Transaction ID:** `transactionDetails.transactionId` from the notification, when it has one.
- **Details:** `orderKey` (Siminn Pay's order key, which staff can copy) and `status` (the Siminn Pay status name).

How Ekom answers notifications:

| Situation | Answer |
| --- | --- |
| The body is missing or not valid | HTTP 400 |
| The order key does not belong to a Siminn Pay payment on this site | HTTP 404 |
| No `secret` is set, or `HMAC` does not match | HTTP 401, nothing recorded |
| The amount differs from the requested amount | HTTP 400, nothing recorded |
| A status that is not final | HTTP 200, nothing recorded |
| Recorded | HTTP 200 |
| Recording fails with an error | An error. Ekom logs it and, when `Ekom:Payments:SendEmailAlerts` is `true`, emails it. |

## Capture, refund and cancel

The package creates payment orders and records Siminn Pay's notifications. It cannot capture, refund or cancel a Siminn Pay payment from Ekom. Do that with Siminn Pay.

A cancellation that Siminn Pay reports as `CancelledByMerchant` is recorded as `Cancelled`. Ekom is not told about refunds. To show one on the order, set the payment status by hand in the Manager. That changes the record only and moves no money.

`SiminnPayService` in the package also has `DeleteOrder` and `CalculateLoan` methods. Ekom does not call them.

## Test it

The package has no test mode setting. Ekom calls the API at `apiUrl` with `apiKey`, so point both at the Siminn Pay environment you test against, and use a phone number that environment knows.

Siminn Pay must reach the notification address, and Ekom builds it from the host of the checkout request. On your own computer, open the checkout through a public address that forwards to your site, or set `PaymentSettings.ReportUrl` to such an address.

After a test payment, look for the log line `SiminnPay Response - PaymentSuccessful recorded as Captured` and check the payment in the order drawer in the Manager.

## Limits and things to know

- **The secret is read when the notification arrives.** Ekom does not store Siminn Pay's settings with the payment. It reads them again from the provider node and configuration for each notification and status request. A `secret` or `apiKey` set only in code is missing then, and every notification is refused.
- **Exact amounts.** Ekom sends the order total as it is, without rounding, and refuses a notification whose amount differs by any amount.
- **Phone number.** The phone number must be registered with Siminn Pay. Ekom does not check its format.
- **The order ID is the reference.** Siminn Pay gets the Ekom order ID as `referenceId`. Ekom finds the payment by Siminn Pay's order key, so a new attempt for the same order does not mix with an older one.
- **Old events.** `Ekom.Payments.SiminnPay.Model.Events.Success` and `Events.Error` are obsolete and never raised. Use `OrderEvents.PaymentRecordedAsync`. See [React to recorded payments](../guides/payments.md#react-to-recorded-payments).

## Related pages

- [Payments](../guides/payments.md): how Ekom stores payments and works out the payment status.
- [Payment and shipping providers](../guides/providers.md): create the provider node and where settings come from.
- [Checkout](../guides/checkout.md): what happens when the customer submits payment.
- [Storefront example](../examples/storefront.md#show-the-receipt-after-payment): a receipt page after payment.
- [Configuration reference](../reference/configuration-reference.md#payments): `Ekom:Payments` settings, including email alerts.
- [Payment gateways](README.md): all gateway packages.

[Back to Payment gateways](README.md)
