# Teya Consumer Loans

[Ekom docs](../README.md) / [Payment gateways](README.md) / Teya Consumer Loans

Teya Consumer Loans lets the customer pay with a consumer loan from Teya. Ekom sends the customer to Teya's loan portal to apply, and checks the loan's status with Teya in the background until the loan is approved or ends. The package is `Ekom.Payments.TeyaConsumerloans`.

On this page:

- [Before you start](#before-you-start)
- [Set it up](#set-it-up)
- [Settings](#settings)
- [What the customer sees](#what-the-customer-sees)
- [How Ekom follows the loan](#how-ekom-follows-the-loan)
- [What Ekom records](#what-ekom-records)
- [Capture, refund and cancel](#capture-refund-and-cancel)
- [Test it](#test-it)
- [Limits and things to know](#limits-and-things-to-know)
- [Related pages](#related-pages)

## Before you start

You need:

- Ekom 1.2 or later.
- The package `Ekom.Payments.TeyaConsumerloans`.
- From Teya: the address of the Consumer Loans API (version 3), a user name and password for it, your merchant number, the loan type ID to offer, and the address of the loan portal.

Teya does not call your site. Ekom asks Teya for the loan's status, so there is no callback address to register, and your site only needs to reach Teya's API.

## Set it up

1. Install the package in your site project:

   ```bash
   dotnet add package Ekom.Payments.TeyaConsumerloans
   ```

   The package registers its background service itself. You add no code.

2. Create a payment provider node under the store's payment provider container, and set its `basePaymentProvider` property to `teyaConsumerloans`. If `basePaymentProvider` is empty, Ekom uses the node name, so a node named `teyaConsumerloans` also works. See [Set up a payment provider](../guides/providers.md#set-up-a-payment-provider) for the node, its success and cancel URLs, and its zone and amount range.
3. Fill in the settings in the table below, on the node or in configuration.

## Settings

Ekom reads each setting from the property with that alias on the provider node, then from `Ekom:Payments:teyaConsumerloans` in configuration. A value set in code with `paymentSettings.SetTeyaConsumerloansSettings(...)` in a `CheckoutEvents.PayAsync` handler comes first. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

| Alias | Required | Default | What it does |
| --- | --- | --- | --- |
| `apiBaseUrl` | Yes | None | The base address of the Consumer Loans API v3. Must be an absolute address. End it with `/`: Ekom adds paths such as `online/token/web` to it. |
| `username` | Yes | None | The API user name from Teya. |
| `password` | Yes | None | The API password from Teya. Ekom sends the user name and password with every API call, as basic authentication. |
| `merchantId` | Yes | None | Your merchant number at Teya. Sent as `MerchantNumber`. |
| `loanTypeId` | Yes | None | The ID of the loan type to offer. Must be above `0`. |
| `numberOfPayments` | Yes | None | The number of payments for the loan. Must be above `0`. |
| `flexibleNumberOfPayments` | Yes | None | `true` or `false`. Sent as `FlexibleNumberOfPayments`. |
| `tokenValidMinutes` | Yes | None | Minutes the customer has to open the loan portal. |
| `progressValidMinutes` | Yes | None | Minutes the customer has to finish the loan after opening the portal. |
| `loanPortalUrl` | Yes | None | The address of Teya's loan portal. Must be an absolute address. Ekom sends the customer to this address with the loan token as the last path segment. See [Limits and things to know](#limits-and-things-to-know). |

Ekom checks all of these before it asks Teya for a loan token or stores a payment. Checkout fails when any of them is missing, or when `apiBaseUrl` or `loanPortalUrl` is not an absolute address.

None of these settings hold one value per store or per language. A node has one value of each.

Keep the password out of source control, for example in user secrets or environment variables:

```json
{
  "Ekom": {
    "Payments": {
      "teyaConsumerloans": {
        "apiBaseUrl": "https://api.borgun.is/consumerloans/v3/",
        "username": "your-api-user",
        "password": "your-api-password",
        "merchantId": "your-merchant-number",
        "loanTypeId": 1,
        "numberOfPayments": 12,
        "flexibleNumberOfPayments": true,
        "tokenValidMinutes": 5,
        "progressValidMinutes": 5,
        "loanPortalUrl": "https://loan-portal.example/"
      }
    }
  }
}
```

The `apiBaseUrl` above is the example in the package's settings class. Use the addresses and values Teya gives you.

## What the customer sees

1. The customer submits payment in your checkout. Ekom asks Teya for a loan token (`POST online/token/web`). The request carries the order total, a description, your loan settings, the success and cancel addresses, and the customer's national ID, email and phone number when the order has them.
2. Ekom stores a new payment and starts following the loan in the background.
3. Ekom returns a page that sends the browser to the loan portal.
4. The customer applies for the loan at Teya.
5. When the customer finishes, Teya sends them to the provider node's `successUrl`, with `orderId=<order id>` added. When the customer cancels, Teya sends them to Ekom's payment return address. Ekom releases the stock holds of the attempt and sends the customer on to the node's `cancelUrl`. See [When the customer cancels or the gateway reports an error](../guides/checkout.md#when-the-customer-cancels-or-the-gateway-reports-an-error).

The description is `PaymentSettings.OrderName`. When a `CheckoutEvents.PayAsync` handler does not set it, Ekom joins the titles of the payment items, up to 50 characters. The package does not give Teya the error URL.

The order is completed by the background check, not by the customer's return. The success page can load before the order is completed.

## How Ekom follows the loan

Ekom asks Teya for the loan's status (`GET online/status`) every 5 seconds. It keeps asking for `tokenValidMinutes` plus `progressValidMinutes` plus 30 seconds. With both settings at `5`, that is 10 minutes and 30 seconds.

- **`SUCCESS`:** Ekom validates the loan with Teya (`PUT online/validate`), then records the payment, which completes the order. It stops asking.
- **A validation that fails** with no answer, HTTP 408, HTTP 429 or HTTP 5xx, or **a validated loan that Ekom cannot record**: Ekom tries again at the next interval, within the same time window. It does not validate a validated loan again. When Teya refuses the validation with any other HTTP 4xx answer, Ekom does not try again and stops asking.
- **`CANCELED`, `FAILED`, `PROGRESSEXPIRED` or `TOKENEXPIRED`:** Ekom records the outcome and stops asking.
- **Any other status,** such as `CREATED`, `INPROGRESS` or `PENDING`: Ekom asks again.
- **A status request that fails** with no answer, HTTP 408, HTTP 429 or HTTP 5xx: Ekom tries again at the next interval, within the same time window.
- **Time runs out:** Ekom stops asking and records nothing. A timeout does not mean that the loan failed.

The checks run in memory on the server that took the payment. When the site restarts, Ekom looks again at the Teya payments requested in the last 7 days that have nothing recorded since the request:

- If the time window of a payment has not ended, Ekom keeps asking for the rest of it.
- If the window has ended, Ekom asks once. On `SUCCESS` it validates and records the payment. On a final failure it records it. When the loan is still in progress, it records a note asking staff to check the loan with Teya, and asks no more.
- If that one request fails, Ekom records nothing and asks again at the next restart.

### Follow the checks in the order's activity log

The package does not write the monitoring messages to the order's activity log itself. It raises `Ekom.Payments.TeyaConsumerloans.Events.ActivityAsync` for each step: start, status changes, validation, errors and timeouts. The arguments hold `PaymentId`, `PaymentSettings`, `Message`, `Level` and `IsSuccess`. Messages carry the payment ID, not the loan token or the customer's details.

The event is raised in a background task, without a web request. This hosted service forwards each message to the order's activity log:

```csharp
using Ekom.API;
using Ekom.Models;
using Ekom.Payments.TeyaConsumerloans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeyaEvents = Ekom.Payments.TeyaConsumerloans.Events;

public sealed class TeyaActivityForwarder(IServiceScopeFactory scopes) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        TeyaEvents.ActivityAsync += OnActivityAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        TeyaEvents.ActivityAsync -= OnActivityAsync;
        return Task.CompletedTask;
    }

    async Task OnActivityAsync(object sender, LoanActivityEventArgs args)
    {
        using var scope = scopes.CreateScope();
        var order = scope.ServiceProvider.GetRequiredService<Order>();

        var logType = args.Level >= LogLevel.Warning ? OrderActivityLogType.Alert
            : args.IsSuccess ? OrderActivityLogType.Success
            : OrderActivityLogType.Info;

        await order.AddActivityLogAsync(
            args.PaymentSettings.OrderUniqueId,
            args.Message,
            userName: "Teya Consumer Loans",
            logType: logType);
    }
}
```

Register it with `builder.Services.AddHostedService<TeyaActivityForwarder>();`. `PaymentSettings.OrderUniqueId` is the order ID. The payment ID is a different value. If a handler throws, Ekom logs the error and the checks go on. See [Add an entry from code](../manager/activity-logs.md#add-an-entry-from-code).

## What Ekom records

| Teya status | Recorded on the order |
| --- | --- |
| `SUCCESS`, and validation succeeds | `Captured`, for the full payment amount |
| `SUCCESS`, and validation or recording fails | Nothing new. Ekom logs the error and, when `Ekom:Payments:SendEmailAlerts` is `true`, emails it once per loan. Ekom tries again within the time window, except when Teya refuses the validation with an HTTP 4xx answer other than 408 or 429. When the checks stop or the window ends, the payment stays open and the next restart checks the loan again. |
| `CANCELED` | `Cancelled`, "The loan application was cancelled" |
| `FAILED`, `PROGRESSEXPIRED` or `TOKENEXPIRED` | `Failed`, "The loan application ended with Teya status ..." |
| Still in progress when the window has ended, found at a restart | `Note`, asking staff to check the loan with Teya. A note does not change the payment status. |
| Time runs out while the site is running | Nothing. The next restart checks the loan once. |

A `Captured` record completes the order. See [When a payment completes the order](../guides/payments.md#when-a-payment-completes-the-order). A payment is captured once, even when two checks see `SUCCESS`.

The capture holds:

- **Transaction ID:** the loan contract number.
- **Authorization code:** the loan's authorization number.
- **Payment method:** `Teya Consumer Loans`.
- **Details:** `contractNumber`, the loan contract number, which staff can copy. Read it in code with `payment.GetDetail("contractNumber")`.
- **Raw data:** the contract number and the authorization number. The borrower's national ID is not kept.

Failures, cancellations and notes hold a `status` detail with Teya's status.

## Capture, refund and cancel

The package applies for the loan and records the result. It cannot cancel or refund a loan from Ekom. Do that with Teya.

Ekom is not told about a change made at Teya. To show it on the order, set the payment status by hand in the Manager. That changes the record only and moves no money.

## Test it

The package has no test mode setting. Ekom calls the API at `apiBaseUrl` and sends the customer to `loanPortalUrl`, so point both at the Teya environment you test against, with the user name, password and merchant number Teya gives you for it.

You do not need a public address, because Teya does not call your site. Keep the site running while you test: the checks run in the site's process.

After a test loan, look for the log line `Teya Consumer Loans Response - SUCCESS` and check the payment in the order drawer in the Manager. Log lines that start with `Teya Consumer Loans - PaymentId:` show each step of the checks.

## Limits and things to know

- **The portal address.** Ekom adds the token to `loanPortalUrl` as the last path segment, not as a query string. With `https://portal.example/loan/` or `https://portal.example/loan`, the customer goes to `https://portal.example/loan/<token>`. A query string in `loanPortalUrl` is kept.
- **The validation address.** When it validates the loan, Ekom sends Teya the success address with `token=<token>` added to its query string as `RedirectUrl`. Ekom adds it with `?` when the success address has no query string, and with `&` when it has one.
- **Amounts.** Ekom sends the order total as it is, without rounding. No currency is sent.
- **Several servers.** Each server follows only the loans it started. After a restart, every server that shares the database looks at the open loans, so two servers can ask Teya about, and validate, the same loan. The capture is still stored once.
- **Content Security Policy.** The page that sends the customer to the loan portal is a small inline script. If your site uses a nonce-based Content Security Policy, put the nonce in `HttpContext.Items` under `CspNonce`, or under the key set in `Ekom:CspKey`, and Ekom adds it to the script.
- **Polling interval.** `TeyaConsumerloansPollingService.PollingInterval` is 5 seconds. It is a static property that code can change.
- **Old events.** `Events.Success` and `Events.Error` are obsolete and never raised. Use `OrderEvents.PaymentRecordedAsync`. See [React to recorded payments](../guides/payments.md#react-to-recorded-payments).

## Related pages

- [Payments](../guides/payments.md): how Ekom stores payments and works out the payment status.
- [Payment and shipping providers](../guides/providers.md): create the provider node and where settings come from.
- [Checkout](../guides/checkout.md): what happens when the customer submits payment, cancels or returns.
- [Activity logs](../manager/activity-logs.md): add the monitoring messages to the order.
- [Configuration reference](../reference/configuration-reference.md#payments): `Ekom:Payments` settings, including email alerts.
- [Payment gateways](README.md): all gateway packages.

[Back to Payment gateways](README.md)
