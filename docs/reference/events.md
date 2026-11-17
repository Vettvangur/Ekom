# Events Reference

Ekom exposes static lifecycle events plus a singleton `DiscountEvents` service. Subscribe once during application startup and unsubscribe during shutdown. Async handlers execute sequentially in subscription order. Exceptions from handlers raised directly in a mutation pipeline can fail that operation; post-commit stock notification failures are caught and logged instead.

```csharp
public sealed class EkomEventComponent : IComponent
{
    public void Initialize() => StockEvents.StockChangedAsync += OnStockChangedAsync;
    public void Terminate() => StockEvents.StockChangedAsync -= OnStockChangedAsync;

    private static Task OnStockChangedAsync(
        object sender,
        StockChangedEventArgs args,
        CancellationToken ct) => Task.CompletedTask;
}
```

## Catalog events

`CatalogEvents` has async-first transform hooks:

| Event | Mutable result |
| --- | --- |
| `BeforeReturnProductAsync` | `ProductEventArgs.Product`; set to another product or `null`. |
| `BeforeReturnProductsAsync` | `ProductsEventArgs.Products`; filter, replace, or reorder. |
| `BeforeReturnCategoryAsync` | `CategoryEventArgs.Category`; replace or suppress. |
| `BeforeReturnCategoriesAsync` | `CategoriesEventArgs.Categories`; filter, replace, or reorder. |
| `CurrencyStringFormat` | `CurrencyStringEventArgs.ValueString` using the supplied value/culture. |

The four legacy synchronous `BeforeReturn...` events are obsolete. An async raise invokes async handlers first and legacy sync handlers second. A sync catalog API bridges through the async pipeline synchronously. Callers can suppress product/category events with `raiseEvent: false`; product collections can use `ProductQuery.RaiseEvents = false`.

## Provider events

`ProviderEvents.BeforeReturnPaymentProvidersAsync` and `BeforeReturnShippingProvidersAsync` receive mutable provider collections and `StoreAlias`. They run after store, country, amount-range filtering. The synchronous counterparts remain available; the async pipeline invokes sync handlers before async handlers.

## Discount events

`DiscountEvents` is an injected singleton, not a static class:

```csharp
public sealed class DiscountHooks(DiscountEvents events) : IComponent
{
    public void Initialize() => events.AfterApplicableDiscountsAsync += AfterAsync;
    public void Terminate() => events.AfterApplicableDiscountsAsync -= AfterAsync;

    private static Task AfterAsync(
        object? sender,
        DiscountEvents.ProductDiscountApplicableEventArgs args) => Task.CompletedTask;
}
```

| Event | Data and behavior |
| --- | --- |
| `BeforeEvaluateDiscountsAsync` | Mutable path, store alias, price, categories; read-only ambient `PricingContext`. |
| `AfterApplicableDiscountsAsync` | Matching `ApplicableDiscounts` list plus path/store/price/categories and pricing context. The list can be edited. |
| `BeforeApplyCouponDiscountAsync` | Raw `CouponCode`, raw nullable `StoreAlias`, and `CancellationToken`. Call `Reject(reason)` to prevent whole-order coupon application. |

Cancellation is checked between handlers, although the delegate itself receives no cancellation-token parameter. The coupon event also exposes the token in its arguments and checks cancellation after each handler.

`BeforeApplyCouponDiscountAsync` runs once per whole-order coupon API call, before input checks, normalization, coupon/discount lookup, availability checks, or order loading. For the overload without a store alias, `StoreAlias` is null and the current store has not yet been resolved. Inputs can be null, empty, or invalid; handlers must treat them as unvalidated data. No order or resolved discount is supplied at this stage.

Handlers run sequentially; the first rejection stops dispatch and preserves its reason. The API throws `CouponApplicationRejectedException` with that message without mutating the order. HTTP application returns 400 with `{ "code": "couponApplicationRejected", "message": "..." }`. Other handler exceptions propagate normally. This event does not run for line-level coupons, automatic/global discounts, calculation previews, merely setting a coupon code, or direct discount-service calls.

## Order events

`OrderEvents` exposes these async events:

- `OrderUpdatingAsync` and `OrderUpdatedAsync`
- `CustomerEmailAddedAsync`
- `CustomerInformationUpdatingAsync` and `CustomerInformationUpdatedAsync`
- `OrderStatusChangedAsync`, `PaymentStatusChangedAsync`, and `FulfillmentStatusChangedAsync`, one for each of an order's three statuses. See [Status change events](#status-change-events).
- `AddingOrderlineAsync`, `AddedOrderlineAsync`, `RemovedOrderlineAsync`, and `UpdatedOrderlineAsync`
- `ShippingProviderAddedAsync` and `PaymentProviderAddedAsync`
- `PaymentRecordedAsync`, raised after a payment transaction was stored on the order and the payment status recalculated. Its arguments carry the payment, the transaction, and the payment status before and after. See [Payments](../guides/payments.md).
- `ShipmentRecordedAsync`, raised after a shipment was created or something that happened to it was stored. Its arguments carry the shipment, the event, and the shipment state before it. See [Shipping and shipments](../guides/shipping.md).

Legacy synchronous variants exist for order updated/updating and adding/added/updated lines. They are distinct notifications; subscribing to both can run code twice when a workflow raises both.

Important mutable arguments:

- `AddingOrderlineEventArgs` can change settings, product, variant, quantity, action, and order.
- Customer updating/updated arguments carry a replaceable `OrderInfo` and form dictionary.
- `OrderSettings.FireEvents` is the master switch for mutations made through `Order`; `FireOnOrderUpdatedEvent` narrows it further. `Order.UpdateStatusAsync` with `FireEvents = false` raises no status change events.

Order handlers may execute inside order mutation locking. Do not recursively mutate the same order unless using the intended `OrderSettings.IsEventHandler` path and understanding the lock behavior.

### Status change events

An order has three independent statuses, and each has its own event:

| Event | Status | Values |
| --- | --- | --- |
| `OrderStatusChangedAsync` | Order status | `Draft`, `Open`, `Closed`, `Cancelled` |
| `PaymentStatusChangedAsync` | Payment status | `Pending`, `Authorized`, `Paid`, `PartiallyPaid`, `Failed`, `PartiallyRefunded`, `Refunded`, `Voided` |
| `FulfillmentStatusChangedAsync` | Fulfilment status | `Unfulfilled`, `Picking`, `Picked`, `Packed`, `PartiallyFulfilled`, `Fulfilled` |

Each event is raised every time its status changes, whatever changed it: a manager action, a recorded payment, checkout completion, or code that sets the legacy status through `Order.UpdateStatusAsync`. It is raised after the change is stored, and not when a status is set to the value it already has.

Two callers can keep a change quiet. In the manager, the **Trigger status events** switch on a status menu decides whether that change raises its events; `ManagerOrderStatusFireEventsByDefault` sets where the switch starts. In code, `Order.UpdateStatusAsync` with `ChangeOrderSettings.FireEvents = false` raises none. One save can raise more than one event: fulfilling a paid order that closes automatically raises `OrderStatusChangedAsync` and `FulfillmentStatusChangedAsync`.

The arguments (`OrderStatusChangedEventArgs`, `PaymentStatusChangedEventArgs`, `FulfillmentStatusChangedEventArgs`) have the same shape:

- `OrderUniqueId`: the order.
- `PreviousStatus`: the status before the change. Null when the order's history did not say.
- `Status`: the status the order has now.
- `OrderInfo`: the order as it was stored. Null when the stored order could not be read.

```csharp
OrderEvents.FulfillmentStatusChangedAsync += async (sender, e, ct) =>
{
    if (e.Status == FulfillmentStatus.Fulfilled)
    {
        await SendShippedEmailAsync(e.OrderUniqueId, ct);
    }
};
```

The statuses are the enums in `Ekom.Models.Orders`. The events are notifications: a handler cannot change or cancel the status change.

These events replace `OrderStatusChanging`, `OrderStatusChanged` and their async variants, which reported the legacy `Ekom.Utilities.OrderStatus`. `OrderStatusEventArgs` and `ChangeOrderSettings.FireOnOrderStatusChangingEvent` are removed with them.

## Checkout events

Each checkout hook has sync and async forms:

| Event | Mutable controls |
| --- | --- |
| `Pay[Async]` | `OrderInfo`, `PaymentSettings`, and `CustomData`. |
| `Processing[Async]` | `OrderInfo`; set `StockValidation` false to bypass that stage. |
| `PaymentOrderItemsPreparing[Async]` | `OrderInfo`, `PaymentRequest`, and mutable payment `OrderItems`. |
| `CompleteCheckout[Async]` | `OrderData`, `OrderInfo`, `StockValidation`, and `UpdateOrderStatus`. |

These are pipeline controls, not passive notifications. Disabling validation or status updates changes checkout correctness and should be covered by integration tests.

## Stock events

`StockEvents.StockChangedAsync` fires after successful primary stock set/increment operations. `StockChangedEventArgs` contains item `Key`, nullable `StoreAlias` (`null` for global stock), `OldValue`, and `NewValue`.

Warehouse balances and discount stock do not publish this event.

## Tracking events

`TrackingEvents.Ga4PurchasePreparingAsync` and `MetaPurchasePreparingAsync` run before purchase payload dispatch. Their arguments contain mutable `OrderInfo` and request objects, a case-insensitive `Properties` dictionary, and `Cancel`. Setting `Cancel = true` suppresses that dispatch.

## Manager action events

`OrderManagerEvents.ActionExecutedAsync` runs after an `IOrderManagerActionProvider` returns a successful result, including a downloadable file. Its arguments include the order, action key, backoffice user name, and execution result. It does not run for unknown actions or bad-request results. Filter by action key when handling a specific action, such as a carrier label. Handler failures fail the manager action response.

## Import events

The Umbraco integrations expose `ImportEvents.CategorySaveStarting`, `ProductImportEvaluating`, `ProductSaveStarting`, `VariantSaveStarting`, and `SyncFinished`.

`ProductImportEvaluating` is a `Func<ImportProductEvaluatingEventArgs, Task>` event raised before product reconciliation and saving in full/category imports, `ProductSync`, and `ProductUpdateSync`. Its arguments expose:

| Property | Behavior |
| --- | --- |
| `ImportProduct` | Incoming product being evaluated. Treat it as read-only during evaluation. |
| `ProductContent` | Nullable matching existing content from the current import lookup; no additional content-service reads. |
| `ImportRootKey` | Resolved import root key, for catalog-scoped handlers. |
| `ExcludeFromImport` | Initially false; once set to true, later handlers cannot reverse exclusion. |

Exclusion removes a product and its variants from the effective incoming feed. With missing-product removal enabled, existing excluded products follow normal recycle/deletion behavior; single-product imports skip saving without removing existing content. Original-feed safeguards and `ekmDisableSync` protection remain unchanged. Subscriber exceptions propagate before product reconciliation begins, but do not roll back earlier category processing. Direct variant-only imports do not raise this event.

See the [import guide](../guides/imports.md#product-eligibility) for lookup scope, all-excluded feeds, and registration guidance.

## Handler guidance

- Honor cancellation tokens in static async events.
- Keep handlers idempotent; checkout/payment callbacks and integrations can retry.
- Avoid slow network work while order mutations are locked. Queue durable work where appropriate.
- Never log payment secrets, API keys, card data, or unnecessary customer information.
- Use async hooks rather than obsolete sync catalog hooks in new code.

## Related reference

- [.NET API](dotnet-api.md)
- [Services and DI](services-and-dependency-injection.md)
- [Models and behavior](models-and-behavior.md)
