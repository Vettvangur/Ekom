# Valitor

[Ekom docs](../README.md) / [Payment gateways](README.md) / Valitor

Valitor takes card payments, and card loans when you turn them on. Ekom sends the customer to Valitor's hosted payment page, the customer pays there, and Valitor tells Ekom the result. The package is `Ekom.Payments.Valitor`.

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

- Ekom 1.2 or later, and `Ekom.Payments.Valitor` 1.1.0 or later. The package targets .NET 8.
- A Valitor merchant account. From Valitor you need the merchant ID and the verification code, for the test environment and for production. Card loans also need a merchant name, see `merchantName` below.

## Set it up

1. Install the package in the site project:

   ```bash
   dotnet add package Ekom.Payments.Valitor
   ```

2. In Umbraco, create a payment provider node under the store's payment provider container and set its `basePaymentProvider` to `valitor`. Fill in `successUrl`, `errorUrl` and `cancelUrl`. See [Set up a payment provider](../guides/providers.md#set-up-a-payment-provider).
3. Fill in the settings in the [Settings](#settings) table, on the node or in configuration.
4. Ekom sends the callback address, `/ekom/payments/valitorresponse` on your site, to Valitor with every payment as `PaymentSuccessfulServerSideURL`. See [What the customer sees](#what-the-customer-sees).

## Settings

The setting names are the properties of `ValitorSettings` in camelCase. Ekom reads each one from code, then the provider node, then `Ekom:Payments:valitor`. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

| Alias | Required | Default | What it does |
| --- | --- | --- | --- |
| `merchantId` | Yes | none | Your Valitor merchant ID. One value per store. The payment request fails when it is empty. |
| `verificationCode` | Yes | none | The verification code Valitor gave you. Ekom signs the request and checks Valitor's answer with it. One value per store. The callback is refused when it is empty. |
| `paymentPageUrl` | Yes | none | The address of Valitor's payment page. The payment request fails when it is empty. See [Test it](#test-it) for the two addresses. |
| `loanType` | No | `Disabled` | `Disabled`: a card payment. `IsLoan`: a card loan with interest. `IsInterestFreeLoan`: an interest-free card loan. |
| `merchantName` | When `loanType` is not `Disabled` | none | Your merchant name, sent to Valitor with card loans. One value per store. |
| `sessionExpiredTimeoutInSeconds` | No | `0` (off) | How many seconds the customer has to finish on Valitor's page. Ekom only sends it when `sessionExpiredRedirectURL` is also set. |
| `sessionExpiredRedirectURL` | No | none | Where Valitor sends the customer when the time runs out. One value per language. |

"One value per store" means that the node property holds a separate value for each store, and Ekom uses the value for the order's store. "One value per language" works the same way, with the payment's language: the node's `language` value, or `is-IS` when it is empty. In configuration a setting has one value for all stores and languages.

An `appsettings.json` example with placeholders:

```json
{
  "Ekom": {
    "Payments": {
      "valitor": {
        "merchantId": "<merchant id>",
        "verificationCode": "<verification code>",
        "paymentPageUrl": "https://paymentweb.uat.valitor.is/",
        "loanType": "Disabled"
      }
    }
  }
}
```

Keep `verificationCode` out of source control, for example in user secrets or the environment variable `Ekom__Payments__valitor__verificationCode`.

## What the customer sees

1. The customer submits payment. Ekom sets the order to `WaitingForPayment` and returns an HTML page. The page posts a form to `paymentPageUrl` and the browser goes to Valitor.
2. The customer pays on Valitor's page.
3. When the payment goes through, Valitor's server calls `/ekom/payments/valitorresponse` on your site. Ekom checks the call and records the payment, and the recorded payment completes the order.
4. Valitor sends the customer straight on to your `successUrl`, with `orderId` and `reference` (the Ekom payment ID) in the query string. Ekom asks Valitor to redirect automatically (`PaymentSuccessfulAutomaticRedirect` set to `1`).
5. If the customer cancels, Valitor sends them to Ekom's `/ekom/checkout/payment-return`. Ekom releases the order's holds and sends the customer to your `cancelUrl`.
6. If `sessionExpiredTimeoutInSeconds` and `sessionExpiredRedirectURL` are set and the time runs out, Valitor sends the customer to `sessionExpiredRedirectURL`.

The success page does not complete the order. The callback in step 3 does. For step 5, see [When the customer cancels or the gateway reports an error](../guides/checkout.md#when-the-customer-cancels-or-the-gateway-reports-an-error). Ekom does not send an error address to Valitor.

Ekom builds the callback address from the scheme and host of the checkout request. To send another address, set `PaymentSettings.ReportUrl` in a `CheckoutEvents.PayAsync` handler.

## What Ekom records

Ekom sends Valitor the Ekom payment ID as `ReferenceNumber`. The callback reads its values from the query string. It must return the reference number, and its `DigitalSignatureResponse` must match a SHA-256 hash of `verificationCode` followed by the reference number.

| Valitor outcome | What Ekom records |
| --- | --- |
| The callback arrives and is verified | A `Captured` transaction for the order amount of the payment. It holds Valitor's transaction number as the transaction ID, the authorization number, the masked card number, the card type, and the details `contractNumber`, `contractType` and `date`. Ekom then works out the payment status, see [How the payment status is worked out](../guides/payments.md#how-the-payment-status-is-worked-out). |
| Valitor repeats the callback | Nothing new. The capture is recorded once per payment. |
| The callback cannot be verified | Nothing. Ekom logs an error and answers HTTP 500. |
| The callback has a value in the wrong format, or the reference number is not a payment ID | Nothing. Ekom answers HTTP 400. |
| The payment ID is unknown, or belongs to another gateway | Nothing. Ekom answers HTTP 404. |
| The customer cancels, or the time runs out | Nothing. The payment stays `Pending`. |

The `contractNumber` detail is the same key that Teya Consumer Loans uses, so `payment.GetDetail("contractNumber")` works for both. See [Read the payment in your own code](../guides/payments.md#read-the-payment-in-your-own-code).

If recording the payment throws an exception, Ekom logs it, answers with an error and sends an alert email when `Ekom:Payments:SendEmailAlerts` is `true`. See [Payments configuration](../reference/configuration-reference.md#payments).

To run code when the payment is recorded, subscribe to `OrderEvents.PaymentRecordedAsync`. See [React to recorded payments](../guides/payments.md#react-to-recorded-payments). The package's own `Events.Success` and `Events.Error` are no longer raised.

## Capture, refund and cancel

Ekom sends every payment with `AuthorizationOnly` set to `0`, so it is not an authorization-only payment, and the package records it as captured. There is nothing to capture later.

The package cannot refund or void a payment. Do that at Valitor. Valitor does not tell Ekom about it, so the order's payment status stays `Paid`. Refunded cannot be set by hand in Ekom Manager either. See [Set the payment status by hand](../manager/orders.md#set-the-payment-status-by-hand).

## Test it

Point `paymentPageUrl` at Valitor's test payment page and use the test merchant ID and verification code from Valitor:

| Environment | `paymentPageUrl` |
| --- | --- |
| Test | `https://paymentweb.uat.valitor.is/` |
| Production | `https://greidslusida.valitor.is` |

There is no separate test mode setting. The address and the credentials decide where the payment goes.

Valitor's server must be able to reach `/ekom/payments/valitorresponse` on your site. A site that runs only on your own machine does not receive the callback, so the order is not completed.

## Limits and things to know

- **Card loans need 30000.** When `loanType` is not `Disabled` and the total is below 30000 in the order's currency, the payment request fails with `ValitorAHKException`. It also fails when `merchantName` is empty. Checkout then sets the order back to Incomplete, see [If sending to payment fails](../guides/checkout.md#if-sending-to-payment-fails).
- **Language.** Ekom sends `IS`, `EN`, `DA` or `DE`, taken from the node's `language` value. Any other language is sent as `IS`.
- **Order lines.** Ekom sends one line per payment item, with its quantity, unit price and discount. By default that is one line with the order title and the total. To change the lines, use `CheckoutEvents.PaymentOrderItemsPreparingAsync`.
- **Rounding.** Ekom rounds each unit price to whole units, half away from zero, before it sends and signs it. Ekom still records the unrounded amount. Quantity, price and discount are formatted with the invariant culture, so a decimal discount uses a period, for example `1.5`.
- **A new verification code affects new payments only.** Ekom checks the callback with the `verificationCode` that was used when the payment started.
- **Content Security Policy.** The page that sends the customer to Valitor runs a short script. If your site sets a CSP nonce, put it in `HttpContext.Items` under `CspNonce`, or under the key named in `Ekom:CspKey`, and Ekom adds it to the script.
- **Settings the package does not use.** `ValitorSettings` also has `reportUrl` and `paymentSuccessfulURLText`. Setting them changes nothing.

## Related pages

- [Payments](../guides/payments.md): what Ekom stores for a payment and how the payment status is worked out.
- [Payment and shipping providers](../guides/providers.md): create the payment provider node.
- [Checkout](../guides/checkout.md): how a payment is started and how a paid order is completed.
- [Teya Consumer Loans](teya-consumer-loans.md): another way to offer loans at checkout.
- [Configuration reference](../reference/configuration-reference.md#payments): `Ekom:Payments` settings.

[Back to Payment gateways](README.md)
