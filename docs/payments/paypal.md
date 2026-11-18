# PayPal

[Ekom docs](../README.md) / [Payment gateways](README.md) / PayPal

PayPal takes payment from the customer's PayPal account. Ekom sends the customer to PayPal to approve the payment, and takes the payment when PayPal sends the customer back to your site. The package is `Ekom.Payments.PayPal`.

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

- Ekom 1.2 or later.
- The package `Ekom.Payments.PayPal`.
- A PayPal REST app, and its client ID and secret. Sandbox apps and live apps have different values.
- For webhooks: a webhook on the same app, and its webhook ID. A webhook is a request that PayPal sends to your site when something happens to a payment.
- A store that charges in a currency PayPal takes. ISK is not one of them. See [Limits and things to know](#limits-and-things-to-know).

Webhooks are optional, but without them:

- a customer who approves the payment and never comes back to your site is not charged,
- a payment that PayPal holds for review is never recorded,
- captures of delayed payments, refunds and reversals made at PayPal are not recorded.

## Set it up

1. Install the package in your site project:

   ```bash
   dotnet add package Ekom.Payments.PayPal
   ```

2. Create a payment provider node under the store's payment provider container, and set its `basePaymentProvider` property to `payPal`. Ekom ignores upper and lower case in the name. If `basePaymentProvider` is empty, Ekom uses the node name, so a node named `payPal` also works. See [Set up a payment provider](../guides/providers.md#set-up-a-payment-provider) for the node, its success, error and cancel URLs, and its zone and amount range.
3. Fill in the settings in the table below, on the node or in configuration. Put `clientSecret` on the node or in configuration, not only in code.
4. In your PayPal app, add a webhook with this address:

   ```text
   https://<your site>/ekom/payments/paypalresponse/webhook
   ```

   Subscribe it to these events:

   - `CHECKOUT.ORDER.APPROVED`
   - `PAYMENT.CAPTURE.COMPLETED`
   - `PAYMENT.CAPTURE.DECLINED`
   - `PAYMENT.CAPTURE.DENIED`
   - `PAYMENT.CAPTURE.PENDING`
   - `PAYMENT.CAPTURE.REFUNDED`
   - `PAYMENT.CAPTURE.REVERSED`
   - `PAYMENT.AUTHORIZATION.CREATED`
   - `PAYMENT.AUTHORIZATION.VOIDED`

5. Copy the webhook's ID into the `webhookId` setting.

You do not register the return address `/ekom/payments/paypalresponse` at PayPal. Ekom sends it with each payment.

## Settings

Ekom reads each setting from the property with that alias on the provider node, then from `Ekom:Payments:payPal` in configuration. A value set in code with `paymentSettings.SetPayPalSettings(...)` in a `CheckoutEvents.PayAsync` handler comes first. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

| Alias | Required | Default | What it does |
| --- | --- | --- | --- |
| `clientId` | Yes | None | The client ID of the PayPal REST app. If it is empty, checkout fails before the customer is sent to PayPal. |
| `clientSecret` | Yes | None | The secret of the PayPal REST app. It is not stored with the payment. Ekom reads it again from the node or configuration when the customer returns and for each webhook, so a value set only in code is missing there. |
| `webhookId` | No | Empty | The ID of the PayPal webhook. Ekom uses it to ask PayPal to verify each webhook. Empty: Ekom answers every webhook with HTTP 500 and records nothing. |
| `sandbox` | No | `false` | `true`: use PayPal's sandbox (`api-m.sandbox.paypal.com`). `false`: use live PayPal (`api-m.paypal.com`). |
| `useDelayedCapture` | No | `false` | `true`: PayPal only reserves the amount. Ekom records the payment as authorized, and as captured when you capture it at PayPal. |
| `brandName` | No | Empty | The shop name on the PayPal pages. Empty: PayPal shows the name of the PayPal account. |
| `locale` | No | Empty | The language of the PayPal pages as a BCP 47 tag, for example `en-GB` or `da-DK`. Empty: Ekom sends no language, and PayPal chooses. |

None of these settings hold one value per store or per language. A node has one value of each. To use two PayPal apps, create two provider nodes, and give each its own `configurationKey` if the values come from configuration.

Ekom stores every setting except `clientSecret` with the payment when it is created. The return and the webhooks of that payment use the stored values. A setting that was empty or `false` when the payment was created, and `clientSecret`, are read again from the node or configuration.

Keep the secret out of source control, for example in user secrets or environment variables:

```json
{
  "Ekom": {
    "Payments": {
      "payPal": {
        "clientId": "<client id>",
        "clientSecret": "<client secret>",
        "webhookId": "<webhook id>",
        "sandbox": true,
        "brandName": "My Shop"
      }
    }
  }
}
```

The success, error and cancel URLs and the currency are not PayPal settings. They come from the provider node and the store, as for every gateway.

## What the customer sees

1. The customer submits payment in your checkout. Ekom stores a new payment, and the package creates a PayPal order for the total. A PayPal order is PayPal's record of what the customer is asked to approve. It carries the Ekom payment ID.
2. The browser is sent to PayPal. The customer sees the order name or order number and the total, not the order lines. PayPal does not ask for a delivery address, because Ekom already has it.
3. The customer approves the payment at PayPal. Ekom asks PayPal for the pay-now flow (`user_action` `PAY_NOW`), so the customer confirms the payment at PayPal and there is no review step back in your shop.
4. PayPal sends the customer to `/ekom/payments/paypalresponse`. Ekom reads the PayPal order, checks that it was created for this payment, and captures it. With `useDelayedCapture` on, Ekom authorizes it instead.
5. Where the customer goes next:
   - Captured, authorized, or held by PayPal for review: the provider node's `successUrl`, with `orderId=<order id>` and `reference=<payment id>` added.
   - PayPal refused the payment, the order was not approved, or PayPal could not be reached: Ekom's payment return address with `outcome=error`. Ekom releases the stock holds of the attempt and sends the customer on to the node's `errorUrl`.

If the customer cancels at PayPal, PayPal sends them to Ekom's payment return address with `outcome=cancel`. Ekom releases the holds and sends the customer on to the node's `cancelUrl`. See [When the customer cancels or the gateway reports an error](../guides/checkout.md#when-the-customer-cancels-or-the-gateway-reports-an-error).

If PayPal refuses to create the order, the checkout request fails. See [If sending to payment fails](../guides/checkout.md#if-sending-to-payment-fails).

## What Ekom records

Ekom records an outcome in two places: when the customer returns to `/ekom/payments/paypalresponse`, and when a webhook arrives at `/ekom/payments/paypalresponse/webhook`.

When the customer returns:

| What happens | Recorded on the order |
| --- | --- |
| PayPal captures the payment | `Captured`, with the captured amount |
| PayPal authorizes the payment (`useDelayedCapture` on) | `Authorized`, with the authorized amount |
| PayPal refuses to take the payment (HTTP 422, for example a declined card) | `Failed`, with PayPal's error |
| PayPal declines the capture or denies the authorization | `Failed` |
| PayPal holds the payment for review | Nothing. The webhook records the outcome. |
| The order is not approved, was not created for this payment, or PayPal cannot be reached | Nothing |

From webhooks:

| PayPal event | Recorded on the order |
| --- | --- |
| `CHECKOUT.ORDER.APPROVED` | Ekom takes the payment as if the customer had returned, and records it as in the table above. |
| `PAYMENT.CAPTURE.COMPLETED` | `Captured`, with the captured amount |
| `PAYMENT.CAPTURE.DECLINED`, `PAYMENT.CAPTURE.DENIED` | `Failed` |
| `PAYMENT.CAPTURE.PENDING` | Nothing. A later event records the outcome. |
| `PAYMENT.CAPTURE.REFUNDED` | `Refunded`, with the amount of this refund |
| `PAYMENT.CAPTURE.REVERSED` | `Refunded`, with the reversed amount and the message "PayPal reversed the payment" |
| `PAYMENT.AUTHORIZATION.CREATED` | `Authorized`. `Failed` when PayPal denied the authorization. |
| `PAYMENT.AUTHORIZATION.VOIDED` | `Voided` |

A `Captured` or `Authorized` record completes the order. See [When a payment completes the order](../guides/payments.md#when-a-payment-completes-the-order).

Each record holds:

- **Transaction ID:** PayPal's ID of the capture, authorization or refund.
- **Payment method:** `PayPal`.
- **Details:** `event` (the PayPal event type, or "Customer returned from PayPal"), `payPalTransaction`, `payPalOrder` (on records made when the customer returns) and `status` (PayPal's status).
- **Raw data:** the event ID and type, the PayPal order ID, and the transaction's ID, status, reason, amount and currency. Nothing about the customer is kept.

A payment is captured once, however often the customer opens the return address or PayPal delivers its events. Each refund is recorded once.

Before it records a webhook, Ekom asks PayPal whether PayPal sent it, with the payment's `webhookId`. How Ekom answers webhooks:

| Situation | Answer |
| --- | --- |
| The body is not a PayPal event | HTTP 400 |
| An event type Ekom does not handle, or an event without an Ekom payment ID | HTTP 200, nothing recorded |
| The payment ID is not in this site's database, or the payment is not a PayPal payment | HTTP 404 |
| The payment has no `webhookId` | HTTP 500 |
| PayPal does not confirm the event | HTTP 400 |
| Recording fails with an error | HTTP 500. Ekom logs the error and, when `Ekom:Payments:SendEmailAlerts` is `true`, emails it. PayPal delivers the event again. |

The return address answers HTTP 400 when `paymentId` or `token` is missing or not valid, and HTTP 404 when the payment is not found or is not a PayPal payment.

## Capture, refund and cancel

The package only starts payments, takes approved payments, and records what PayPal reports. You cannot capture, refund or void a PayPal payment from Ekom. Do it at PayPal, and the webhook records the result:

- **Capture** a payment taken with `useDelayedCapture`: `PAYMENT.CAPTURE.COMPLETED` records `Captured`. Capture before the authorization expires.
- **Void** an authorization: `PAYMENT.AUTHORIZATION.VOIDED` records `Voided`.
- **Refund** a captured payment, in full or in part: `PAYMENT.CAPTURE.REFUNDED` records `Refunded` with the amount of that refund.

Without the webhook, none of these reach Ekom. Setting a payment status by hand in the Manager does not move money at PayPal.

## Test it

1. Set `sandbox` to `true`.
2. Use the client ID, secret and webhook ID of a sandbox app. Live values do not work in the sandbox.
3. Pay with a sandbox buyer account.

PayPal's webhook simulator sends events that PayPal cannot verify, so Ekom records nothing from them. Test the webhook with real sandbox payments.

## Limits and things to know

- **Currency.** PayPal takes only [its own list of currencies](https://developer.paypal.com/api/rest/reference/currency-codes/), and ISK is not on it. The package does not check the currency before it calls PayPal. To offer PayPal to customers of an Icelandic shop, offer it on a store that charges in another currency, such as EUR or USD.
- **Ekom's currency list.** Ekom passes the store's currency to the gateway only when it is one of `ISK`, `USD`, `EUR`, `DKK`, `GBP`, `NOK`, `SEK`, `CHF`, `CAD`, `HUF`, `BHD`, `AUD`, `RUB`, `PLN`, `RON`, `HRK` or `CZK`. For any other currency Ekom logs an error and sends `ISK`, which PayPal refuses.
- **Amounts.** Ekom rounds the total half away from zero to 2 decimals, or to whole units for HUF, JPY and TWD. The rounded total is the amount stored with the payment.
- **Cancelled is never recorded.** A customer who leaves PayPal without approving is not recorded as cancelled, because PayPal does not report it.
- **Pending payments.** A payment that PayPal holds for review sends the customer to the success page, but the order is completed only when the webhook reports the outcome.
- **PayPal not reachable on return.** If PayPal cannot be reached while Ekom captures the payment, Ekom logs the error, emails it when `Ekom:Payments:SendEmailAlerts` is `true`, and sends the customer to the error page. Ekom releases the stock holds of the attempt. If the capture went through at PayPal, the webhook records it later, and the order gets a review. See [A payment that arrives after its holds were released](../guides/checkout.md#a-payment-that-arrives-after-its-holds-were-released).
- **Changing PayPal apps.** The `clientId` of a payment is the one stored with it, but `clientSecret` is read from the node or configuration each time. After you move to another app, or from sandbox to live, the return and the webhooks of payments started with the old app no longer authenticate at PayPal.
- **Another currency at PayPal.** If PayPal reports a capture or authorization in a different currency from the payment's, Ekom records the payment's own amount. A refund in a different currency is not recorded.
- **Content Security Policy.** The page that sends the customer to PayPal is a small inline script. If your site uses a nonce-based Content Security Policy, put the nonce in `HttpContext.Items` under `CspNonce`, or under the key set in `Ekom:CspKey`, and Ekom adds it to the script.

## Related pages

- [Payments](../guides/payments.md): how Ekom stores payments and works out the payment status.
- [Payment and shipping providers](../guides/providers.md): create the provider node and where settings come from.
- [Checkout](../guides/checkout.md): what happens when the customer submits payment, cancels or returns.
- [Order reviews](../guides/order-reviews.md): orders whose payment does not match, which a person must check.
- [Configuration reference](../reference/configuration-reference.md#payments): `Ekom:Payments` settings, including email alerts.
- [Payment gateways](README.md): all gateway packages.

[Back to Payment gateways](README.md)
