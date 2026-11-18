# Borgun

[Ekom docs](../README.md) / [Payment gateways](README.md) / Borgun

Borgun, now Teya, takes card payments. Ekom sends the customer to Borgun's hosted payment page, the customer enters the card there, and Borgun tells Ekom the result. The package is `Ekom.Payments.Borgun`.

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

- Ekom 1.2 or later, and `Ekom.Payments.Borgun` 1.1.0 or later. The package targets .NET 8.
- A Borgun merchant account. From Borgun you need the merchant ID, the secret code and the payment gateway ID, for the test environment and for production.

## Set it up

1. Install the package in the site project:

   ```bash
   dotnet add package Ekom.Payments.Borgun
   ```

2. In Umbraco, create a payment provider node under the store's payment provider container and set its `basePaymentProvider` to `borgun`. Fill in `successUrl`, `errorUrl` and `cancelUrl`. See [Set up a payment provider](../guides/providers.md#set-up-a-payment-provider).
3. Fill in the settings in the [Settings](#settings) table, on the node or in configuration.
4. Ekom sends the callback address, `/ekom/payments/borgunresponse` on your site, to Borgun with every payment as `returnurlsuccessserver`. See [What the customer sees](#what-the-customer-sees).

## Settings

The setting names are the properties of `BorgunSettings` in camelCase. Ekom reads each one from code, then the provider node, then `Ekom:Payments:borgun`. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

| Alias | Required | Default | What it does |
| --- | --- | --- | --- |
| `merchantId` | Yes | none | Your Borgun merchant ID. One value per store. The payment request fails when it is empty, and Ekom stores no payment. |
| `secretCode` | Yes | none | The secret code Borgun gave you. Ekom signs the request and checks Borgun's answer with it. One value per store. The payment request fails when it is empty, and Ekom stores no payment. |
| `paymentGatewayId` | Not checked | `0` | Your Borgun payment gateway ID. Ekom sends it with every payment. One value per store. |
| `paymentPageUrl` | Yes | none | The address of Borgun's payment page. See [Test it](#test-it) for the two addresses. The payment request fails when it is empty, and Ekom stores no payment. |
| `preserveDecimalAmounts` | No | `false` | `false`: amounts are rounded to whole units, for example `24990,00`. `true`: amounts keep 2 decimals, for example `19,99`. |
| `merchantEmail` | No | none | Sent to Borgun as the merchant's email address. One value per store. |
| `merchantLogo` | No | none | The address of a logo that Borgun shows on its payment page. One value per store. |
| `requireCustomerInformation` | No | `false` | `true`: Borgun asks the customer for email, mobile number and home address. Ekom only sends this when `merchantEmail` is also set. One value per store. |

"One value per store" means that the node property holds a separate value for each store, and Ekom uses the value for the order's store. In configuration a setting has one value for all stores.

An `appsettings.json` example with placeholders:

```json
{
  "Ekom": {
    "Payments": {
      "borgun": {
        "merchantId": "<merchant id>",
        "secretCode": "<secret code>",
        "paymentGatewayId": "<payment gateway id>",
        "paymentPageUrl": "https://test.borgun.is/SecurePay/default.aspx",
        "preserveDecimalAmounts": false
      }
    }
  }
}
```

Keep `secretCode` out of source control, for example in user secrets or the environment variable `Ekom__Payments__borgun__secretCode`.

## What the customer sees

1. The customer submits payment. Ekom sets the order to `WaitingForPayment` and returns an HTML page. The page posts a form to `paymentPageUrl` and the browser goes to Borgun.
2. The customer enters the card on Borgun's page.
3. When the payment goes through, Borgun's server calls `/ekom/payments/borgunresponse` on your site. Ekom checks the call and records the payment, and the recorded payment completes the order.
4. Borgun sends the customer to your `successUrl`, with `?orderId=<order id>` added. Ekom asks Borgun to skip its own receipt page.
5. If the customer cancels, Borgun sends them to Ekom's `/ekom/checkout/payment-return`. Ekom releases the order's holds and sends the customer to your `cancelUrl`. The query string includes `paymentError=cancelPayment`.
6. If Borgun reports an error, the same happens with your `errorUrl` and `paymentError=errorPayment`.

The success page does not complete the order. The callback in step 3 does. For steps 5 and 6, see [When the customer cancels or the gateway reports an error](../guides/checkout.md#when-the-customer-cancels-or-the-gateway-reports-an-error).

Ekom builds the callback address from the scheme and host of the checkout request. To send another address, set `PaymentSettings.ReportUrl` in a `CheckoutEvents.PayAsync` handler.

## What Ekom records

With each payment Ekom sends Borgun the Ekom payment ID as `reference`, and the order number as `orderid`. When the order has no order number, Ekom sends the last part of the payment ID instead. The callback must return both, and its `orderhash` must match an HMAC-SHA256 of the order ID, amount and currency, signed with `secretCode`.

| Borgun outcome | What Ekom records |
| --- | --- |
| The callback arrives, is verified and its `status` is `Ok` (case ignored) | A `Captured` transaction for the order amount of the payment. It holds the authorization code, the masked card number, and the details `orderid` (Borgun order ID) and `step`. Ekom then works out the payment status, see [How the payment status is worked out](../guides/payments.md#how-the-payment-status-is-worked-out). |
| The callback is verified but its `status` is not `Ok` | Nothing. Ekom logs a warning and answers HTTP 200. |
| Borgun repeats the callback | Nothing new. The capture is recorded once per payment. |
| The callback cannot be verified | Nothing. Ekom logs an error and answers HTTP 400. |
| The payment ID is unknown, or belongs to another gateway | Nothing. Ekom answers HTTP 404. |
| The customer cancels, or Borgun reports an error | Nothing. The payment stays `Pending`. Ekom releases the holds as described above. |

Ekom does not record the transaction ID for Borgun payments. The `Borgun order id` detail is the value to search for at Borgun.

If recording the payment throws an exception, Ekom logs it, answers with an error and sends an alert email when `Ekom:Payments:SendEmailAlerts` is `true`. See [Payments configuration](../reference/configuration-reference.md#payments).

To run code when the payment is recorded, subscribe to `OrderEvents.PaymentRecordedAsync`. See [React to recorded payments](../guides/payments.md#react-to-recorded-payments). The package's own `Events.Success` and `Events.Error` are no longer raised.

## Capture, refund and cancel

The package records every verified Borgun payment as captured. It has no authorization-only mode and nothing to capture later.

The package cannot refund or void a payment. Do that at Borgun. Borgun does not tell Ekom about it, so the order's payment status stays `Paid`. Refunded cannot be set by hand in Ekom Manager either. See [Set the payment status by hand](../manager/orders.md#set-the-payment-status-by-hand).

## Test it

Point `paymentPageUrl` at Borgun's test payment page and use the test merchant ID, secret code and payment gateway ID from Borgun:

| Environment | `paymentPageUrl` |
| --- | --- |
| Test | `https://test.borgun.is/SecurePay/default.aspx` |
| Production | `https://securepay.borgun.is/SecurePay/default.aspx` |

There is no separate test mode setting. The address and the credentials decide where the payment goes.

Borgun's server must be able to reach `/ekom/payments/borgunresponse` on your site. A site that runs only on your own machine does not receive the callback, so the order is not completed.

## Limits and things to know

- **Rounding.** With `preserveDecimalAmounts` off, Ekom rounds the total and every line to whole units, half away from zero. Ekom still records the unrounded amount. Turn the setting on for a currency with decimals, such as EUR.
- **Language.** Ekom sends the two-letter language code of the node's `language` in upper case: `IS` for `is-IS`, `EN` for `en-US` or `en`.
- **Order lines.** Ekom sends one line per payment item. By default that is one line with the order title and the total. To change the lines, use `CheckoutEvents.PaymentOrderItemsPreparingAsync`.
- **A new secret code affects new payments only.** Ekom checks the callback with the `secretCode` and `merchantId` that were used when the payment started.
- **Content Security Policy.** The page that sends the customer to Borgun runs a short script. If your site sets a CSP nonce, put it in `HttpContext.Items` under `CspNonce`, or under the key named in `Ekom:CspKey`, and Ekom adds it to the script.
- **Settings the package does not use.** `BorgunSettings` also has `reportUrl`, `language` and `skipReceipt`. Setting them changes nothing. Ekom always asks Borgun to skip its receipt page.

## Related pages

- [Payments](../guides/payments.md): what Ekom stores for a payment and how the payment status is worked out.
- [Payment and shipping providers](../guides/providers.md): create the payment provider node.
- [Checkout](../guides/checkout.md): how a payment is started and how a paid order is completed.
- [Teya Consumer Loans](teya-consumer-loans.md): consumer loans from Teya.
- [Configuration reference](../reference/configuration-reference.md#payments): `Ekom:Payments` settings.

[Back to Payment gateways](README.md)
