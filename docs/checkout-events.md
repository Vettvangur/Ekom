# Checkout Events

Checkout events let you hook into the payment and completion flow.

Use them when you need to extend checkout behavior without rewriting the checkout pipeline itself.

## When to use checkout events

Use checkout events when you want to:

- add custom data before payment starts
- change checkout processing behavior
- validate something before completion
- trigger side effects when checkout completes

## Recommended registration pattern

Register checkout handlers through an Umbraco component.

```csharp
using Ekom.Events;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class CheckoutEventHandlers : IComponent
{
    public void Initialize()
    {
        CheckoutEvents.CompleteCheckoutAsync += OnCompleteCheckoutAsync;
    }

    public void Terminate()
    {
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
    }

    private Task OnCompleteCheckoutAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
```

## Available events

### `PayAsync`

Runs when Ekom starts the payment flow.

Use this when you need to add custom payment data or inspect the order before payment starts.

### `ProcessingAsync`

Runs during checkout processing.

Use this when you need to change behavior such as stock validation.

### `CompleteCheckoutAsync`

Runs when checkout is being completed.

Use this when you want to trigger custom business logic after a successful checkout flow.

## Example

This example disables stock validation during checkout completion.

```csharp
using Ekom.Events;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class CheckoutEventHandlers : IComponent
{
    public void Initialize()
    {
        CheckoutEvents.CompleteCheckoutAsync += OnCompleteCheckoutAsync;
    }

    public void Terminate()
    {
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
    }

    private Task OnCompleteCheckoutAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct)
    {
        args.StockValidation = false;
        return Task.CompletedTask;
    }
}
```

## Notes

- Checkout events expose both sync and async handlers, but async is the preferred pattern.
- `CompleteCheckoutAsync` is already used internally by Ekom tracking and plugins.
- Be careful when changing `StockValidation` or `UpdateOrderStatus`, because that affects core checkout behavior.

## Related pages

- [Checkout Overview](checkout-overview.md)
- [Checkout Flow](checkout-flow.md)
- [Complete Checkout](complete-checkout.md)
- [Tracking Events](tracking-events.md)
