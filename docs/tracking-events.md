# Tracking Events

Tracking events let you modify or cancel tracking payloads before they are dispatched.

These events are useful when you need to enrich GA4 or Meta purchase tracking with custom fields, or stop dispatch in special cases.

## When to use tracking events

Use tracking events when you want to:

- add custom fields to tracking payloads
- inspect outgoing purchase tracking requests
- cancel a GA4 or Meta dispatch
- customize tracking behavior per store or order

## Recommended registration pattern

Register tracking handlers during startup through an Umbraco component.

```csharp
using Ekom.Events;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class TrackingEventHandlers : IComponent
{
    public void Initialize()
    {
        TrackingEvents.Ga4PurchasePreparingAsync += OnGa4PurchasePreparingAsync;
    }

    public void Terminate()
    {
        TrackingEvents.Ga4PurchasePreparingAsync -= OnGa4PurchasePreparingAsync;
    }

    private Task OnGa4PurchasePreparingAsync(object sender, Ga4PurchasePreparingEventArgs args, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
```

## Available events

### `Ga4PurchasePreparingAsync`

Runs before a GA4 purchase request is dispatched.

Use this when you need to enrich or cancel the outgoing GA4 payload.

### `MetaPurchasePreparingAsync`

Runs before a Meta purchase request is dispatched.

Use this when you need to enrich or cancel the outgoing Meta payload.

## Example

This example cancels GA4 dispatch for a specific store.

```csharp
using Ekom.Events;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class TrackingEventHandlers : IComponent
{
    public void Initialize()
    {
        TrackingEvents.Ga4PurchasePreparingAsync += OnGa4PurchasePreparingAsync;
    }

    public void Terminate()
    {
        TrackingEvents.Ga4PurchasePreparingAsync -= OnGa4PurchasePreparingAsync;
    }

    private Task OnGa4PurchasePreparingAsync(object sender, Ga4PurchasePreparingEventArgs args, CancellationToken ct)
    {
        if (args.OrderInfo.StoreInfo.Alias == "internal-store")
        {
            args.Cancel = true;
        }

        return Task.CompletedTask;
    }
}
```

## Notes

- Tracking events are async-only.
- They run after the purchase request has been created but before it is queued for dispatch.
- Tracking automation already hooks into checkout completion internally.

## Related pages

- [Tracking Overview](tracking-overview.md)
- [CookieHub Integration](cookiehub-integration.md)
- [Configuration](configuration.md)
- [Checkout Events](checkout-events.md)
