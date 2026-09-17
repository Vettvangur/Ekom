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

Cancellation is checked between handlers, although the delegate itself receives no cancellation-token parameter.

## Order events

`OrderEvents` exposes these async events:

- `OrderUpdatingAsync` and `OrderUpdatedAsync`
- `CustomerEmailAddedAsync`
- `CustomerInformationUpdatingAsync` and `CustomerInformationUpdatedAsync`
- `OrderStatusChangingAsync` and `OrderStatusChangedAsync`
- `AddingOrderlineAsync`, `AddedOrderlineAsync`, `RemovedOrderlineAsync`, and `UpdatedOrderlineAsync`
- `ShippingProviderAddedAsync` and `PaymentProviderAddedAsync`

Legacy synchronous variants exist for order updated/updating, status changing/changed, adding/added/updated lines. They are distinct notifications; subscribing to both can run code twice when a workflow raises both.

Important mutable arguments:

- `OrderStatusEventArgs.Status` can change the requested status; `ClearCustomerOrderReference` defaults to true.
- `AddingOrderlineEventArgs` can change settings, product, variant, quantity, action, and order.
- Customer updating/updated arguments carry a replaceable `OrderInfo` and form dictionary.
- `OrderSettings.FireEvents` is the master switch for mutations made through `Order`; `FireOnOrderUpdatedEvent` and `ChangeOrderSettings.FireOnOrderStatusChangingEvent` narrow it further.

Order handlers may execute inside order mutation locking. Do not recursively mutate the same order unless using the intended `OrderSettings.IsEventHandler` path and understanding the lock behavior.

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
