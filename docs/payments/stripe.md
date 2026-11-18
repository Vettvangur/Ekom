# Stripe

[Ekom docs](../README.md) / [Payment gateways](README.md) / Stripe

Stripe takes card payments and other payment methods on Stripe Checkout, a payment page that Stripe hosts. Ekom sends the customer to that page, and Stripe reports the outcome to your site by webhook. The package is `Ekom.Payments.Stripe`.

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
- The package `Ekom.Payments.Stripe`.
- A Stripe account with a secret API key. A secret key starts with `sk_live_` or `sk_test_`. A restricted key also works if it is allowed to write Checkout Sessions.
- A webhook endpoint in your Stripe account that points to your site, and its signing secret. A signing secret starts with `whsec_`. A webhook is a request that Stripe sends to your site when something happens to a payment.

The webhook is not optional. Ekom records a Stripe payment only from webhooks. Without a signing secret, checkout refuses to send the customer to Stripe.

## Set it up

1. Install the package in your site project:

   ```bash
   dotnet add package Ekom.Payments.Stripe
   ```

2. Create a payment provider node under the store's payment provider container, and set its `basePaymentProvider` property to `stripe`. If `basePaymentProvider` is empty, Ekom uses the node name, so a node named `stripe` also works. See [Set up a payment provider](../guides/providers.md#set-up-a-payment-provider) for the node, its success, error and cancel URLs, and its zone and amount range.
3. Fill in the settings in the table below, on the node or in configuration.
4. In Stripe, add a webhook endpoint with this address:

   ```text
   https://<your site>/ekom/payments/striperesponse
   ```

   Choose the **Snapshot** payload format. A snapshot event carries the whole object, and Ekom reads the payment from it. Ekom answers other events with HTTP 400.

   Subscribe the endpoint to these events:

   - `checkout.session.completed`
   - `checkout.session.async_payment_succeeded`
   - `checkout.session.async_payment_failed`
   - `checkout.session.expired`
   - `charge.captured`
   - `charge.refunded`
   - `charge.expired`

5. Copy the endpoint's signing secret into the `webhookSecret` setting.

## Settings

Ekom reads each setting from the property with that alias on the provider node, then from `Ekom:Payments:stripe` in configuration. A value set in code with `paymentSettings.SetStripeSettings(...)` in a `CheckoutEvents.PayAsync` handler comes first. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

| Alias | Required | Default | What it does |
| --- | --- | --- | --- |
| `secretKey` | Yes | None | The Stripe secret key (`sk_live_...` or `sk_test_...`), or a restricted key that may write Checkout Sessions. Ekom uses it only to create the Checkout Session. It is not stored with the payment. |
| `webhookSecret` | Yes | None | The signing secret (`whsec_...`) of the webhook endpoint. Ekom stores it with each payment and uses it to verify that payment's webhooks. If it is empty, checkout fails before the customer is sent to Stripe. |
| `useDelayedCapture` | No | `false` | `true`: Stripe only reserves the amount. Ekom records the payment as authorized, and as captured when you capture it at Stripe. |
| `checkoutExpiresInMinutes` | No | Empty | How many minutes the Checkout page stays open, from `30` to `1440`. A value outside that range makes checkout fail. Empty: Stripe's default of 24 hours. |
| `locale` | No | Empty | The language of the Checkout page, for example `en` or `da`. Ekom sends the value as it is. Empty: see [Limits and things to know](#limits-and-things-to-know). |
| `paymentMethodTypes` | No | Empty | Stripe payment method types, separated by commas, for example `card,klarna`. Empty: Ekom sends no list, and Stripe offers the payment methods turned on in your Stripe account. |

None of these settings hold one value per store or per language. A node has one value of each. To use two Stripe accounts, create two provider nodes, and give each its own `configurationKey` if the values come from configuration.

Keep the keys out of source control, for example in user secrets or environment variables:

```json
{
  "Ekom": {
    "Payments": {
      "stripe": {
        "secretKey": "sk_test_...",
        "webhookSecret": "whsec_...",
        "useDelayedCapture": false,
        "checkoutExpiresInMinutes": 60,
        "paymentMethodTypes": "card"
      }
    }
  }
}
```

The success, error and cancel URLs and the currency are not Stripe settings. They come from the provider node and the store, as for every gateway.

## What the customer sees

1. The customer submits payment in your checkout. Ekom stores a new payment and the package creates a Stripe Checkout Session for it.
2. The browser is sent to Stripe's Checkout page. The customer's email is filled in from the order.
3. The customer pays. A declined card shows an error on the Checkout page, and the customer can try again there until the page expires.
4. After payment, Stripe sends the customer to the provider node's `successUrl`, with `orderId=<order id>` and `reference=<payment id>` added.
5. Separately, Stripe sends a webhook to `/ekom/payments/striperesponse`. Ekom records the payment and completes the order. The webhook and the customer's redirect are independent, so the order can still be waiting for payment when the success page loads.

If the customer leaves the Checkout page with its back link, Stripe sends them to Ekom's payment return address. Ekom releases the stock holds of the attempt and sends the customer on to the node's `cancelUrl`. See [When the customer cancels or the gateway reports an error](../guides/checkout.md#when-the-customer-cancels-or-the-gateway-reports-an-error). The package does not give Stripe the error URL, so the customer never lands on it from Stripe.

If Stripe refuses to create the Checkout Session, the checkout request fails. See [If sending to payment fails](../guides/checkout.md#if-sending-to-payment-fails).

## What Ekom records

Ekom records only verified webhooks. A webhook is verified when its `Stripe-Signature` header matches the payment's `webhookSecret` and was made no more than 5 minutes before or after the time it arrives.

| Stripe event | Recorded on the order |
| --- | --- |
| `checkout.session.completed`, payment status `paid` or `no_payment_required` | `Captured` |
| `checkout.session.completed`, payment status `unpaid`, `useDelayedCapture` on | `Authorized` |
| `checkout.session.completed`, payment status `unpaid`, `useDelayedCapture` off | Nothing. The payment method confirms later, and Stripe sends one of the next two events. |
| `checkout.session.async_payment_succeeded` | `Captured` |
| `checkout.session.async_payment_failed` | `Failed` |
| `checkout.session.expired` | `Cancelled`. The Checkout page expired before the customer paid. |
| `charge.captured` | `Captured`, with the captured amount |
| `charge.refunded`, charge captured | `Refunded`, with the amount of this refund |
| `charge.refunded`, charge not captured | `Voided`. The reserved amount was released. |
| `charge.expired` | `Voided`. The reservation expired before it was captured. |

A `Captured` or `Authorized` record completes the order. See [When a payment completes the order](../guides/payments.md#when-a-payment-completes-the-order).

Each record holds:

- **Transaction ID:** the Stripe payment intent ID.
- **Details:** `event` (the Stripe event type), `paymentIntent`, `checkoutSession` or `charge` (the ID of the object the event is about), and `mode` (`Live` or `Test`).
- **Card brand, last four digits and payment method:** only from `charge.*` events. A Checkout Session does not carry them, so the first capture has no card details. Delayed captures and refunds have them.
- **Raw data:** the event's IDs, statuses and amounts. Nothing about the customer is kept.

A payment is captured once, however often Stripe delivers the events. Each refund is recorded once.

How Ekom answers webhooks:

| Situation | Answer |
| --- | --- |
| The body is not a Stripe event, for example a thin event | HTTP 400 |
| An event type Ekom does not handle, or an event without an Ekom payment ID | HTTP 200, nothing recorded |
| The payment ID is not in this site's database, or the payment is not a Stripe payment | HTTP 404 |
| The payment has no stored `webhookSecret` | HTTP 500 |
| The signature does not verify | HTTP 400 |
| Recording fails with an error | HTTP 500. Ekom logs the error and, when `Ekom:Payments:SendEmailAlerts` is `true`, emails it. Stripe delivers the event again. |

## Capture, refund and cancel

The package only starts payments and records what Stripe reports. You cannot capture, refund or void a Stripe payment from Ekom. Do it in your Stripe account or through Stripe's API, and the webhook records the result:

- **Capture** a payment taken with `useDelayedCapture`: `charge.captured` records `Captured` with the captured amount. Capture before the reservation expires. When it expires, `charge.expired` records `Voided`.
- **Release** a reserved amount: Stripe reports it as `charge.refunded` on a charge that was not captured, and Ekom records `Voided`.
- **Refund** a captured payment, in full or in part: `charge.refunded` records `Refunded` with the amount of that refund.

Setting a payment status by hand in the Manager does not move money at Stripe.

## Test it

1. Use a test key (`sk_test_...`) in `secretKey`. Each record's `mode` detail shows `Test` or `Live`, from the event.
2. To receive webhooks on your own computer, forward them with the Stripe CLI:

   ```bash
   stripe listen --forward-to https://localhost:<port>/ekom/payments/striperesponse
   ```

3. Put the signing secret that the command prints into `webhookSecret` before you start a test payment. Ekom stores the secret with each payment when it is created, so a payment started with another secret does not verify.

Each webhook endpoint has its own signing secret. Sites that share one Stripe account, such as a staging site and a local site on the same test keys, each need their own endpoint. Each site also receives the other site's events, and answers them with HTTP 404, because the payment is not in its database.

## Limits and things to know

- **Amounts.** Ekom sends amounts in the currency's minor unit, rounded half away from zero to 2 decimals. Zero-decimal currencies, such as JPY, are rounded to whole units. ISK is rounded to whole kronur and sent with two zero decimals, as Stripe requires.
- **Lines on the Checkout page.** Ekom lists the payment items when their rounded amounts add up to the payment total, and there are 1 to 100 of them. Otherwise, for example with a negative discount line, the customer sees one line with the order name and the total. By default Ekom sends one payment item, the payment title. See [What Ekom does when you submit payment](../guides/checkout.md#what-ekom-does-when-you-submit-payment).
- **Language.** When `locale` is empty, Ekom sends the two-letter language of the provider node's `language` value if Stripe Checkout has that language. Otherwise Ekom sends no language, and Stripe follows the customer's browser. Icelandic is not on Stripe's list, so with the default `is-IS` the browser decides.
- **Declined cards** are not recorded. The customer tries again on the same Checkout page.
- **Another currency at Stripe.** If Stripe reports a charge in a different currency from the payment's, a capture is recorded with the payment's own amount, and a refund is not recorded.
- **The signing secret of a payment does not change.** Ekom verifies every event of a payment with the `webhookSecret` that was in use when the payment started.
- **Content Security Policy.** The page that sends the customer to Stripe is a small inline script. If your site uses a nonce-based Content Security Policy, put the nonce in `HttpContext.Items` under `CspNonce`, or under the key set in `Ekom:CspKey`, and Ekom adds it to the script.

## Related pages

- [Payments](../guides/payments.md): how Ekom stores payments and works out the payment status.
- [Payment and shipping providers](../guides/providers.md): create the provider node and where settings come from.
- [Checkout](../guides/checkout.md): what happens when the customer submits payment, cancels or returns.
- [Order reviews](../guides/order-reviews.md): orders whose payment does not match, which a person must check.
- [Configuration reference](../reference/configuration-reference.md#payments): `Ekom:Payments` settings, including email alerts.
- [Payment gateways](README.md): all gateway packages.

[Back to Payment gateways](README.md)
