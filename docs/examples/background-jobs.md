[Ekom docs](../README.md) / [Examples](README.md) / Use Ekom outside a web request

# Use Ekom outside a web request

Inside a storefront request, Ekom works out the current store from the address and the current basket from a cookie. A hosted service, a queue handler or a scheduled job has neither. These examples show the calls that work there: they name the store, read orders by ID, change stock, change an order's status and read the catalog for a feed.

On this page:

- [Sync stock from your ERP in a hosted service](#sync-stock-from-your-erp-in-a-hosted-service)
- [Mark an order as shipped from a queue message](#mark-an-order-as-shipped-from-a-queue-message)
- [Export a product feed on a schedule](#export-a-product-feed-on-a-schedule)
- [Check which Ekom calls depend on the request](#check-which-ekom-calls-depend-on-the-request)

## Sync stock from your ERP in a hosted service

This hosted service reads stock levels from your ERP every 15 minutes, finds each SKU in the store, and sets the stock of all of them in one call.

```csharp
using Ekom.API;
using Ekom.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyShop.Jobs;

public sealed record ErpStockLevel(string Sku, decimal Quantity);

// Your own client for the ERP's stock list.
public interface IErpStockFeed
{
    Task<IReadOnlyList<ErpStockLevel>> GetStockLevelsAsync(CancellationToken ct);
}

public sealed class ErpStockSyncService(
    IServiceScopeFactory scopeFactory,
    IErpStockFeed erp,
    ILogger<ErpStockSyncService> logger) : BackgroundService
{
    // There is no request, so there is no current store. Name it on every call.
    private const string StoreAlias = "Store";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

        // The first run waits one interval, so Umbraco has started and Ekom has filled its catalog.
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Keep the service running. The next run tries again.
                logger.LogError(ex, "Stock sync for store {StoreAlias} failed", StoreAlias);
            }
        }
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        IReadOnlyList<ErpStockLevel> levels = await erp.GetStockLevelsAsync(ct);

        // A new scope for each run: Ekom's services are transient and read the HttpContext when they are created.
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        Catalog catalog = scope.ServiceProvider.GetRequiredService<Catalog>();
        Stock stock = scope.ServiceProvider.GetRequiredService<Stock>();

        var changes = new List<StockSetRequest>(levels.Count);
        foreach (ErpStockLevel level in levels)
        {
            if (string.IsNullOrWhiteSpace(level.Sku))
            {
                continue;
            }

            // A SKU belongs to a variant, or to a product without variants.
            IVariant? variant = await catalog.GetVariantAsync(level.Sku, StoreAlias, ct);
            Guid? key = variant?.Key
                ?? (await catalog.GetProductAsync(level.Sku, StoreAlias, raiseEvent: false, ct: ct))?.Key;

            if (key is null)
            {
                logger.LogWarning("ERP SKU {Sku} is not in store {StoreAlias}", level.Sku, StoreAlias);
                continue;
            }

            changes.Add(new StockSetRequest(key.Value, StoreAlias, level.Quantity));
        }

        // One call for the whole list. Each value replaces the stored count.
        await stock.SetStockAsync(changes, ct);

        logger.LogInformation("Set stock for {Count} items in store {StoreAlias}", changes.Count, StoreAlias);
    }
}
```

This composer registers the three examples on this page:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;

namespace MyShop.Jobs;

public sealed class JobsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Runs on every server.
        builder.Services.AddHostedService<ErpStockSyncService>();

        builder.Services.AddScoped<WarehouseShippedHandler>();
        builder.Services.AddSingleton<WarehouseQueueConsumer>();

        // Umbraco runs it on the servers that ServerRoles names.
        builder.Services.AddRecurringBackgroundJob<ProductFeedJob>();

        // Register your own IErpStockFeed here too.
    }
}
```

What to know:

- Ekom's API classes (`Catalog`, `Order`, `Stock`, `Providers`) are transient, and some of their services read the `HttpContext` when they are created. Create a scope for each run and take the API classes from it. Do not keep one in a singleton, and do not use `Catalog.Instance` or `Order.Instance` here.
- Pass the store alias to every call. A call without one uses the first store, with no error. See [Check which Ekom calls depend on the request](#check-which-ekom-calls-depend-on-the-request).
- The store alias in `StockSetRequest` counts only when `Ekom:PerStoreStock` is `true`. Otherwise every store shares one count and Ekom ignores it.
- `SetStockAsync` replaces the stored count and does not take open reservations into account. Use it when the ERP's number is the truth. For a change by an amount, such as a return, use `IncrementStockAsync(key, "Store", 2m, ct)`. See [Stock](../guides/stock.md#read-and-change-sellable-stock).
- Ekom writes the list in batches of 200, one transaction each. When a later batch fails, the earlier ones stay saved, and the next run sets them again.
- Ekom fills its catalog when Umbraco has started. The service waits one interval before its first run, so it does not find an empty catalog at startup.
- A hosted service runs on every server. With more than one server, every server syncs. Use an Umbraco recurring job, as in the feed example, to run on one server only.

## Mark an order as shipped from a queue message

This handler runs when your warehouse reports that it has shipped an order. It reads the order by ID, records the tracking number, and sets the fulfillment status to Fulfilled. Your queue library calls `WarehouseQueueConsumer` for each message.

```csharp
using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Orders;
using Ekom.Models.Shipments;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyShop.Jobs;

public sealed record WarehouseShippedMessage(Guid OrderId, string TrackingNumber, string? TrackingUrl);

public sealed class WarehouseShippedHandler(
    Order orderApi,
    IOrderShipments shipments,
    IOrderStateService orderStates,
    ILogger<WarehouseShippedHandler> logger)
{
    public async Task HandleAsync(WarehouseShippedMessage message, CancellationToken ct)
    {
        // Reading by ID needs no request and no store. It also returns completed orders.
        IOrderInfo? found = await orderApi.GetOrderAsync(message.OrderId, ct);
        if (found is not OrderInfo order)
        {
            logger.LogWarning("The warehouse shipped order {OrderId}, which does not exist", message.OrderId);
            return;
        }

        // Queues can deliver a message twice. Record each tracking number once.
        if (await shipments.FindOrderAsync(message.TrackingNumber, ct) is null)
        {
            await shipments.CreateAsync(new NewOrderShipment
            {
                OrderUniqueId = order.UniqueId,
                OrderNumber = order.OrderNumber,
                StoreAlias = order.StoreInfo.Alias,
                Kind = ShipmentKind.Outbound,
                State = ShipmentState.Created,
                ProviderKey = order.ShippingProvider?.Key,
                ProviderName = order.ShippingProvider?.Title,
                TrackingNumber = message.TrackingNumber,
                TrackingUrl = message.TrackingUrl,
                Actor = "Warehouse",
            }, ct);
        }

        OrderStatusSnapshot status = order.Status;
        if (status.OrderStatus != OrderStatus.Open || status.FulfillmentStatus == FulfillmentStatus.Fulfilled)
        {
            return;
        }

        try
        {
            // A job has no backoffice user, so it uses ChangeAsSystemAsync. "Fulfill" without quantities
            // fulfills every line. It does not change the payment status.
            await orderStates.ChangeAsSystemAsync(
                order.UniqueId,
                new OrderSystemStateChange("Fulfill", Actor: "Warehouse"),
                ct);
        }
        catch (CheckoutConflictException)
        {
            // A checkout or payment holds the order. Let the queue deliver the message again later.
            logger.LogWarning("Order {OrderNumber} is held by checkout; the shipped status will be retried", order.OrderNumber);
            throw;
        }
    }
}

// Call OnMessageAsync from your queue library's message callback.
public sealed class WarehouseQueueConsumer(IServiceScopeFactory scopeFactory)
{
    public async Task OnMessageAsync(WarehouseShippedMessage message, CancellationToken ct)
    {
        // One scope for each message, as ASP.NET Core does for each request.
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        WarehouseShippedHandler handler = scope.ServiceProvider.GetRequiredService<WarehouseShippedHandler>();
        await handler.HandleAsync(message, ct);
    }
}
```

The composer in the first example registers the handler and the consumer.

`IOrderStateService` is the service behind the Manager's status menus. It has two methods:

| Method | Who it is for | Access check |
| --- | --- | --- |
| `ChangeAsync` | The signed-in backoffice user. | Manager access to the order's store. Without a backoffice user it throws `EkomHttpException` with status `403`. |
| `ChangeAsSystemAsync` | Code with no backoffice user: jobs, queue handlers, integrations. | None. Call it only from trusted server code. |

Both take the same actions: `Fulfill`, `Unfulfilled`, `Picking`, `Picked`, `Packed`, `PartiallyFulfilled`, `PaymentPending`, `PaymentAuthorized`, `PaymentPaid`, `PaymentPartiallyPaid`, `PaymentFailed`, `PaymentVoided`, `Cancel`, `Close` and `Reopen`. See [Order, payment, and fulfillment statuses](../guides/order-statuses.md) for what each one does.

What to know:

- `GetOrderAsync(orderId)` needs no request and no store. It returns any saved order, completed orders included.
- `Fulfill` changes only the fulfillment status. An order that is shipped before it is paid stays unpaid, and stays Open until its payment is recorded.
- After the change, an Open order that is Paid and Fulfilled is closed automatically, unless automatic closing is off for the store or the order was reopened.
- `Actor` is required. It is saved as the user name on the order's activity log entry, so name the job or integration.
- `ChangeAsSystemAsync` checks no access. Never call it with an order ID or an action that came from a storefront request.
- By default the change applies to the order as it is saved at that moment. Pass `ExpectedRevision` with the revision you read from `order.Status.Revision` to get a `409` when someone changed the status in between.
- It raises the status change events, `OrderStatusChangedAsync`, `PaymentStatusChangedAsync` and `FulfillmentStatusChangedAsync`, for each status that changed. Pass `Notify: false` to keep them quiet.
- A rule that is broken throws `EkomHttpException`: `400` for an action the order's state does not allow, such as closing an unpaid order, `404` for an unknown order, and `409` for a revision that no longer matches.
- When a checkout or payment holds the order, the change throws `CheckoutConflictException`. The handler rethrows it so the queue delivers the message again later.
- `FindOrderAsync` on `IOrderShipments` finds the order of a tracking number, so a message that arrives twice records the shipment once. Creating a shipment does not change the fulfillment status.
- Older code sets the legacy status with `Order.UpdateStatusAsync`. That still works, but the legacy statuses cannot express every state: `ReadyForDispatch` also marks the payment as Paid, and `Closed` closes the order without the Paid and Fulfilled check. Use `ChangeAsSystemAsync` in new code.

## Export a product feed on a schedule

This Umbraco recurring job writes a CSV feed of the store's products every 6 hours, one row per variant, for a price comparison site or an ad platform. It runs on one server only.

```csharp
using Ekom.API;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace MyShop.Jobs;

public sealed class ProductFeedJob(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment environment,
    ILogger<ProductFeedJob> logger) : IRecurringBackgroundJob
{
    private const string StoreAlias = "Store";
    private const string Currency = "is-IS";
    private const string Culture = "is-IS";
    private const string SiteUrl = "https://www.example.com";

    public TimeSpan Period => TimeSpan.FromHours(6);

    public TimeSpan Delay => TimeSpan.FromMinutes(5);

    // Write the file on one server only.
    public ServerRole[] ServerRoles => [ServerRole.Single, ServerRole.SchedulingPublisher];

    public event EventHandler PeriodChanged
    {
        add { }
        remove { }
    }

    public async Task RunJobAsync()
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        Catalog catalog = scope.ServiceProvider.GetRequiredService<Catalog>();

        // Without PageSize the response holds every product in the store.
        ProductResponse products = await catalog.GetAllProductsAsync(
            StoreAlias,
            new ProductQuery { OrderBy = OrderBy.SkuAsc });

        var csv = new StringBuilder("sku,title,price,availability,link\n");
        foreach (IProduct product in products.Products)
        {
            string link = SiteUrl + (product.UrlsWithContext.FirstOrDefault(x => x.Culture == Culture)?.Url ?? "/");

            if (!product.AllVariants.Any())
            {
                AddRow(csv, product.SKU, product.Title, product.Prices, product.Available, link);
                continue;
            }

            foreach (IVariant variant in product.AllVariants)
            {
                AddRow(csv, variant.SKU, $"{product.Title} {variant.Title}", variant.Prices, variant.Available, link);
            }
        }

        string folder = Path.Combine(environment.WebRootPath, "feeds");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "products.csv");

        // Write a new file and swap it in, so nobody downloads half a feed.
        await File.WriteAllTextAsync(path + ".tmp", csv.ToString(), Encoding.UTF8);
        File.Move(path + ".tmp", path, overwrite: true);

        logger.LogInformation("Wrote {Count} products of store {StoreAlias} to {Path}", products.ProductCount, StoreAlias, path);
    }

    private static void AddRow(StringBuilder csv, string sku, string title, IEnumerable<IPrice> prices, bool available, string link)
    {
        // Price follows the visitor's currency cookie. A job has no visitor, so pick the currency.
        IPrice? price = prices.FirstOrDefault(x => x.Currency.CurrencyValue == Currency);
        if (price is null)
        {
            return;
        }

        csv.Append(Escape(sku)).Append(',')
            .Append(Escape(title)).Append(',')
            .Append(price.WithVat.Value.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
            .Append(available ? "in stock" : "out of stock").Append(',')
            .Append(Escape(link)).Append('\n');
    }

    private static string Escape(string value)
        => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
```

The composer in the first example registers it with `AddRecurringBackgroundJob`.

What to know:

- `ServerRoles` decides where Umbraco runs the job. `Single` and `SchedulingPublisher` mean one server in a load-balanced site.
- `GetAllProductsAsync("Store", query)` reads the store you name. Without a `PageSize`, the response holds every product.
- A product's `Price` picks the currency from the visitor's cookie or the request culture. A job has neither, so `Price` is always the store's first currency. The example takes the price for one currency from `Prices` instead.
- A product's `Url` is worked out for the request's culture and domain. In a job it uses the culture of the thread, and is empty when no URL has that culture. The example takes the path for one culture from `UrlsWithContext` and adds the site address itself.
- Catalog event handlers and custom product classes run in the job too. Code in them that reads the `HttpContext`, for example member prices, sees no request.
- Ekom's catalog is in memory, so reading every product is fast. Writing the file is the slow part, which is why it runs every few hours.

## Check which Ekom calls depend on the request

Code that runs without a web request gets these results. They come from the source of Ekom on Umbraco 17, 18 and 19.

| Call | Without a request |
| --- | --- |
| `Catalog.GetProductAsync()` and `GetCategoryAsync()` without arguments | Return `null`. They read the product or category Ekom routed the request to. |
| Catalog, provider, discount and store calls without a store alias, for example `GetProductAsync(sku)`, `GetAllProductsAsync(query)` with an empty `StoreAlias`, `GetShippingProvidersAsync()`, `GetDiscounts()` and `Store.GetStore()` | Use the first store, with no error. |
| `Stock` calls without a store alias, when `Ekom:PerStoreStock` is `true` | Read and change the first store's stock. |
| A store alias that does not exist | `Order` calls and single product or category lookups throw `StoreNotFoundException`. `GetAllProductsAsync(storeAlias)` returns no products. `Stock` calls do not check the alias. |
| `Order.GetOrderAsync()`, `GetOrderAsync(storeAlias)` and `GetCompletedOrderAsync(storeAlias)` | Return `null`. There is no basket cookie and no signed-in member. |
| `Order.AddOrderLineAsync` | Throws `NullReferenceException`. With no basket to add to, Ekom starts a new one and tries to write the basket cookie to the response. |
| The other basket changes, such as `UpdateOrderlineQuantityAsync`, `RemoveOrderLineAsync`, `UpdateShippingInformationAsync` and `UpdateCustomerInformationAsync` | Throw `OrderInfoNotFoundException` or `ArgumentException`, because there is no current basket. `ApplyCouponToOrderAsync` returns `false`. |
| `Order.PayAsync` | Throws `ArgumentNullException`. Payment needs a request. |
| `IOrderStateService.ChangeAsync` | Throws `EkomHttpException` with status `403`. Use `IOrderStateService.ChangeAsSystemAsync`, as above. |
| `product.Price`, `variant.Price` | The store's first currency. |
| `product.Url`, `category.Url` | The URL for the thread's culture, or empty. |
| `Order.GetOrderAsync(orderId)`, `UpdateStatusAsync(status, orderId, ...)`, `AddActivityLogAsync`, `UpdateCustomDataAsync(orderId, ...)` | Work. They name the order. |
| `Stock`, `Catalog` and `Providers` calls with a store alias | Work. |

The same applies to work you start from a request and let run after it, for example with `Task.Run`. Ekom's services keep the `HttpContext` of the request they were created in. Basket calls then act on that customer's basket, after the request has ended. Take the services from a new scope instead, and pass the store alias and order ID.

## Related pages

- [Stores](../guides/stores.md): how Ekom finds the store of a request, and why background work passes the alias.
- [Stock](../guides/stock.md): reading and changing stock, and per-store stock.
- [Order, payment, and fulfillment statuses](../guides/order-statuses.md): the three statuses and the legacy status.
- [Orders and baskets](../guides/orders-and-baskets.md): reading orders by ID and the basket methods.
- [Catalog and products](../guides/catalog-and-products.md): product queries and the catalog cache.
- [Services and dependency injection](../reference/services-and-dependency-injection.md): which services you can inject, and their lifetimes.

[Back to Examples](README.md)
