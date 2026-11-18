# Write your own gateway

[Ekom docs](../README.md) / [Payment gateways](README.md) / Write your own gateway

When no package exists for the gateway you need, write the provider yourself. A provider is three classes: its settings, the class that sends the customer to the gateway, and the controller that receives the gateway's answer. This page builds all three for an invented gateway, "MyGateway", that takes a signed form post and posts a signed answer back.

On this page:

- [Before you start](#before-you-start)
- [The settings class](#the-settings-class)
- [The provider class](#the-provider-class)
- [The callback controller](#the-callback-controller)
- [Use it on a site](#use-it-on-a-site)
- [What to know](#what-to-know)

## Before you start

- The classes can live in your site project or in a class library that references Ekom. They need no reference to any `Ekom.Payments` package: the contracts are in Ekom itself, in the `Ekom.Payments` namespace.
- You need no registration code. Ekom finds every class that implements `IPaymentProvider` and has a `_ppNodeName` constant, in all assemblies the site has loaded.
- Read [Record payments from a gateway](../guides/payments.md#record-payments-from-a-gateway) first. It explains `IOrderPayments`, which this page uses, and the rules a provider must follow.

The three classes below compile against the current Ekom build. The gateway itself is invented, so replace the form fields, the signature and the answer with what your gateway documents.

## The settings class

Each public property is one setting. Its alias is the property name in camelCase: `merchantId`, `secretKey`, `paymentPageUrl`.

```csharp
using Ekom.Payments;

namespace MyShop.Payments;

// The settings of the gateway. Ekom fills each property from the payment provider node,
// then from Ekom:Payments:myGateway in configuration. The alias is the property name in camelCase.
public class MyGatewaySettings : PaymentSettingsBase<MyGatewaySettings>
{
    // One value per store: Ekom reads the value for the order's store.
    [EkomProperty(PropertyEditorType.Store)]
    public string? MerchantId { get; set; }

    // Signs the request and verifies the gateway's answer.
    public string? SecretKey { get; set; }

    // The gateway's hosted payment page.
    public Uri? PaymentPageUrl { get; set; }
}
```

- `[EkomProperty(PropertyEditorType.Store)]` marks a setting that holds one value per store. `PropertyEditorType.Language` does the same per language.
- Ekom converts the stored text to the property's type, so a property can be a `string`, a number, a `bool`, a `Guid`, an enum or a `Uri`.

## The provider class

Ekom creates this class for each payment request and calls `RequestAsync`. The method reads the settings, stores the payment on the order, and returns HTML that sends the customer to the gateway.

```csharp
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Ekom.Payments;
using Ekom.Payments.Helpers;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MyShop.Payments;

// Ekom creates this class for each payment request, through dependency injection.
internal sealed class MyGatewayPayment(
    // Ekom.Services has an interface of the same name, so name this one in full.
    Ekom.Payments.IUmbracoService umbraco,
    IOrderPayments payments,
    IHttpContextAccessor httpContextAccessor,
    ILogger<MyGatewayPayment> logger) : IPaymentProvider
{
    // Ekom finds the provider by this constant. A payment provider node with
    // basePaymentProvider "myGateway" uses this class.
    internal const string _ppNodeName = "myGateway";

    internal const string CallbackPath = "/ekom/payments/mygatewayresponse";

    public async Task<string> RequestAsync(PaymentSettings paymentSettings)
    {
        ArgumentNullException.ThrowIfNull(paymentSettings);

        HttpContext httpContext = httpContextAccessor.HttpContext
            ?? throw new NotSupportedException("A payment request needs an HTTP request.");

        // Settings set in code win. The rest come from the node, then from configuration.
        var settings = paymentSettings.CustomSettings.TryGetValue(typeof(MyGatewaySettings), out object? fromCode)
            ? (MyGatewaySettings)fromCode
            : new MyGatewaySettings();

        umbraco.PopulatePaymentProviderProperties(
            paymentSettings, _ppNodeName, settings, MyGatewaySettings.Properties);

        if (string.IsNullOrWhiteSpace(settings.MerchantId)
            || string.IsNullOrWhiteSpace(settings.SecretKey)
            || settings.PaymentPageUrl is null)
        {
            throw new InvalidOperationException("MyGateway needs merchantId, secretKey and paymentPageUrl.");
        }

        decimal total = paymentSettings.Orders.Sum(item => item.GrandTotal);

        // Store the payment on the order before the customer leaves. The settings are stored
        // with it, so the callback can read them without knowing the store or the node.
        var payment = await payments.CreateAsync(
            paymentSettings, _ppNodeName, total, settings, httpContext: httpContext);

        using var scope = PaymentLog.BeginScope(logger, payment);
        logger.LogInformation("MyGateway payment requested. " + PaymentLog.Template,
            payment.OrderNumber, payment.OrderUniqueId, payment.PaymentId);

        string amount = total.ToString("0.00", CultureInfo.InvariantCulture);
        string reference = payment.PaymentId.ToString();

        var fields = new Dictionary<string, string>
        {
            ["merchant"] = settings.MerchantId,
            ["reference"] = reference, // The gateway sends this back, and the callback finds the payment by it.
            ["amount"] = amount,
            ["currency"] = paymentSettings.Currency,
            ["callbackUrl"] = PaymentsUriHelper.EnsureFullUri(new Uri(CallbackPath, UriKind.Relative), httpContext.Request).ToString(),
            ["signature"] = Sign(settings.SecretKey, reference, amount),
        };

        // Ekom returns this HTML to the browser, and the form sends the customer to the gateway.
        var html = new StringBuilder();
        html.Append("<form id=\"payment\" method=\"post\" action=\"")
            .Append(WebUtility.HtmlEncode(settings.PaymentPageUrl.ToString()))
            .Append("\">");

        foreach (var (name, value) in fields)
        {
            html.Append("<input type=\"hidden\" name=\"").Append(WebUtility.HtmlEncode(name))
                .Append("\" value=\"").Append(WebUtility.HtmlEncode(value)).Append("\" />");
        }

        html.Append("</form><script>document.getElementById('payment').submit();</script>");

        return html.ToString();
    }

    internal static string Sign(string secretKey, params string[] values)
    {
        byte[] hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secretKey),
            Encoding.UTF8.GetBytes(string.Join('|', values)));

        return Convert.ToHexString(hash);
    }
}
```

## The callback controller

The gateway posts its answer to this controller. It finds the payment, verifies the answer, records the result and sends the customer on.

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ekom.Models.Payments;
using Ekom.Payments;
using Ekom.Payments.Helpers;
using Ekom.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MyShop.Payments;

// What the gateway posts back when the customer has paid or given up.
public sealed class MyGatewayResponse
{
    public string? Reference { get; set; }
    public string? Status { get; set; }
    public string? TransactionId { get; set; }
    public string? Amount { get; set; }
    public string? Signature { get; set; }
}

[ApiController]
[Route("ekom/payments/mygatewayresponse")]
public sealed class MyGatewayResponseController(
    IOrderPayments payments,
    ILogger<MyGatewayResponseController> logger) : ControllerBase
{
    [HttpPost("")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Post([FromForm] MyGatewayResponse response)
    {
        if (!Guid.TryParse(response.Reference, out Guid paymentId))
        {
            return BadRequest();
        }

        OrderPayment? payment = await payments.GetAsync(paymentId);

        // Refuse a payment that another provider started.
        if (payment is null || !payment.BelongsTo(MyGatewayPayment._ppNodeName))
        {
            return NotFound();
        }

        using var scope = PaymentLog.BeginScope(logger, payment);

        var settings = payment.GetProviderData<MyGatewaySettings>();
        string amount = payment.Amount.ToString("0.00", CultureInfo.InvariantCulture);

        // A callback that cannot be verified says nothing about the payment. Log it and record nothing.
        if (string.IsNullOrWhiteSpace(settings?.SecretKey)
            || response.Amount != amount
            || !SignatureMatches(settings.SecretKey, response, amount))
        {
            logger.LogError("MyGateway callback failed verification. " + PaymentLog.Template,
                payment.OrderNumber, payment.OrderUniqueId, payment.PaymentId);

            return BadRequest();
        }

        PaymentSettings paymentSettings = payment.GetPaymentSettings()
            ?? throw new InvalidOperationException($"Payment {paymentId} has no payment settings.");

        // Leave the signature out of what is stored.
        string rawData = JsonSerializer.Serialize(new { response.Reference, response.Status, response.TransactionId, response.Amount });

        if (response.Status == "paid")
        {
            // Safe to repeat: the same callback delivered twice is stored once.
            // Recording a capture also completes the checkout.
            await payments.RecordAsync(new PaymentTransaction
            {
                PaymentId = payment.PaymentId,
                Type = PaymentTransactionType.Captured,
                TransactionId = response.TransactionId,
                PaymentMethod = "MyGateway",
                RawData = rawData,
            });

            return Redirect(PaymentsUriHelper.EnsureFullUri(paymentSettings.SuccessUrl, Request).ToString());
        }

        // The gateway itself says the payment failed, in an answer that was verified.
        await payments.RecordAsync(new PaymentTransaction
        {
            PaymentId = payment.PaymentId,
            Type = PaymentTransactionType.Failed,
            TransactionId = response.TransactionId,
            Message = "The gateway declined the payment.",
            RawData = rawData,
        });

        return Redirect(PaymentsUriHelper.EnsureFullUri(paymentSettings.ErrorUrl, Request).ToString());
    }

    private static bool SignatureMatches(string secretKey, MyGatewayResponse response, string amount)
    {
        string expected = MyGatewayPayment.Sign(
            secretKey, response.Reference ?? "", amount, response.Status ?? "", response.TransactionId ?? "");

        // Compare in constant time.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(response.Signature ?? ""));
    }
}
```

## Use it on a site

1. Add the three classes to the site and build it.
2. In Umbraco, create a payment provider node and set its `basePaymentProvider` to `myGateway`. See [Create a payment provider](../guides/providers.md).
3. Give the provider its settings, on the node or in configuration:

```json
{
  "Ekom": {
    "Payments": {
      "myGateway": {
        "merchantId": "your-merchant-id",
        "secretKey": "your-secret-key",
        "paymentPageUrl": "https://pay.mygateway.example/checkout"
      }
    }
  }
}
```

4. Pay for a test order and open the order in Ekom Manager. The order drawer shows the payment with its request and its capture. See [See payments in the Manager](../guides/payments.md#see-payments-in-the-manager).

## What to know

- **`_ppNodeName` must be a constant.** Ekom reads it by reflection and ignores a `static readonly` field. The class itself can be `internal`. The name lookup ignores upper and lower case.
- **The returned string is HTML.** Ekom sends it to the browser with status `230`, and a storefront shows it or writes it into the page. See [Responses and errors](../guides/checkout.md#responses-and-errors). A gateway that only needs a redirect returns a page that redirects.
- **Encode what you write into the HTML.** Values such as the callback address and the currency go into attributes.
- **`CreateAsync` stores your settings with the payment.** `payment.GetProviderData<MyGatewaySettings>()` reads them back in the callback, where there is no store or node to read from. They are stored in the database, so mark a property with `[System.Text.Json.Serialization.JsonIgnore]` and `[Newtonsoft.Json.JsonIgnore]` if it must not be saved, and read that value from configuration in the callback instead.
- **Verify before you record.** The example checks the amount and a signature and compares in constant time. When verification fails it records nothing: an answer you cannot trust says nothing about the payment.
- **Recording a capture completes the order.** You do not call checkout completion yourself. A second delivery of the same answer stores nothing and completes nothing.
- **Record `Failed` only when the gateway says so** in an answer you verified. A customer who closes the gateway page is not a failed payment; Ekom releases the attempt when its payment window ends.
- **The callback is a public endpoint.** It needs no sign-in and no antiforgery token, because the gateway calls it. Its only protection is your verification.
- **Ekom.Services has its own `IUmbracoService`.** In a file that imports both namespaces, name `Ekom.Payments.IUmbracoService` in full, as the provider class does.
- **A package for other sites** can add its own services by implementing `IPaymentModule`. See [Add a payment provider package](../guides/payments.md#add-a-payment-provider-package). The [`AsynchronousExample` provider](https://github.com/Vettvangur/Ekom.Payments/tree/main/PaymentProviders/Ekom.Payments.AsynchronousExample) in the Ekom.Payments repository is built that way.

## Related pages

- [Payments](../guides/payments.md): how Ekom stores payments and works out the payment status.
- [Payment and shipping providers](../guides/providers.md): provider nodes and where settings come from.
- [Checkout](../guides/checkout.md): what happens before and after your provider is called.
- [Stripe](stripe.md): a real provider that is driven by webhooks.
- [React to events](../examples/events.md): run your own code when a payment is recorded.

[Back to Payment gateways](README.md)
