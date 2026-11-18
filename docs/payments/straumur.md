# Straumur

[Ekom docs](../README.md) / [Payment gateways](README.md) / Straumur

Straumur takes card payments on its hosted checkout page. Ekom creates the checkout at Straumur, sends the customer there, and records what Straumur reports in its webhooks. The package is `Ekom.Payments.Straumur`.

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

- Ekom 1.2 or later, and `Ekom.Payments.Straumur` 1.1.0 or later. The package targets .NET 8.
- A Straumur merchant account. From Straumur you need the terminal identifier, the API key and the HMAC key, for the staging environment and for production.

A webhook is a call that Straumur's server makes to your site when something happens to a payment. Ekom completes the order from the webhook, so your site must be reachable from the internet.

## Set it up

1. Install the package in the site project:

   ```bash
   dotnet add package Ekom.Payments.Straumur
   ```

2. In Umbraco, create a payment provider node under the store's payment provider container and set its `basePaymentProvider` to `straumur`. Fill in `successUrl`, `errorUrl` and `cancelUrl`. See [Set up a payment provider](../guides/providers.md#set-up-a-payment-provider).
3. Fill in the settings in the [Settings](#settings) table, on the node or in configuration.
4. Register this address as your webhook address at Straumur:

   ```text
   https://<your site>/ekom/payments/straumurresponse
   ```

   Ekom does not send a webhook address with the payment, so Straumur only calls your site when the address is registered.

## Settings

The setting names are the properties of `StraumurSettings` in camelCase. Ekom reads each one from code, then the provider node, then `Ekom:Payments:straumur`. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

| Alias | Required | Default | What it does |
| --- | --- | --- | --- |
| `terminalIdentifier` | Yes | none | Your Straumur terminal identifier. The payment request fails when it is empty. |
| `apiKey` | Yes | none | Your Straumur API key. Ekom sends it in the `X-API-key` header. The payment request fails when it is empty. |
| `hmacKey` | Yes | none | Your Straumur HMAC key, as a hexadecimal string. Ekom checks every webhook with it. Webhooks are refused when it is empty. |
| `paymentPageUrl` | Yes | none | The address of Straumur's hosted checkout API. Ekom posts the payment request there. See [Test it](#test-it) for the two addresses. |
| `checkoutExpiresInMinutes` | No | none | How many minutes the customer has to finish the checkout. Must be greater than `0`. When empty, Ekom sends no expiry time. |
| `addOrderToReference` | No | none | `true`: the reference sent to Straumur starts with the order line titles, separated by `;`, and ends with the Ekom payment ID. Only the exact text `true` turns it on. |
| `useDelayedCapture` | No | `false` | `true`: Straumur reserves the money first and captures it later. See [Capture, refund and cancel](#capture-refund-and-cancel). |
| `delayedCaptureDateTime` | No | none | With `useDelayedCapture`, the date and time in UTC when Straumur captures the payment. |
| `recurringProcessingModel` | No | none | `CardOnFile` or `Subscription`, for recurring payments. See [Limits and things to know](#limits-and-things-to-know). |

No Straumur setting holds a separate value per store or per language. To use different values for two stores, create a provider node for each and give each node its own `configurationKey`. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

The misspelled alias `terminalIdenitifer` is still read when `terminalIdentifier` is empty.

An `appsettings.json` example with placeholders:

```json
{
  "Ekom": {
    "Payments": {
      "straumur": {
        "terminalIdentifier": "<terminal identifier>",
        "apiKey": "<api key>",
        "hmacKey": "<hmac key>",
        "paymentPageUrl": "https://checkout-api.staging.straumur.is/api/v1/hostedcheckout",
        "checkoutExpiresInMinutes": 30
      }
    }
  }
}
```

Keep `apiKey` and `hmacKey` out of source control, for example in user secrets or the environment variables `Ekom__Payments__straumur__apiKey` and `Ekom__Payments__straumur__hmacKey`.

## What the customer sees

1. The customer submits payment. Ekom sets the order to `WaitingForPayment` and posts the payment request to `paymentPageUrl`. Straumur answers with the address of a hosted checkout page.
2. Ekom returns an HTML page that sends the browser to that address.
3. The customer pays on Straumur's page.
4. Straumur calls `/ekom/payments/straumurresponse` on your site. Ekom checks the call and records the payment, and the recorded payment completes the order.
5. Straumur sends the customer to your `successUrl`, with `orderId` and `reference` (the Ekom payment ID) in the query string.
6. If the customer leaves the checkout, Straumur sends them to Ekom's `/ekom/checkout/payment-return`. Ekom releases the order's holds and sends the customer to your `cancelUrl`.

The success page does not complete the order. The webhook in step 4 does. For step 6, see [When the customer cancels or the gateway reports an error](../guides/checkout.md#when-the-customer-cancels-or-the-gateway-reports-an-error). Ekom does not send an error address to Straumur.

If Straumur answers the payment request with an error status, Ekom logs the status and the response body, and the checkout fails. See [If sending to payment fails](../guides/checkout.md#if-sending-to-payment-fails).

## What Ekom records

Ekom sends Straumur the Ekom payment ID as the reference. Straumur returns it in the webhook as `merchantReference`. The webhook's `hmacSignature` must match an HMAC-SHA256, signed with `hmacKey`, of `checkoutReference`, `payfacReference`, `merchantReference`, `amount`, `currency`, `reason` and `success` joined by `:`.

The event type is in `additionalData.eventType`. A webhook without it counts as `Authorization`.

| Straumur webhook | What Ekom records |
| --- | --- |
| `Authorization`, successful | A `Captured` transaction. With `useDelayedCapture`, an `Authorized` transaction instead. With a `recurringProcessingModel`, nothing: the next event confirms the payment. |
| `Capture`, successful | A `Captured` transaction. |
| `Tokenization`, successful | A `Captured` transaction. |
| `Refund`, successful | A `Refunded` transaction for the amount in the webhook. Straumur sends it in minor units, and Ekom divides it by 100. |
| `Authorization` or `Capture` with `success` set to `false` | A `Failed` transaction, with Straumur's `reason` as the message. Ekom answers HTTP 200 with the text `Payment not Valid`. |
| Any other event, or a failed `Refund` | Nothing. Ekom answers HTTP 200. |
| The webhook cannot be verified | Nothing. Ekom logs an error and answers HTTP 500. |
| `merchantReference` holds no payment ID | Nothing. Ekom answers HTTP 400. |
| The payment ID is unknown, or belongs to another gateway | Nothing. Ekom answers HTTP 404. |
| The customer leaves the checkout | Nothing. The payment stays `Pending`. |

Captures and authorizations are recorded for the order amount of the payment. A capture is recorded once per payment, however often Straumur sends it.

Each transaction holds Straumur's `payfacReference` as the transaction ID, the authorization code, the masked card number, the payment method, and the details `event`, `checkoutReference`, `threeDAuthenticated` and `cardUsage`. Ekom then works out the payment status, see [How the payment status is worked out](../guides/payments.md#how-the-payment-status-is-worked-out). The raw data is the webhook without its signature and without the card token.

If recording the payment throws an exception, Ekom logs it, answers with an error and sends an alert email when `Ekom:Payments:SendEmailAlerts` is `true`. See [Payments configuration](../reference/configuration-reference.md#payments).

To run code when the payment is recorded, subscribe to `OrderEvents.PaymentRecordedAsync`. See [React to recorded payments](../guides/payments.md#react-to-recorded-payments). The package's own `Events.Success` and `Events.Error` are no longer raised.

## Capture, refund and cancel

The package cannot start a capture, a refund or a cancellation from Ekom. Do these at Straumur. What Ekom records afterwards depends on the webhook Straumur sends:

- **Capture.** Without `useDelayedCapture`, the payment is recorded as captured when it is authorized. With `useDelayedCapture`, Ekom records `Authorized` when the payment is authorized and `Captured` when the `Capture` webhook arrives. When `delayedCaptureDateTime` is in the future, Ekom sends Straumur the number of whole hours until that time as the capture delay. The order is completed by the authorization, see [When a payment completes the order](../guides/payments.md#when-a-payment-completes-the-order).
- **Refund.** A refund made at Straumur is recorded when its `Refund` webhook arrives, so the payment status becomes `PartiallyRefunded` or `Refunded`.
- **Cancel or void.** The package handles no cancellation event. If you cancel an authorization at Straumur, the order stays `Authorized` in Ekom.

## Test it

Point `paymentPageUrl` at Straumur's staging API and use the staging terminal identifier, API key and HMAC key from Straumur:

| Environment | `paymentPageUrl` |
| --- | --- |
| Staging | `https://checkout-api.staging.straumur.is/api/v1/hostedcheckout` |
| Production | `https://greidslugatt.straumur.is/api/v1/hostedcheckout` |

There is no separate test mode setting. The address and the keys decide where the payment goes.

Straumur's server must be able to reach `/ekom/payments/straumurresponse` on your site, and that address must be registered at Straumur. A site that runs only on your own machine does not receive webhooks, so the order is not completed.

## Limits and things to know

- **Whole units only.** Ekom cuts the decimals off the total and off each line before it multiplies by 100: `19.99` is sent as `1900`. Ekom still records the full amount. Use this gateway with a currency without decimals, such as ISK, or with prices that have no decimals.
- **Language.** Ekom sends `is` or `en`, taken from the node's `language` value. Any other language is sent as `is`.
- **Delayed capture uses a fixed time.** `delayedCaptureDateTime` is one date and time, not a delay. Ekom sends the whole hours between now and that time, and sends nothing when the time has passed. To capture a set time after each order, set it per payment in a `CheckoutEvents.PayAsync` handler with `paymentSettings.SetStraumurSettings(...)`.
- **Recurring payments.** With `CardOnFile`, Ekom asks Straumur for an amount of `0`, and records a capture of the full order total when the `Tokenization` or `Capture` webhook arrives. The order then shows as paid although no money was taken. With `Subscription`, Ekom asks for the full total. Ekom does not store the card token Straumur sends, so the package cannot charge the card again. `UnscheduledCardOnFile` is accepted but changes nothing: Ekom sends a normal payment.
- **Order lines.** Ekom sends one line per payment item. By default that is one line with the order title and the total. To change the lines, use `CheckoutEvents.PaymentOrderItemsPreparingAsync`.
- **Settings are saved with the payment.** Ekom stores the Straumur settings, `apiKey` and `hmacKey` included, with each payment in the `EkomOrderPayments` table. It checks each webhook with the `hmacKey` that was used when the payment started, so a new key only affects new payments.
- **Content Security Policy.** The page that sends the customer to Straumur runs a short script. If your site sets a CSP nonce, put it in `HttpContext.Items` under `CspNonce`, or under the key named in `Ekom:CspKey`, and Ekom adds it to the script.

## Related pages

- [Payments](../guides/payments.md): what Ekom stores for a payment and how the payment status is worked out.
- [Payment and shipping providers](../guides/providers.md): create the payment provider node.
- [Checkout](../guides/checkout.md): how a payment is started and how a paid order is completed.
- [Order reviews](../guides/order-reviews.md): orders whose payment a person must check.
- [Configuration reference](../reference/configuration-reference.md#payments): `Ekom:Payments` settings.

[Back to Payment gateways](README.md)
