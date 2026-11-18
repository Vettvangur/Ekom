# Netgiro

[Ekom docs](../README.md) / [Payment gateways](README.md) / Netgiro

Netgiro is an Icelandic pay-later service. Ekom sends the customer to Netgiro's payment page with a form post, and Netgiro calls your site back when the customer has confirmed the payment. The package is `Ekom.Payments.Netgiro`.

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
- The package `Ekom.Payments.Netgiro`.
- From Netgiro: your application ID, which is a GUID, your secret key, and the address of Netgiro's payment page for the environment you use.
- A site that Netgiro's servers can reach. Netgiro confirms each payment with a request to your site, and Ekom records the payment only from that request.

## Set it up

1. Install the package in your site project:

   ```bash
   dotnet add package Ekom.Payments.Netgiro
   ```

2. Create a payment provider node under the store's payment provider container, and set its `basePaymentProvider` property to `netgiro`. If `basePaymentProvider` is empty, Ekom uses the node name, so a node named `netgiro` also works. See [Set up a payment provider](../guides/providers.md#set-up-a-payment-provider) for the node, its success, error and cancel URLs, and its zone and amount range.
3. Fill in the settings in the table below, on the node or in configuration.

You do not register a callback address at Netgiro. Ekom sends it with every payment, in the `PaymentConfirmedURL` field. The address is:

```text
https://<your site>/ekom/payments/netgiroresponse
```

Ekom builds it from the scheme and host of the checkout request. Netgiro must be able to reach that host.

## Settings

Ekom reads each setting from the property with that alias on the provider node, then from `Ekom:Payments:netgiro` in configuration. A value set in code with `paymentSettings.SetNetgiroSettings(...)` in a `CheckoutEvents.PayAsync` handler comes first. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

| Alias | Required | Default | What it does |
| --- | --- | --- | --- |
| `applicationId` | Yes | None | Your Netgiro application ID, a GUID. Sent as `ApplicationID` and used in the request signature. One value per store. |
| `secret` | Yes | None | Your Netgiro secret key. Ekom signs the payment request with it and verifies Netgiro's callback with it. One value per store. |
| `paymentPageUrl` | Yes | None | The address of Netgiro's payment page. The customer's browser posts the payment form to it. |

`applicationId` and `secret` hold one value per store on the provider node, and Ekom reads the value for the order's store. In configuration each setting has one value for all stores. To use a different Netgiro account per store from configuration, give each store's provider node its own `configurationKey`.

Keep the secret out of source control, for example in user secrets or environment variables:

```json
{
  "Ekom": {
    "Payments": {
      "netgiro": {
        "applicationId": "00000000-0000-0000-0000-000000000000",
        "secret": "your-netgiro-secret",
        "paymentPageUrl": "https://test.netgiro.is/securepay/"
      }
    }
  }
}
```

The success and cancel URLs and the currency are not Netgiro settings. They come from the provider node and the store, as for every gateway.

## What the customer sees

1. The customer submits payment in your checkout. Ekom stores a new payment and returns a page that posts a form to `paymentPageUrl`.
2. The form carries `ApplicationID`, `ConfirmationType` `1` (server callback), the success, cancel and callback addresses, `TotalAmount`, one `Items[n]` group per payment item (`Name`, `Quantity`, `UnitPrice`, `Amount`), `ReferenceNumber`, which is the Ekom payment ID, and `Signature`.
3. The customer confirms the payment at Netgiro.
4. Netgiro calls `/ekom/payments/netgiroresponse`. Ekom verifies the call, records the payment and completes the order.
5. Netgiro sends the customer to the provider node's `successUrl`, with `orderId=<order id>` and `reference=<payment id>` added.

If the customer cancels at Netgiro, Netgiro sends them to Ekom's payment return address, with `reference=<payment id>` added. Ekom releases the stock holds of the attempt and sends the customer on to the node's `cancelUrl`. See [When the customer cancels or the gateway reports an error](../guides/checkout.md#when-the-customer-cancels-or-the-gateway-reports-an-error). The package does not give Netgiro the error URL.

## What Ekom records

Netgiro calls `/ekom/payments/netgiroresponse` with GET or POST. Ekom reads the values from a form body, from a JSON body, or from the query string. It finds the payment by `ReferenceNumber`.

Ekom checks the call before it records anything:

- `InvoiceNumber` must contain only digits.
- When the call has `NetgiroSignature`: `TransactionId` must be present, `Status` must be `1` (unconfirmed) or `2` (confirmed), `TotalAmount` must equal the payment amount rounded to whole units, and `NetgiroSignature` must equal SHA-256 of `secret`, `ReferenceNumber`, `TransactionId`, `InvoiceNumber`, `TotalAmount` and `Status`, joined without separators.
- When the call has no `NetgiroSignature`: `ConfirmationCode` must be present, and `Signature` must equal SHA-256 of `secret`, `ReferenceNumber`, `ConfirmationCode` and `InvoiceNumber`. The amount and status are not part of this signature, so Ekom cannot check them. It logs a warning and records the payment.
- The signature must not be the one Ekom sent with the payment request.

| Netgiro call | Recorded on the order | Ekom answers |
| --- | --- | --- |
| Verified, status `1` or `2`, or verified without `NetgiroSignature` | `Captured`, for the full payment amount | HTTP 200 |
| The same payment confirmed again | Nothing new. Netgiro confirms twice, with status `1` and then `2`, and the capture is stored once. | HTTP 200 |
| Status `5` (cancelled) or any other status | Nothing | HTTP 400 |
| Fails a check above, or the payment has no stored `secret` or `applicationId` | Nothing. Ekom logs an error. | HTTP 400 |
| No `ReferenceNumber` | Nothing | HTTP 400 |
| The payment is not in this site's database, or is not a Netgiro payment | Nothing | HTTP 404 |
| Recording fails with an error | The capture, if it was stored before the error. A repeated call finishes the completion. | An error. Ekom logs it and, when `Ekom:Payments:SendEmailAlerts` is `true`, emails it. |

Ekom never records `Failed` or `Cancelled` for Netgiro. A cancelled payment is only seen through the customer's return to the cancel address.

The capture holds:

- **Transaction ID:** Netgiro's `TransactionId`.
- **Payment method:** `Netgiro`.
- **Details:** `invoiceNumber` (Netgiro's invoice number, which staff can copy) and `status` (Netgiro's status).
- **Raw data:** `ReferenceNumber`, `TransactionId`, `InvoiceNumber`, `TotalAmount`, `Status` and `ConfirmationCode`. The signatures are not kept.

A `Captured` record completes the order. See [When a payment completes the order](../guides/payments.md#when-a-payment-completes-the-order).

## Capture, refund and cancel

The package only starts payments and records Netgiro's confirmation. It cannot capture, refund or cancel a Netgiro payment. Do that with Netgiro.

Ekom is not told about a refund or a cancellation made at Netgiro. To show it on the order, set the payment status by hand in the Manager. That changes the record only and moves no money.

## Test it

The package has no test mode setting. To test, set `paymentPageUrl` to Netgiro's test page and use the application ID and secret that Netgiro gives you for testing. The settings class lists these addresses:

| Environment | `paymentPageUrl` |
| --- | --- |
| Test | `https://test.netgiro.is/securepay/` |
| Production | `https://securepay.netgiro.is/v1/` |

Netgiro must reach the callback address, and Ekom builds it from the host of the checkout request. On your own computer, open the checkout through a public address that forwards to your site, so that Ekom sends that address to Netgiro.

After a test payment, look for the log line `Netgiro Payment Response - SUCCESS` and check the payment in the order drawer in the Manager.

## Limits and things to know

- **Whole amounts.** Ekom rounds `TotalAmount` and each item's `UnitPrice` and `Amount` to whole units, half away from zero. Each value is rounded on its own. By default Ekom sends one payment item. See [What Ekom does when you submit payment](../guides/checkout.md#what-ekom-does-when-you-submit-payment).
- **No currency is sent.** Netgiro gets amounts only. Ekom records the payment in the order's currency, or `ISK` when none is set.
- **The secret is stored with each payment.** Ekom saves `applicationId` and `secret` in the payment's provider data in the database, and verifies the callback with the saved values. If you change the secret, payments started before the change are still verified with the old one.
- **The callback address comes from the request.** If your site runs behind a proxy, make sure the checkout request carries the public scheme and host. Otherwise Netgiro gets an address it cannot reach, and no payment is recorded.
- **Content Security Policy.** The page that sends the customer to Netgiro posts the form with a small inline script. If your site uses a nonce-based Content Security Policy, put the nonce in `HttpContext.Items` under `CspNonce`, or under the key set in `Ekom:CspKey`, and Ekom adds it to the script.
- **Old events.** `Ekom.Payments.Netgiro.Events.Success` and `Events.Error` are obsolete and never raised. Use `OrderEvents.PaymentRecordedAsync`. See [React to recorded payments](../guides/payments.md#react-to-recorded-payments).

## Related pages

- [Payments](../guides/payments.md): how Ekom stores payments and works out the payment status.
- [Payment and shipping providers](../guides/providers.md): create the provider node and where settings come from.
- [Checkout](../guides/checkout.md): what happens when the customer submits payment, cancels or returns.
- [Configuration reference](../reference/configuration-reference.md#payments): `Ekom:Payments` settings, including email alerts.
- [Payment gateways](README.md): all gateway packages.

[Back to Payment gateways](README.md)
