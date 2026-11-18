# Payment gateways

[Ekom docs](../README.md) / Payment gateways

A payment gateway is the company that takes the customer's money: a card processor, a wallet or a pay-later service. Each gateway that Ekom supports is one NuGet package from the [Ekom.Payments repository](https://github.com/Vettvangur/Ekom.Payments). This section has one page per gateway, with what to install, which settings to fill in, which address to give the gateway, and what Ekom records for each outcome.

The packages need Ekom 1.2 or later. They cannot be used without Ekom.

## Where to start

1. Read [Payment and shipping providers](../guides/providers.md) if you have not created a payment provider node before. Every gateway is set up as a provider node.
2. Open the page for your gateway below and follow "Set it up".
3. Read [Payments](../guides/payments.md) to understand what Ekom stores for a payment and how the payment status is worked out.

Offline payments, such as bank transfer or pay on pickup, need no gateway and no package. See [Offline payments](../guides/payments.md#offline-payments).

## The gateways

Set the provider node's `basePaymentProvider` to the name in the last column. Ekom ignores upper and lower case in the name.

| Gateway | How the customer pays | Package | `basePaymentProvider` |
| --- | --- | --- | --- |
| [Stripe](stripe.md) | Card and other methods, on Stripe's hosted Checkout page | `Ekom.Payments.Stripe` | `stripe` |
| [PayPal](paypal.md) | PayPal account, approved on PayPal's site | `Ekom.Payments.PayPal` | `payPal` |
| [Borgun (Teya cards)](borgun.md) | Card, on Borgun's hosted payment page | `Ekom.Payments.Borgun` | `borgun` |
| [Valitor](valitor.md) | Card or card loan, on Valitor's hosted payment page | `Ekom.Payments.Valitor` | `valitor` |
| [Straumur](straumur.md) | Card, on Straumur's hosted checkout page | `Ekom.Payments.Straumur` | `straumur` |
| [Netgiro](netgiro.md) | Pay later, on Netgiro's payment page | `Ekom.Payments.Netgiro` | `netgiro` |
| [Siminn Pay](siminn-pay.md) | In the Siminn Pay app on the customer's phone | `Ekom.Payments.SiminnPay` | `siminnPay` |
| [Teya Consumer Loans](teya-consumer-loans.md) | Consumer loan, applied for on Teya's loan portal | `Ekom.Payments.TeyaConsumerloans` | `teyaConsumerloans` |
| [AltaPay](altapay.md) | Card, on a payment page hosted by AltaPay | `Ekom.Payments.AltaPay` | `altaPay` |
| [PayTrail](paytrail.md) | A method chosen on PayTrail's hosted payment page | `Ekom.Payments.PayTrail` | `payTrail` |

## What every gateway has in common

- **Installing the package is enough.** Ekom finds the provider and registers its services at startup. There is no setup code.
- **Settings come from three places.** A value set in code wins, then the property on the provider node, then `Ekom:Payments:<base provider name>` in configuration. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).
- **Keep secrets out of source control.** Because the last step reads standard .NET configuration, a key can come from user secrets or an environment variable, for example `Ekom__Payments__stripe__secretKey`.
- **The gateway's answer is verified before anything is recorded.** An answer that cannot be verified is logged and changes nothing on the order.
- **A recorded payment completes the order.** You do not complete the checkout yourself. See [How a paid order is completed](../guides/checkout.md#how-a-paid-order-is-completed).
- **Every payment is on the order.** Staff see each attempt and each transaction in Ekom Manager. See [See payments in the Manager](../guides/payments.md#see-payments-in-the-manager).

## Your own gateway

| Page | Read it when you want to |
| --- | --- |
| [Write your own gateway](write-your-own.md) | Connect a gateway that has no package, with a complete example of the three classes you need. |

## Other sections

- [Guides](../guides/README.md): checkout, providers and how payments are recorded.
- [Examples](../examples/README.md): working code, including handlers that run when a payment is recorded.
- [Reference](../reference/README.md): settings, events and the HTTP API.
- [Upgrading](../upgrading/README.md): moving from Ekom.Payments 1.0.

[Back to the documentation home](../README.md)
