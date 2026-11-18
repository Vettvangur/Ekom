# Events

[Ekom docs](../README.md) / [Reference](README.md) / Events

Ekom raises events at fixed points: when it returns catalog data, when a basket or order changes, during checkout, and when stock or prices change. Subscribe to an event to run your own code at that point, for example to hide products, send an email or notify an ERP system.

This page lists every public event, what its arguments contain, when it is raised, and what happens when a handler throws.

On this page:

- [Subscribe to an event](#subscribe-to-an-event)
- [How Ekom runs handlers](#how-ekom-runs-handlers)
- [Catalog events](#catalog-events)
- [Provider events](#provider-events)
- [Discount events](#discount-events)
- [Order events](#order-events)
- [Checkout events](#checkout-events)
- [Stock events](#stock-events)
- [Price events](#price-events)
- [Tracking events](#tracking-events)
- [Manager action events](#manager-action-events)
- [Import events](#import-events)
- [Write handlers that are safe to run](#write-handlers-that-are-safe-to-run)

## Subscribe to an event

Most events are static events in the `Ekom.Events` namespace. Subscribe once when the site starts and unsubscribe when it stops. In Umbraco, an `IAsyncComponent` added by a composer does both:

```csharp
using Ekom.Events;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

public sealed class StockAlertComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Components().Append<StockAlertComponent>();
}

public sealed class StockAlertComponent : IAsyncComponent
{
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

    private static Task OnStockChangedAsync(object sender, StockChangedEventArgs e, CancellationToken ct)
    {
        if (e.OldValue > 0 && e.NewValue <= 0)
        {
            // The product or variant e.Key just sold out.
        }

        return Task.CompletedTask;
    }
}
```

`DiscountEvents` is the exception: it is a singleton service, not a static class. Take it in the component's constructor. See [Discount events](#discount-events).

## How Ekom runs handlers

These rules apply to every event on this page unless its section says otherwise:

- Handlers run one at a time, in the order they were subscribed.
- If a handler throws, the handlers after it do not run.
- Ekom checks the cancellation token between handlers.
- Where an event has both a synchronous and an asynchronous form, they are separate events. If you subscribe to both, your code runs twice. Use the asynchronous form in new code.
- Each section says whether the event runs before or after the change is saved, and what happens to the change when a handler throws.

## Catalog events

`CatalogEvents` lets you change what the catalog returns: replace an item, hide it, or filter and reorder a list. Guide: [Catalog and products](../guides/catalog-and-products.md).

| Event | Arguments | You can change |
| --- | --- | --- |
| `BeforeReturnProductAsync` | `ProductEventArgs` | `Product`: replace it, or set it to `null` to return no product. |
| `BeforeReturnProductsAsync` | `ProductsEventArgs` | `Products`: filter, replace or reorder the list. |
| `BeforeReturnCategoryAsync` | `CategoryEventArgs` | `Category`: replace it, or set it to `null`. |
| `BeforeReturnCategoriesAsync` | `CategoriesEventArgs` | `Categories`: filter, replace or reorder the list. |
| `CurrencyStringFormat` | `CurrencyStringEventArgs` | `ValueString`: the formatted price text. You also get `Value` and `CultureInfo`. |

- The four `BeforeReturn...Async` events have the signature `Func<TArgs, CancellationToken, ValueTask>`. They have no `sender` argument.
- They run when a `Catalog` method returns a result. Pass `raiseEvent: false` to a `Catalog` method, or set `ProductQuery.RaiseEvents = false`, to skip them.
- In a product list, `BeforeReturnProductsAsync` runs after the query's filters and search, and before the `Filter` predicate, sorting and paging.
- The synchronous events `BeforeReturnProduct`, `BeforeReturnProducts`, `BeforeReturnCategory` and `BeforeReturnCategories` are obsolete. When both forms are subscribed, the asynchronous handlers run first.
- An exception from a handler reaches the code that asked for the catalog data. Over HTTP, the request fails with `500`.

```csharp
CatalogEvents.BeforeReturnProductsAsync += (args, ct) =>
{
    args.Products = args.Products.Where(product => product.Available);
    return ValueTask.CompletedTask;
};
```

## Provider events

`ProviderEvents` lets you change the list of payment or shipping providers a customer can choose from. Guide: [Payment and shipping providers](../guides/providers.md).

| Event | Arguments | You can change |
| --- | --- | --- |
| `BeforeReturnPaymentProvidersAsync` | `ProviderEvents.PaymentProvidersEventArgs`: `Providers`, `StoreAlias` | `Providers` |
| `BeforeReturnShippingProvidersAsync` | `ProviderEvents.ShippingProvidersEventArgs`: `Providers`, `StoreAlias` | `Providers` |

- They run after Ekom has filtered the providers by store, country and order amount.
- The asynchronous methods on `Providers` run the synchronous events (`BeforeReturnPaymentProviders`, `BeforeReturnShippingProviders`) first, then the asynchronous ones. The synchronous `Providers` methods run them in the same order, and wait for the asynchronous handlers to finish. Use the asynchronous methods in new code, so that a slow handler does not block a thread.
- An exception from a handler reaches the caller.

## Discount events

`DiscountEvents` is registered as a singleton. Inject it to subscribe. Its handlers have the signature `Func<object?, TArgs, Task>`. Guide: [Discount APIs and behavior](../guides/discounts/developers.md).

```csharp
using Ekom.Events;
using Umbraco.Cms.Core.Composing;

public sealed class CouponRulesComponent(DiscountEvents discountEvents) : IAsyncComponent
{
    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        discountEvents.BeforeApplyCouponDiscountAsync += OnBeforeApplyCouponAsync;
        return Task.CompletedTask;
    }

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        discountEvents.BeforeApplyCouponDiscountAsync -= OnBeforeApplyCouponAsync;
        return Task.CompletedTask;
    }

    private static Task OnBeforeApplyCouponAsync(object? sender, DiscountEvents.BeforeApplyCouponDiscountEventArgs e)
    {
        if (string.Equals(e.CouponCode?.Trim(), "STAFF50", StringComparison.OrdinalIgnoreCase))
        {
            e.Reject("This code can only be used in the staff shop.");
        }

        return Task.CompletedTask;
    }
}
```

| Event | Arguments | When it runs |
| --- | --- | --- |
| `BeforeEvaluateDiscountsAsync` | `ProductDiscountEvaluationEventArgs`: `Path`, `StoreAlias`, `Price`, `Categories` (all settable), `PricingContext` (read-only) | Before Ekom looks for product discounts that match a product. Ekom uses the values as your handlers leave them. |
| `AfterApplicableDiscountsAsync` | `ProductDiscountApplicableEventArgs`: `Path`, `StoreAlias`, `Price`, `Categories`, `PricingContext`, `ApplicableDiscounts` | After Ekom has found the matching product discounts. You can add to or remove from the `ApplicableDiscounts` list. |
| `BeforeApplyCouponDiscountAsync` | `BeforeApplyCouponDiscountEventArgs`: `CouponCode`, `StoreAlias`, `CancellationToken`, `IsRejected`, `RejectionReason`, `Reject(reason)` | Once for each call to `Order.ApplyCouponToOrderAsync`, before anything else. |

`PricingContext` holds the case-insensitive pricing values active for the calculation, for example those sent with a line to the order-discount integration. It is empty when none are active.

About `BeforeApplyCouponDiscountAsync`:

- It runs before Ekom checks, normalizes or looks up the code, and before it loads the order. `CouponCode` is the raw input and can be `null` or empty. `StoreAlias` is `null` when the caller used the overload without a store alias. No order or discount is given to you.
- Call `Reject(reason)` to stop the coupon. The first rejection wins and no further handlers run. Ekom then throws `CouponApplicationRejectedException` with your reason and does not change the order. Over HTTP, `POST /ekom/order/coupon/apply` returns `400` with `{ "code": "couponApplicationRejected", "message": "..." }`.
- Any other exception from a handler reaches the caller.
- It does not run for line coupons, automatic discounts, `SetCouponCodeAsync`, the order-discount calculation, or direct calls to discount services.

## Order events

`OrderEvents` runs your code when a basket or order changes. Guide: [Orders and baskets](../guides/orders-and-baskets.md).

`OrderSettings` controls whether the events in the first table run for a change made through `Ekom.API.Order`:

- `FireEvents` (default `true`) turns all of them on or off.
- `FireOnOrderUpdatedEvent` (default `true`) turns off `OrderUpdating`, `OrderUpdated` and `CustomerEmailAddedAsync` only. It is always `false` when `FireEvents` is `false`.

| Event | Arguments | When it runs | If a handler throws |
| --- | --- | --- | --- |
| `AddingOrderlineAsync` | `AddingOrderlineEventArgs`: `Settings`, `Product`, `Variant`, `Quantity`, `Action`, `OrderInfo` | Before a line is added. Ekom uses the `Product`, `Variant`, `Quantity`, `Action` and `Settings` your handlers leave. | The line is not added. |
| `AddedOrderlineAsync` | `AddedOrderlineEventArgs`: `OrderInfo`, `OrderLine`, `Settings` | After the line is added, before the order is saved. | The order is not saved. |
| `UpdatedOrderlineAsync` | `UpdatedOrderlineEventArgs`: `OrderInfo` | After a line is added, changed or removed, before the order is saved. Ekom saves the `OrderInfo` your handlers leave. | The order is not saved. |
| `RemovedOrderlineAsync` | `RemovedOrderlineEventArgs`: `OrderInfo`, `OrderLine` | After a line is removed, before the order is saved. | The order is not saved. |
| `OrderUpdatingAsync` | `OrderUpdatingEventArgs`: `OrderInfo` | Before any change to the order is saved. Change the order object itself. | The order is not saved. |
| `OrderUpdatedAsync` | `OrderUpdatedEventArgs`: `OrderInfo` | After the order is saved. | The change stays saved. Depending on the operation, the exception reaches the caller or Ekom logs it as a warning. |
| `CustomerInformationUpdatingAsync` | `CustomerInformationUpdatingEventArgs`: `OrderInfo`, `Form` | During `UpdateCustomerInformationAsync`, after the form values are applied to the order, before it is saved. | The order is not saved. |
| `CustomerInformationUpdatedAsync` | `CustomerInformationUpdatedEventArgs`: `OrderInfo`, `Form` | After the customer information is saved. | The change stays saved; the exception reaches the caller. |
| `CustomerEmailAddedAsync` | `CustomerEmailAddedEventArgs`: `OrderInfo`, `PreviousCustomerEmail`, `NewCustomerEmail` | After a save that gives the order a customer email for the first time. | The change stays saved; the exception reaches the caller. |
| `ShippingProviderAddedAsync` | `ShippingProviderAddedEventArgs`: `OrderInfo` | After the order is saved with a different shipping provider. | The change stays saved; the exception reaches the caller. |
| `PaymentProviderAddedAsync` | `PaymentProviderAddedEventArgs`: `OrderInfo` | After the order is saved with a different payment provider. | The change stays saved; the exception reaches the caller. |

Synchronous forms exist for `AddingOrderline`, `AddedOrderline`, `UpdatedOrderline`, `OrderUpdating` and `OrderUpdated`. Ekom raises the synchronous form just before the asynchronous one.

When you add lines with `AddLinkedOrderLinesAsync`, `AddedOrderlineAsync`, `UpdatedOrderlineAsync` and `OrderUpdatedAsync` run after the order is saved, and a failing handler is logged as a warning.

Order handlers can run while Ekom holds the order's lock. To change the same order from a handler, pass `OrderSettings` with `IsEventHandler = true`, or the call waits for a lock that your own handler holds.

### Payment and shipment events

These two events are raised for every payment and shipment, whatever recorded it. `OrderSettings` does not affect them. Guides: [Payments](../guides/payments.md), [Shipping and shipments](../guides/shipping.md).

| Event | Arguments | When it runs | If a handler throws |
| --- | --- | --- | --- |
| `PaymentRecordedAsync` | `PaymentRecordedEventArgs`: `Payment`, `Transaction`, `PreviousPaymentStatus`, `PaymentStatus` | After a payment transaction is saved and the order's payment status is calculated again. | The payment stays saved. The exception reaches the code that recorded the payment, and the status change events for that save are not raised. |
| `ShipmentRecordedAsync` | `ShipmentRecordedEventArgs`: `Shipment`, `Event`, `PreviousState` | After a shipment is created, or after something that happened to it is saved. `PreviousState` is `null` for a new shipment. Not raised when an imported shipment is created. | The shipment stays saved; the exception reaches the caller. |

### Status change events

An order has three separate statuses, and each has its own event. Guide: [Order, payment, and fulfillment statuses](../guides/order-statuses.md).

| Event | Status | Values |
| --- | --- | --- |
| `OrderStatusChangedAsync` | Order status | `Draft`, `Open`, `Closed`, `Cancelled` |
| `PaymentStatusChangedAsync` | Payment status | `Pending`, `Authorized`, `Paid`, `PartiallyPaid`, `Failed`, `PartiallyRefunded`, `Refunded`, `Voided` |
| `FulfillmentStatusChangedAsync` | Fulfillment status | `Unfulfilled`, `Picking`, `Picked`, `Packed`, `PartiallyFulfilled`, `Fulfilled` |

Each event is raised every time its status changes, whatever changed it: a manager action, a recorded payment, checkout completion, or code that sets the legacy status through `Order.UpdateStatusAsync`. It is raised after the change is saved, and not when a status is set to the value it already has. One save can raise more than one event: fulfilling a paid order that then closes automatically raises `FulfillmentStatusChangedAsync` and `OrderStatusChangedAsync`.

Two callers can keep a change quiet:

- In the manager, the **Trigger status events** switch decides whether a change raises its events. `Ekom:Manager:OrderStatusFireEventsByDefault` sets where the switch starts. See [Manager order-status events](configuration-reference.md#manager-order-status-events).
- In code, `Order.UpdateStatusAsync` with `ChangeOrderSettings { FireEvents = false }` raises none.

The three argument types (`OrderStatusChangedEventArgs`, `PaymentStatusChangedEventArgs`, `FulfillmentStatusChangedEventArgs`) have the same members:

| Member | Type | Description |
| --- | --- | --- |
| `OrderUniqueId` | `Guid` | The order. |
| `PreviousStatus` | the status enum, nullable | The status before the change. `null` when the order's history does not say. |
| `Status` | the status enum | The status the order has now. |
| `OrderInfo` | `IOrderInfo?` | The order as it was saved. `null` when the saved order could not be read. |

The status enums are in `Ekom.Models.Orders`. The events are notifications: a handler cannot change or cancel the status change, and the change stays saved if a handler throws.

```csharp
OrderEvents.FulfillmentStatusChangedAsync += async (sender, e, ct) =>
{
    if (e.Status == FulfillmentStatus.Fulfilled)
    {
        await SendShippedEmailAsync(e.OrderUniqueId, ct);
    }
};
```

## Checkout events

`CheckoutEvents` lets you change how checkout runs. These events control the checkout; they are not only notifications. Each has a synchronous and an asynchronous form, and Ekom raises the synchronous form first. Guide: [Checkout](../guides/checkout.md).

| Event | Arguments | When it runs | You can change |
| --- | --- | --- | --- |
| `ProcessingAsync` | `ProcessingEventArgs`: `OrderInfo`, `StockValidation` (default `true`) | At the start of a payment request, before stock is checked. | Set `StockValidation = false` to skip the stock check. When reservations are on for the store, stock is still reserved. |
| `PaymentOrderItemsPreparingAsync` | `PaymentOrderItemsPreparingEventArgs`: `OrderInfo`, `PaymentRequest`, `OrderItems` | Before the order items are sent to the payment provider. | The `OrderItems` collection. |
| `PayAsync` | `PayEventArgs`: `OrderInfo`, `PaymentSettings`, `CustomData` | Before Ekom hands the payment to the provider. | `PaymentSettings`, for example its URLs. Ekom sets the order identifiers in `PaymentSettings.OrderCustomData` again after your handlers. |
| `CompletingCheckoutAsync` | `CompletingCheckoutEventArgs`: `OrderInfo`, `StockValidation`, `UpdateOrderStatus`, `Payment` | When a paid order is about to be completed, before stock is deducted and the status is updated. | Set `StockValidation = false` to complete without checking stock. Set `UpdateOrderStatus = false` to leave the status unchanged. |

- `UpdateOrderStatus` starts as `false` for an order with the legacy status `OfflinePayment`, and `true` otherwise.
- `Payment` is the payment the order was paid with: provider, transaction ID, amounts and the provider's own details. It is `null` when no payment is recorded, for example when `CompleteOrderAsync` is called directly.
- An exception from a `ProcessingAsync`, `PaymentOrderItemsPreparingAsync` or `PayAsync` handler stops the checkout step it runs in.
- An exception from a `CompletingCheckout` or `CompletingCheckoutAsync` handler does not stop completion. The customer has already paid, so Ekom logs the error, runs the other handlers, completes the order and opens an [order review](../guides/order-reviews.md) with the code `completion-handler-failed`. An `OperationCanceledException` still stops completion.
- The order can still fail to complete after `CompletingCheckoutAsync`. Do not send the order to another system from it.

`CheckoutEvents.CompleteCheckoutAsync` is a notification. Its synchronous form, `CompleteCheckout`, runs first.

| Event | Arguments | When it runs | If a handler throws |
| --- | --- | --- | --- |
| `CompleteCheckoutAsync` | `CompleteCheckoutEventArgs`: `OrderData` and `OrderInfo` (both read again after completion), `Payment` | After an order has been completed: stock is taken, the coupon is counted and the status is set. It is not raised when completion fails. | The order stays completed. Ekom logs the error, runs the other handlers and opens a `completion-handler-failed` review. |

Use `CompleteCheckoutAsync` to send a completed order to another system, send emails or book a shipment. Use `CompletingCheckoutAsync` only to change how an order completes.

`CompleteCheckoutEventArgs` also has `StockValidation` and `UpdateOrderStatus`. They are marked obsolete. They report what completion used, and setting them has no effect, because the order has already completed. Set them in a `CompletingCheckoutAsync` handler.

Turning off stock validation or the status update changes how checkout behaves. Cover it with integration tests.

## Stock events

`StockEvents.StockChangedAsync` runs after a change to sellable stock is saved. Guide: [Stock](../guides/stock.md).

| Member of `StockChangedEventArgs` | Type | Description |
| --- | --- | --- |
| `Key` | `Guid` | The product or variant. |
| `StoreAlias` | `string?` | The store, or `null` when stock is shared by all stores. |
| `OldValue` | `decimal` | Stock before the change. |
| `NewValue` | `decimal` | Stock after the change. |

- It runs for `IncrementStockAsync`, both `SetStockAsync` forms, reservations that take or return stock, and the stock deducted when checkout completes.
- It does not run when the value did not change, or for warehouse stock or discount stock.
- If a handler throws, Ekom logs a warning and the stock change stays saved. Handlers after the failing one do not run. A failed handler is not retried.

## Price events

`PriceEvents.PriceChanged` (an `EventHandler<PriceChangedEventArgs>`) runs after a database price override is saved or removed through `Ekom.API.Prices`. Guide: [Database price storage](../guides/database-price-storage.md).

| Member of `PriceChangedEventArgs` | Type | Description |
| --- | --- | --- |
| `Key` | `Guid` | The product, variant or other node. |
| `StoreAlias` | `string` | The store. |
| `Currency` | `string` | The currency. |
| `PropertyAlias` | `string` | The price property, usually `price`. |
| `OldValue` | `decimal?` | The previous override, or `null` when there was none. |
| `NewValue` | `decimal?` | The new override, or `null` when the override was removed and the node's own price applies again. |

Ekom calls each handler separately. If one throws, Ekom logs a warning and still calls the others. The price stays saved. Keep handlers short: queue the work instead of calling remote systems.

## Tracking events

`TrackingEvents` lets you change or cancel a purchase event before Ekom sends it to Google Analytics 4 or Meta. Guide: [Tracking and consent](../guides/tracking-and-consent.md).

| Event | Arguments |
| --- | --- |
| `Ga4PurchasePreparingAsync` | `Ga4PurchasePreparingEventArgs`: `OrderInfo`, `Request`, `Properties`, `Cancel` |
| `MetaPurchasePreparingAsync` | `MetaPurchasePreparingEventArgs`: `OrderInfo`, `Request`, `Properties`, `Cancel` |

- They run when checkout completes, when tracking and that provider are turned on.
- You can change `Request`. `Properties` is a case-insensitive dictionary for your own values. Set `Cancel = true` to not send the event.
- An exception from a handler reaches checkout completion.

## Manager action events

`OrderManagerEvents.ActionExecutedAsync` runs after a custom manager action succeeds. A manager action is a button in the manager order view, added by an `IOrderManagerActionProvider`. See [Services and dependency injection](services-and-dependency-injection.md#add-actions-to-the-manager-order-view).

| Member of `OrderManagerActionExecutedEventArgs` | Type | Description |
| --- | --- | --- |
| `OrderInfo` | `IOrderInfo` | The order. |
| `ActionKey` | `string` | The action that ran. Check it when you handle one action only, such as a shipping label. |
| `UserName` | `string?` | The backoffice user. |
| `Result` | `OrderManagerActionExecutionResult` | The result, including a file download. |

It does not run for an unknown action or a bad-request result. An exception from a handler makes the manager action request fail.

## Import events

`ImportEvents` belongs to the Umbraco integration (`Ekom.Events` namespace in the `Ekom.U17`, `Ekom.U18` and `Ekom.U19` packages). Its handlers have the signature `Func<TArgs, Task>`. Guide: [Catalog imports](../guides/imports.md).

| Event | Arguments | When it runs |
| --- | --- | --- |
| `CategorySaveStarting` | `ImportCategoryEventArgs`: `CategoryContent`, `ImportCategory`, `IsCreateOperation`, `ImagesHaveNoChanges` | Before a category is saved. |
| `ProductImportEvaluating` | `ImportProductEvaluatingEventArgs`: `ImportProduct`, `ProductContent`, `ImportRootKey`, `ExcludeFromImport` | Before Ekom compares and saves a product, in full and category imports, `ProductSync` and `ProductUpdateSync`. |
| `ProductSaveStarting` | `ImportProductEventArgs`: `ProductContent`, `ImportProduct`, `IsCreateOperation`, `ImagesHaveNoChanges`, `FilesHaveNoChanges` | Before a product is saved. |
| `VariantSaveStarting` | `ImportVariantEventArgs`: `VariantContent`, `ImportVariant`, `IsCreateOperation`, `ImagesHaveNoChanges`, `FilesHaveNoChanges` | Before a variant is saved. |
| `SyncFinished` | `ImportSyncFinishedEventArgs`: `ImportCategories`, `ImportProducts`, `ImportVariants`, `ImportVariantGroups`, `Type` | After a sync has finished. |

About `ProductImportEvaluating`:

- `ImportProduct` is the incoming product. Do not change it here.
- `ProductContent` is the existing Umbraco node the import matched, or `null`. Ekom does not load content again for this event.
- `ImportRootKey` is the key of the import root, for handlers that only apply to one catalog.
- Set `ExcludeFromImport = true` to leave the product and its variants out of the import. Once one handler sets it, a later handler cannot set it back to `false`.
- With removal of missing products turned on, an existing product you exclude is treated as missing and removed as usual. A single-product import skips the product without removing existing content. Nodes marked `ekmDisableSync` stay protected.
- An exception stops the import before products are compared. Categories already processed stay saved.
- Imports of variants only do not raise this event.

## Write handlers that are safe to run

- Pass on and honor the cancellation token.
- Make handlers idempotent. Payment callbacks and integrations can send the same thing twice.
- Do not call slow remote systems while Ekom holds an order's lock. Queue the work and process it elsewhere.
- Never log payment details, API keys, card data or customer data you do not need.

## Related pages

- [.NET API](dotnet-api.md): the methods that raise these events.
- [Services and dependency injection](services-and-dependency-injection.md): services you can replace instead of handling an event.
- [Orders and baskets](../guides/orders-and-baskets.md): how order changes and their events fit together.
- [Checkout](../guides/checkout.md): where each checkout event runs in the payment flow.
- [Order, payment, and fulfillment statuses](../guides/order-statuses.md): what each status means.

[Back to Reference](README.md)
