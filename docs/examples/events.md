# React to events

[Ekom docs](../README.md) / [Examples](README.md) / React to events

These examples run your own code when something happens in Ekom: an order is completed, paid, changed or fulfilled, stock changes, a customer adds to the basket or pays, or the storefront asks for shipping options. Each one is a complete class you can copy into your site project.

Every event behaves differently when your handler fails. Read the "What to know" list under each example before you use it. The [Events reference](../reference/events.md) lists every event and its arguments.

On this page:

- [Subscribe when the site starts and unsubscribe when it stops](#subscribe-when-the-site-starts-and-unsubscribe-when-it-stops)
- [Send a completed order to your ERP system](#send-a-completed-order-to-your-erp-system)
- [Email the customer when an order is fulfilled](#email-the-customer-when-an-order-is-fulfilled)
- [Follow captured, failed and refunded payments](#follow-captured-failed-and-refunded-payments)
- [Limit how many of a product a customer can buy](#limit-how-many-of-a-product-a-customer-can-buy)
- [Push stock changes to your ERP system](#push-stock-changes-to-your-erp-system)
- [Offer only pickup for bulky items](#offer-only-pickup-for-bulky-items)

## Subscribe when the site starts and unsubscribe when it stops

Ekom's events are static events in the `Ekom.Events` namespace. Subscribe from an Umbraco component: a composer adds the component, Umbraco calls `InitializeAsync` when the site starts and `TerminateAsync` when it stops. This example logs a warning when an order is cancelled. Every other example on this page uses the same pattern.

```csharp
using Ekom.Events;
using Ekom.Models.Orders;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyShop.Events;

public sealed class CancelledOrderAlertsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Components().Append<CancelledOrderAlertsComponent>();
}

public sealed class CancelledOrderAlertsComponent : IAsyncComponent
{
    private readonly ILogger<CancelledOrderAlertsComponent> _logger;

    public CancelledOrderAlertsComponent(ILogger<CancelledOrderAlertsComponent> logger)
    {
        _logger = logger;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        OrderEvents.OrderStatusChangedAsync += OnOrderStatusChangedAsync;
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        // Remove exactly the handler you added, or it runs twice after Umbraco restarts the components.
        OrderEvents.OrderStatusChangedAsync -= OnOrderStatusChangedAsync;
        return Task.CompletedTask;
    }

    private Task OnOrderStatusChangedAsync(object sender, OrderStatusChangedEventArgs e, CancellationToken ct)
    {
        if (e.Status != OrderStatus.Cancelled)
        {
            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "Order {OrderNumber} ({OrderId}) in store {StoreAlias} was cancelled. It was {PreviousStatus}.",
            e.OrderInfo?.OrderNumber,
            e.OrderUniqueId,
            e.OrderInfo?.StoreInfo.Alias,
            e.PreviousStatus);

        return Task.CompletedTask;
    }
}
```

Umbraco finds the composer by itself. You do not register it in `Program.cs`.

What to know:

- Umbraco creates the component once. Inject only singletons into its constructor, such as `ILogger<T>`, `IOptions<T>` or `IServiceScopeFactory`. To use a scoped or transient service in a handler, create a scope for each call, as in [Email the customer when an order is fulfilled](#email-the-customer-when-an-order-is-fulfilled).
- Subscribe with a method, not a lambda, so that `TerminateAsync` can remove the same handler.
- Handlers run one at a time, in the order they were subscribed. If one throws, the handlers after it do not run, and that includes Ekom's own handlers and those of installed plugins.
- `OrderStatusChangedAsync` runs after the new status is saved, in the request that changed it: a manager action in the backoffice, a payment provider's callback, or your own code. It is not always the customer's request, so do not read the customer from `HttpContext` in this handler.
- If the handler throws, the status stays saved, but the code that changed it gets the exception: the manager user sees an error, and a payment callback stops before it completes checkout.
- `e.OrderInfo` is `null` when Ekom could not read the saved order. `e.PreviousStatus` is `null` when the order's history does not say.
- Ekom's own code subscribes the same way. One Ekom service uses an `IHostedService` (`StartAsync` and `StopAsync`) instead, which also works.

## Send a completed order to your ERP system

`CheckoutEvents.CompleteCheckoutAsync` runs after a paid order has completed. The handler copies the order lines, the customer and the payment into a queue and returns at once. A background service sends the queue to the ERP system with retries, and writes the result to the order's activity log, where staff see it in Ekom Manager.

```csharp
using System.Net.Http.Json;
using System.Threading.Channels;
using Ekom.Events;
using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyShop.Events;

public sealed record ErpOrder(
    Guid OrderId,
    string OrderNumber,
    string StoreAlias,
    string Currency,
    decimal Total,
    ErpCustomer Customer,
    IReadOnlyList<ErpOrderLine> Lines,
    ErpPayment? Payment);

public sealed record ErpCustomer(
    string Name,
    string Email,
    string Phone,
    string Address,
    string City,
    string ZipCode,
    string Country);

public sealed record ErpOrderLine(string Sku, string Title, decimal Quantity, decimal LineTotal);

public sealed record ErpPayment(string? Provider, string? TransactionId, decimal Amount, string? Currency);

public sealed class ErpOrderExportComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddHttpClient("Erp", client =>
        {
            client.BaseAddress = new Uri(builder.Config["Erp:BaseUrl"] ?? "https://erp.example.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddSingleton<ErpOrderQueue>();
        builder.Services.AddHostedService<ErpOrderSender>();
        builder.Components().Append<ErpOrderExportComponent>();
    }
}

/// <summary>Holds completed orders until the sender passes them to the ERP system.</summary>
public sealed class ErpOrderQueue
{
    private readonly Channel<ErpOrder> _channel = Channel.CreateUnbounded<ErpOrder>();

    public ChannelReader<ErpOrder> Reader => _channel.Reader;

    public bool TryEnqueue(ErpOrder order) => _channel.Writer.TryWrite(order);
}

public sealed class ErpOrderExportComponent : IAsyncComponent
{
    private readonly ErpOrderQueue _queue;
    private readonly ILogger<ErpOrderExportComponent> _logger;

    public ErpOrderExportComponent(ErpOrderQueue queue, ILogger<ErpOrderExportComponent> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        CheckoutEvents.CompleteCheckoutAsync += OnCompleteCheckoutAsync;
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
        return Task.CompletedTask;
    }

    private Task OnCompleteCheckoutAsync(object sender, CompleteCheckoutEventArgs e, CancellationToken ct)
    {
        // An exception here stops checkout completion for an order the customer has paid for.
        // Copy what the ERP needs and return; the sender makes the slow call later.
        try
        {
            var order = e.OrderInfo;
            var customer = order.CustomerInformation.Customer;

            var erpOrder = new ErpOrder(
                order.UniqueId,
                order.OrderNumber,
                order.StoreInfo.Alias,
                order.StoreInfo.Currency.ISOCurrencySymbol,
                order.ChargedAmount.Value,
                new ErpCustomer(
                    customer.Name,
                    customer.Email,
                    customer.Phone,
                    customer.Address,
                    customer.City,
                    customer.ZipCode,
                    customer.Country),
                order.OrderLines
                    .Select(line => new ErpOrderLine(
                        line.Variant?.SKU ?? line.Product.SKU,
                        line.Product.Title,
                        line.Quantity,
                        line.Amount.WithVat.Value))
                    .ToList(),
                e.Payment is null
                    ? null
                    : new ErpPayment(e.Payment.Provider, e.Payment.TransactionId, e.Payment.Amount, e.Payment.Currency));

            if (!_queue.TryEnqueue(erpOrder))
            {
                _logger.LogError("Order {OrderNumber} could not be queued for the ERP system.", order.OrderNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order {OrderId} could not be prepared for the ERP system.", e.OrderInfo?.UniqueId);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Sends queued orders to the ERP system, outside the customer's checkout.</summary>
public sealed class ErpOrderSender : BackgroundService
{
    private const int MaxAttempts = 5;

    private readonly ErpOrderQueue _queue;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErpOrderSender> _logger;

    public ErpOrderSender(
        ErpOrderQueue queue,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<ErpOrderSender> logger)
    {
        _queue = queue;
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var order in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                var sent = await SendWithRetriesAsync(order, stoppingToken);
                await WriteActivityLogAsync(order, sent, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> SendWithRetriesAsync(ErpOrder order, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("Erp");

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/orders")
                {
                    Content = JsonContent.Create(order),
                };

                // The same order always has the same key, so the ERP can ignore a second delivery.
                request.Headers.Add("Idempotency-Key", order.OrderId.ToString());

                using var response = await client.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                _logger.LogWarning(
                    "ERP returned {StatusCode} for order {OrderNumber}, attempt {Attempt}.",
                    (int)response.StatusCode, order.OrderNumber, attempt);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "ERP call failed for order {OrderNumber}, attempt {Attempt}.", order.OrderNumber, attempt);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "ERP call timed out for order {OrderNumber}, attempt {Attempt}.", order.OrderNumber, attempt);
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }

        _logger.LogError("Order {OrderNumber} was not sent to the ERP system after {Attempts} attempts.", order.OrderNumber, MaxAttempts);
        return false;
    }

    private async Task WriteActivityLogAsync(ErpOrder order, bool sent, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var activityLog = scope.ServiceProvider.GetRequiredService<IOrderActivityLogService>();

            // Staff see this line on the order in Ekom Manager.
            await activityLog.AddOrderLogAsync(
                order.OrderId,
                sent ? "Sent to the ERP system." : "Could not be sent to the ERP system. Send it again by hand.",
                userName: "ERP export",
                logType: sent ? OrderActivityLogType.Success : OrderActivityLogType.Alert,
                ct: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not write the ERP result to the activity log of order {OrderNumber}.", order.OrderNumber);
        }
    }
}
```

Put the ERP address in `appsettings.json`:

```json
{
  "Erp": {
    "BaseUrl": "https://erp.example.com/"
  }
}
```

What to know:

- The event runs when Ekom completes a paid order: after a payment provider records a captured or authorized payment (in the provider's callback request), in the customer's own checkout request for an offline payment, and when your code calls `Order.CompleteOrderAsync`.
- It runs after Ekom has taken stock, claimed the coupon and set the order status. `e.OrderInfo` is read again after completion, so it shows the final status. If completion fails, for example when stock ran out, the event is not raised.
- If the handler throws, the order still completes. Ekom logs the error, runs the other handlers and opens an order review with the code `completion-handler-failed`, so staff know an integration may have missed the order. The handler still catches every exception and makes no remote call, because a slow handler delays the customer's confirmation page.
- The event can run more than once for one order, because code can call `Order.CompleteOrderAsync` again for an order that has already completed. The `Idempotency-Key` header carries the order's `UniqueId`, so the ERP system can ignore an order it already has. If your ERP system cannot do that, keep a list of sent order IDs in your own database table and check it before you send.
- Ekom raises this event without a cancellation token. The `ct` your handler gets never cancels.
- `e.Payment` is `null` when no payment is recorded on the order. It holds the provider, the transaction ID, amounts, the card brand and masked number, and the provider's own details (`GetDetail(key)`, `GetData<T>()`). Send only what the ERP system needs.
- `line.Amount.WithVat.Value` is the line total after discounts, with VAT. `order.ChargedAmount.Value` is what the customer paid after gift cards.
- The queue lives in memory. Orders still in it when the site stops are lost. If you cannot lose an order, write it to your own database table in the handler and let the background service read from there.

## Email the customer when an order is fulfilled

`OrderEvents.FulfillmentStatusChangedAsync` runs each time an order's fulfillment status changes. This handler sends a "your order has shipped" email through Umbraco's `IEmailSender` when the status becomes `Fulfilled`. It uses the sender address from `Umbraco:CMS:Global:Smtp:From`.

```csharp
using System.Net;
using Ekom.Events;
using Ekom.Models.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace MyShop.Events;

public sealed class ShippedEmailComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Components().Append<ShippedEmailComponent>();
}

public sealed class ShippedEmailComponent : IAsyncComponent
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<GlobalSettings> _globalSettings;
    private readonly ILogger<ShippedEmailComponent> _logger;

    public ShippedEmailComponent(
        IServiceScopeFactory scopeFactory,
        IOptions<GlobalSettings> globalSettings,
        ILogger<ShippedEmailComponent> logger)
    {
        _scopeFactory = scopeFactory;
        _globalSettings = globalSettings;
        _logger = logger;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        OrderEvents.FulfillmentStatusChangedAsync += OnFulfillmentStatusChangedAsync;
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        OrderEvents.FulfillmentStatusChangedAsync -= OnFulfillmentStatusChangedAsync;
        return Task.CompletedTask;
    }

    private async Task OnFulfillmentStatusChangedAsync(object sender, FulfillmentStatusChangedEventArgs e, CancellationToken ct)
    {
        if (e.Status != FulfillmentStatus.Fulfilled || e.OrderInfo is null)
        {
            return;
        }

        var order = e.OrderInfo;
        var customer = order.CustomerInformation.Customer;
        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            return;
        }

        // The status is already saved. A failed email must not turn the save into an error
        // for the manager user or the payment callback that made the change.
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var body =
                $"<p>Hi {WebUtility.HtmlEncode(customer.Name)},</p>" +
                $"<p>Your order {WebUtility.HtmlEncode(order.OrderNumber)} is on its way.</p>";

            var message = new EmailMessage(
                _globalSettings.Value.Smtp?.From,
                customer.Email,
                $"Your order {order.OrderNumber} has shipped",
                body,
                isBodyHtml: true);

            await emailSender.SendAsync(message, emailType: "MyShopOrderShipped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send the shipped email for order {OrderNumber}.", order.OrderNumber);
        }
    }
}
```

What to know:

- The event runs after the new status is saved, in the request that changed it. When staff fulfill an order in Ekom Manager, the handler runs in their backoffice request, not in the customer's.
- If the handler throws, the status stays saved and the code that changed it gets the exception. The manager user sees an error even though the order was fulfilled. The handler catches its own exceptions for this reason.
- The event is raised only when the status really changes. It is raised again if the order leaves `Fulfilled` and comes back, so the customer can get the email twice. Store a flag of your own if that must not happen.
- In the manager, the **Trigger status events** switch decides whether a change raises events. If staff turn it off, no email is sent. Code that calls `Order.UpdateStatusAsync` with `ChangeOrderSettings { FireEvents = false }` raises no status events either.
- `OrderStatusChangedAsync` and `PaymentStatusChangedAsync` work the same way for the order status and the payment status. One save can raise more than one of them.
- Sending email takes time, and the manager user waits for it. For many emails or a slow mail server, queue the email as in [Send a completed order to your ERP system](#send-a-completed-order-to-your-erp-system).

## Follow captured, failed and refunded payments

`OrderEvents.PaymentRecordedAsync` runs once for each payment transaction Ekom stores: a capture, an authorization, a failure, a refund and so on. This handler logs captures and failures and emails the bookkeeper about refunds. Set the bookkeeper's address in `MyShop:BookkeepingEmail`.

```csharp
using System.Globalization;
using Ekom.Events;
using Ekom.Models.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace MyShop.Events;

public sealed class PaymentAlertsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Components().Append<PaymentAlertsComponent>();
}

public sealed class PaymentAlertsComponent : IAsyncComponent
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<GlobalSettings> _globalSettings;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentAlertsComponent> _logger;

    public PaymentAlertsComponent(
        IServiceScopeFactory scopeFactory,
        IOptions<GlobalSettings> globalSettings,
        IConfiguration configuration,
        ILogger<PaymentAlertsComponent> logger)
    {
        _scopeFactory = scopeFactory;
        _globalSettings = globalSettings;
        _configuration = configuration;
        _logger = logger;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        OrderEvents.PaymentRecordedAsync += OnPaymentRecordedAsync;
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        OrderEvents.PaymentRecordedAsync -= OnPaymentRecordedAsync;
        return Task.CompletedTask;
    }

    private async Task OnPaymentRecordedAsync(object sender, PaymentRecordedEventArgs e, CancellationToken ct)
    {
        var payment = e.Payment;
        var transaction = e.Transaction;

        switch (transaction.Type)
        {
            case PaymentTransactionType.Captured:
                _logger.LogInformation(
                    "Order {OrderNumber}: {Amount} {Currency} captured through {Provider}. Payment status is now {PaymentStatus}.",
                    payment.OrderNumber, transaction.Amount, transaction.Currency, payment.ProviderName, e.PaymentStatus);
                break;

            case PaymentTransactionType.Failed:
                // Message is the provider's reason, when it gave one.
                _logger.LogWarning(
                    "Order {OrderNumber}: payment through {Provider} failed. {Message}",
                    payment.OrderNumber, payment.ProviderName, transaction.Message);
                break;

            case PaymentTransactionType.Refunded:
                await TellBookkeepingAsync(payment, transaction);
                break;
        }
    }

    private async Task TellBookkeepingAsync(OrderPayment payment, OrderPaymentTransaction transaction)
    {
        var to = _configuration["MyShop:BookkeepingEmail"];
        if (string.IsNullOrWhiteSpace(to))
        {
            return;
        }

        // The refund is already stored. Throwing here would fail the code that recorded it.
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var amount = transaction.Amount.ToString("N2", CultureInfo.InvariantCulture);
            var message = new EmailMessage(
                _globalSettings.Value.Smtp?.From,
                to,
                $"Refund on order {payment.OrderNumber}",
                $"{amount} {transaction.Currency} was refunded through {payment.ProviderName} " +
                $"(transaction {transaction.TransactionId}), recorded by {transaction.Actor}.",
                isBodyHtml: false);

            await emailSender.SendAsync(message, emailType: "MyShopRefund");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not tell bookkeeping about the refund on order {OrderNumber}.", payment.OrderNumber);
        }
    }
}
```

What to know:

- The event runs after the transaction is saved and the order's payment status is calculated again, in the code that recorded the payment. That is usually the payment provider's callback request.
- It runs before the status change events and before checkout completion. If the handler throws on a capture, the transaction stays saved, but the order is not completed in that call. A repeated callback from the provider completes it. For an offline payment, Ekom logs the exception and completes the order anyway.
- A repeated callback that stores nothing new does not raise the event, so it runs once for each stored transaction. `e.Transaction.Id` identifies the transaction if you store your own copy.
- `e.PaymentStatus` is the order's payment status after this transaction, and `e.PreviousPaymentStatus` the one before. Both are nullable.
- `e.Transaction.Message`, `RawData` and `GetDetails()` come from the payment provider. Do not log `RawData`, and do not send card details in email.

## Limit how many of a product a customer can buy

A handler on a "before" event can stop an action by throwing. Here, a product with a `maxPerOrder` property can be bought at most that many times in one order. `OrderEvents.AddingOrderlineAsync` stops the add-to-basket, and `CheckoutEvents.ProcessingAsync` checks again when the customer pays.

```csharp
using System.Globalization;
using System.Net;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyShop.Events;

public sealed class PurchaseLimitsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Components().Append<PurchaseLimitsComponent>();
}

/// <summary>
/// Products with a "maxPerOrder" property can be bought only that many times in one order.
/// </summary>
public sealed class PurchaseLimitsComponent : IAsyncComponent
{
    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        OrderEvents.AddingOrderlineAsync += OnAddingOrderlineAsync;
        CheckoutEvents.ProcessingAsync += OnProcessingAsync;
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        OrderEvents.AddingOrderlineAsync -= OnAddingOrderlineAsync;
        CheckoutEvents.ProcessingAsync -= OnProcessingAsync;
        return Task.CompletedTask;
    }

    // Runs before Ekom changes the basket. Throwing here leaves the basket as it was.
    private static Task OnAddingOrderlineAsync(object sender, AddingOrderlineEventArgs e, CancellationToken ct)
    {
        var limit = ParseLimit(e.Product.GetValue("maxPerOrder"));
        if (limit is null)
        {
            return Task.CompletedTask;
        }

        var lines = e.OrderInfo.OrderLines.Where(line => line.ProductKey == e.Product.Key).ToList();
        var inBasket = lines.Sum(line => line.Quantity);

        // "Set" replaces the quantity of the matching line; the other actions add to the basket.
        var after = e.Action == OrderAction.Set
            ? inBasket - (lines.FirstOrDefault(line => line.VariantKey == e.Variant?.Key)?.Quantity ?? 0) + e.Quantity
            : inBasket + e.Quantity;

        if (after > limit)
        {
            throw new EkomHttpException(
                HttpStatusCode.BadRequest,
                $"You can buy at most {limit} of {e.Product.Title} in one order.");
        }

        return Task.CompletedTask;
    }

    // Runs when the customer pays, before stock is checked and before the payment provider is called.
    // It catches quantities that were changed without AddingOrderlineAsync, for example through
    // POST /ekom/order/updateorderlinequantity.
    private static Task OnProcessingAsync(object sender, ProcessingEventArgs e, CancellationToken ct)
    {
        foreach (var product in e.OrderInfo.OrderLines.GroupBy(line => line.ProductKey))
        {
            var first = product.First().Product;
            var limit = ParseLimit(first.Properties.GetValue("maxPerOrder"));

            if (limit is not null && product.Sum(line => line.Quantity) > limit)
            {
                throw new EkomHttpException(
                    HttpStatusCode.BadRequest,
                    $"You can buy at most {limit} of {first.Title} in one order. Change the quantity in your basket.");
            }
        }

        return Task.CompletedTask;
    }

    private static int? ParseLimit(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) && limit > 0
            ? limit
            : null;
}
```

When a handler throws `EkomHttpException`, Ekom's API returns its status code and message. Here `POST /ekom/order/add` and `POST /ekom/checkout/pay` answer with:

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{ "message": "You can buy at most 5 of T-shirt in one order." }
```

Show `message` to the customer. The basket is unchanged.

What to know:

- `AddingOrderlineAsync` runs before Ekom changes or saves the basket, in the request that adds the line. If it throws, the line is not added and nothing is saved. It also runs for `Order.AddOrderLineAsync` and `AddLinkedOrderLinesAsync` called from your own code, where your code gets the exception.
- It does not run for `POST /ekom/order/updateorderlinequantity` or `Order.UpdateOrderlineQuantityAsync`, or when `OrderSettings.FireEvents` is `false`. That is why the checkout check is needed. To check quantity changes as the customer makes them, send them to `POST /ekom/order/add` with `"action": "Set"`.
- Ekom holds the order's lock while `AddingOrderlineAsync` runs. Do not change the same order from the handler.
- `ProcessingAsync` runs in the customer's pay request, after Ekom saves the customer details and providers sent with the request, and before Ekom checks or reserves stock and calls the payment provider. If it throws, no payment starts.
- Use status `400`. Ekom turns an `EkomHttpException` with status `409` from this point of checkout into its own checkout-conflict response, and your message is lost. An exception type that Ekom does not map to a response gives `500` with the text "An unexpected error occurred".
- The form checkout at `POST /ekom/mvcCheckout/pay` does not show your message. It redirects to the return URL with `errorStatus=serverError`.
- Do not use these handlers for slow work. The customer waits for them.

## Push stock changes to your ERP system

`StockEvents.StockChangedAsync` runs after each change to sellable stock. The handler only records the new value. A background service sends the latest value of each changed product or variant to the ERP system every ten seconds, so a busy checkout does not send one request per line.

```csharp
using System.Collections.Concurrent;
using System.Net.Http.Json;
using Ekom.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyShop.Events;

public sealed record StockLevel(Guid Key, string? StoreAlias, decimal Stock);

public sealed class StockSyncComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddHttpClient("Erp", client =>
            client.BaseAddress = new Uri(builder.Config["Erp:BaseUrl"] ?? "https://erp.example.com/"));

        builder.Services.AddSingleton<PendingStockLevels>();
        builder.Services.AddHostedService<StockSyncWorker>();
        builder.Components().Append<StockSyncComponent>();
    }
}

/// <summary>
/// The latest stock level of each product or variant that changed since the last push.
/// Ten changes to one SKU between two pushes become one value.
/// </summary>
public sealed class PendingStockLevels
{
    private readonly ConcurrentDictionary<(Guid Key, string? StoreAlias), decimal> _levels = new();

    public void Set(Guid key, string? storeAlias, decimal stock) => _levels[(key, storeAlias)] = stock;

    public void SetIfMissing(Guid key, string? storeAlias, decimal stock) => _levels.TryAdd((key, storeAlias), stock);

    public IReadOnlyList<StockLevel> TakeAll()
    {
        var taken = new List<StockLevel>();
        foreach (var entry in _levels)
        {
            // Remove only the value we read, so a newer value that arrives meanwhile stays for the next push.
            if (_levels.TryRemove(new KeyValuePair<(Guid, string?), decimal>(entry.Key, entry.Value)))
            {
                taken.Add(new StockLevel(entry.Key.Key, entry.Key.StoreAlias, entry.Value));
            }
        }

        return taken;
    }
}

public sealed class StockSyncComponent : IAsyncComponent
{
    private readonly PendingStockLevels _pending;

    public StockSyncComponent(PendingStockLevels pending)
    {
        _pending = pending;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        StockEvents.StockChangedAsync += OnStockChangedAsync;
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        StockEvents.StockChangedAsync -= OnStockChangedAsync;
        return Task.CompletedTask;
    }

    // Runs after the stock is saved, in the request or job that changed it. Keep it this short.
    private Task OnStockChangedAsync(object sender, StockChangedEventArgs e, CancellationToken ct)
    {
        _pending.Set(e.Key, e.StoreAlias, e.NewValue);
        return Task.CompletedTask;
    }
}

/// <summary>Pushes changed stock levels to the ERP system every ten seconds.</summary>
public sealed class StockSyncWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly PendingStockLevels _pending;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StockSyncWorker> _logger;

    public StockSyncWorker(PendingStockLevels pending, IHttpClientFactory httpClientFactory, ILogger<StockSyncWorker> logger)
    {
        _pending = pending;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task PushAsync(CancellationToken ct)
    {
        var levels = _pending.TakeAll();
        if (levels.Count == 0)
        {
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Erp");
            using var response = await client.PutAsJsonAsync("api/stock", levels, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Could not push {Count} stock levels to the ERP system. Trying again next time.", levels.Count);

            // Put the values back unless a newer value arrived while we were sending.
            foreach (var level in levels)
            {
                _pending.SetIfMissing(level.Key, level.StoreAlias, level.Stock);
            }
        }
    }
}
```

What to know:

- The event runs after the stock is saved, in the request or job that changed it, for example checkout completion, a stock reservation, or your own call to the `Stock` API.
- It runs for `IncrementStockAsync`, both `SetStockAsync` forms, reservations that take or return stock, and stock deducted at checkout. It does not run for warehouse stock or discount stock, or when the value did not change.
- If the handler throws, Ekom logs a warning and the stock stays changed. Handlers after yours do not run, and the event is not raised again.
- `e.Key` is the Umbraco key of the product or variant, not its SKU. Store the key in your ERP system, or look the SKU up in the background service. See [Use Ekom outside a web request](background-jobs.md).
- `e.StoreAlias` is `null` when stock is shared by all stores, and the store alias when stock is kept per store.
- Values waiting to be sent are kept in memory and lost when the site stops. On a site with several servers, each server sends the changes it made, and a late push from one server can overwrite a newer value from another. Run a full stock sync on a schedule as well.
- If you use the Ekom.Algolia plugin, it already updates availability in its search index when stock changes.

## Offer only pickup for bulky items

`ProviderEvents.BeforeReturnShippingProvidersAsync` lets you change the list of shipping providers before the storefront gets it. Here, when the basket holds a product with the `bulky` property ticked, only pickup options are offered. The same component rejects a checkout that sends another shipping provider anyway. `BeforeReturnPaymentProvidersAsync` works the same way for payment providers.

```csharp
using System.Net;
using Ekom.API;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace MyShop.Events;

public sealed class BulkyItemShippingComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Components().Append<BulkyItemShippingComponent>();
}

/// <summary>
/// Products with the "bulky" property ticked cannot go by post or courier.
/// A basket that holds one is offered pickup only.
/// </summary>
public sealed class BulkyItemShippingComponent : IAsyncComponent
{
    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        ProviderEvents.BeforeReturnShippingProvidersAsync += OnBeforeReturnShippingProvidersAsync;
        CheckoutEvents.ProcessingAsync += OnProcessingAsync;
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        ProviderEvents.BeforeReturnShippingProvidersAsync -= OnBeforeReturnShippingProvidersAsync;
        CheckoutEvents.ProcessingAsync -= OnProcessingAsync;
        return Task.CompletedTask;
    }

    // Changes the list the storefront shows. Runs after Ekom has filtered by store, country and amount.
    private static async Task OnBeforeReturnShippingProvidersAsync(
        object sender,
        ProviderEvents.ShippingProvidersEventArgs e,
        CancellationToken ct)
    {
        // The current customer's basket, found from the request's cookie.
        var basket = await Order.Instance.GetOrderAsync(e.StoreAlias, ct);
        if (basket is null || !HasBulkyItem(basket))
        {
            return;
        }

        e.Providers = e.Providers.Where(provider => provider.Method == ShippingMethods.Pickup).ToList();
    }

    // The list above is only what the customer sees. This stops a checkout that sends a hidden provider anyway.
    private static Task OnProcessingAsync(object sender, ProcessingEventArgs e, CancellationToken ct)
    {
        var shipping = e.OrderInfo.ShippingProvider;
        if (shipping is not null && shipping.Method != ShippingMethods.Pickup && HasBulkyItem(e.OrderInfo))
        {
            throw new EkomHttpException(
                HttpStatusCode.BadRequest,
                "Your basket has an item that must be picked up. Choose a pickup option.");
        }

        return Task.CompletedTask;
    }

    private static bool HasBulkyItem(IOrderInfo order)
        => order.OrderLines.Any(line => IsTrue(line.Product.Properties.GetValue("bulky")));

    private static bool IsTrue(string? value)
        => value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
```

What to know:

- The event runs when code asks `Ekom.API.Providers` for the list, for example with `GetShippingProvidersAsync` or `GET /ekom/provider/shippingproviders`. It runs after Ekom has filtered the providers by store, country and order amount. Nothing is saved.
- The synchronous `Providers.GetShippingProviders` runs this handler too, and waits for it to finish. Use `GetShippingProvidersAsync` in your views so the handler does not block a thread.
- The handler runs in the storefront's request, so `Order.Instance.GetOrderAsync` finds the customer's basket from the cookie. `e.StoreAlias` is an empty string when the caller did not give a store; `GetOrderAsync` then uses the store of the current request.
- Ekom raises this event without a cancellation token. The `ct` your handler gets never cancels.
- If the handler throws, the exception reaches the caller. Over HTTP, the request fails with `500`.
- The list only controls what the customer sees. When the customer pays, Ekom checks the chosen provider against its own store, country and amount rules, not against your filter. The `ProcessingAsync` handler closes that gap and answers `400` with its message, as in [Limit how many of a product a customer can buy](#limit-how-many-of-a-product-a-customer-can-buy).
- `line.Product` is the snapshot of the product saved with the order line, so `bulky` is read from that snapshot, not from the product node.

## Related pages

- [Events](../reference/events.md): every event, its arguments, and what happens when a handler throws.
- [Checkout](../guides/checkout.md): where the checkout events run in the payment flow.
- [Payments](../guides/payments.md): how payments are recorded and when checkout completes.
- [Order, payment, and fulfillment statuses](../guides/order-statuses.md): what each status means and what changes it.
- [Payment and shipping providers](../guides/providers.md): how providers are set up and filtered.
- [Use Ekom outside a web request](background-jobs.md): how to use Ekom from background services like the ones on this page.

[Back to Examples](README.md)
