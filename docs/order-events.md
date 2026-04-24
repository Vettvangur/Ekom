# Order Events

Order events let you hook into order updates, customer data changes, status changes, and order line changes.

These are some of the most useful integration points in Ekom.

## When to use order events

Use order events when you want to:

- react to order status changes
- react when an order line is added or updated
- trigger integrations when customer data changes
- run logic when an order is updated

## Recommended registration pattern

Register handlers during startup through an Umbraco component.

```csharp
using Ekom.Events;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class OrderEventHandlers : IComponent
{
    public void Initialize()
    {
        OrderEvents.OrderStatusChangedAsync += OnOrderStatusChangedAsync;
    }

    public void Terminate()
    {
        OrderEvents.OrderStatusChangedAsync -= OnOrderStatusChangedAsync;
    }

    private Task OnOrderStatusChangedAsync(object sender, OrderStatusEventArgs args, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
```

## Available events

### Order updates

- `OrderUpdatedAsync`
- `OrderUpdatingAsync`

Use these when you need to react before or after an order update.

### Customer events

- `CustomerEmailAddedAsync`
- `CustomerInformationUpdatingAsync`
- `CustomerInformationUpdatedAsync`

Use these when checkout or customer data should trigger custom logic.

### Order status events

- `OrderStatusChangingAsync`
- `OrderStatusChangedAsync`

Use these when you need to run logic before or after a status transition.

### Order line events

- `AddingOrderlineAsync`
- `AddedOrderlineAsync`
- `UpdatedOrderlineAsync`

Use these when you want to react to cart and order line changes.

## Example

This example reacts when an order status changes.

```csharp
using Ekom.Events;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class OrderEventHandlers : IComponent
{
    private readonly ILogger<OrderEventHandlers> _logger;

    public OrderEventHandlers(ILogger<OrderEventHandlers> logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        OrderEvents.OrderStatusChangedAsync += OnOrderStatusChangedAsync;
    }

    public void Terminate()
    {
        OrderEvents.OrderStatusChangedAsync -= OnOrderStatusChangedAsync;
    }

    private Task OnOrderStatusChangedAsync(object sender, OrderStatusEventArgs args, CancellationToken ct)
    {
        _logger.LogInformation("Order {OrderUniqueId} changed from {PreviousStatus} to {Status}", args.OrderUniqueId, args.PreviousStatus, args.Status);
        return Task.CompletedTask;
    }
}
```

## Notes

- Order events expose both sync and async variants in several places, but async is the preferred pattern.
- Some event args are mutable and can affect behavior.
- Order events are heavily used by integrations such as Klaviyo.

## Related pages

- [Orders Overview](orders-overview.md)
- [Order Lifecycle](order-lifecycle.md)
- [Create an Order Activity Log](create-order-activity-log.md)
- [Checkout Events](checkout-events.md)
