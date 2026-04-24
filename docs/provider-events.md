# Provider Events

Provider events let you filter or replace the payment and shipping providers returned by Ekom.

These are useful when provider availability depends on business rules that are specific to your solution.

## When to use provider events

Use provider events when you want to:

- hide providers for a specific store
- filter providers based on custom logic
- reorder the returned providers
- replace the provider list before it is returned

## Recommended registration pattern

Register provider handlers during startup through an Umbraco component.

```csharp
using Ekom.Events;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class ProviderEventHandlers : IComponent
{
    public void Initialize()
    {
        ProviderEvents.BeforeReturnPaymentProvidersAsync += OnBeforeReturnPaymentProvidersAsync;
    }

    public void Terminate()
    {
        ProviderEvents.BeforeReturnPaymentProvidersAsync -= OnBeforeReturnPaymentProvidersAsync;
    }

    private Task OnBeforeReturnPaymentProvidersAsync(object sender, ProviderEvents.PaymentProvidersEventArgs args, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
```

## Available events

### `BeforeReturnPaymentProvidersAsync`

Runs before the payment provider collection is returned.

Use this when payment provider availability depends on store-specific or custom rules.

### `BeforeReturnShippingProvidersAsync`

Runs before the shipping provider collection is returned.

Use this when shipping provider availability depends on store-specific or custom rules.

## Example

This example removes one payment provider from a specific store.

```csharp
using Ekom.Events;
using System.Linq;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class ProviderEventHandlers : IComponent
{
    public void Initialize()
    {
        ProviderEvents.BeforeReturnPaymentProvidersAsync += OnBeforeReturnPaymentProvidersAsync;
    }

    public void Terminate()
    {
        ProviderEvents.BeforeReturnPaymentProvidersAsync -= OnBeforeReturnPaymentProvidersAsync;
    }

    private Task OnBeforeReturnPaymentProvidersAsync(object sender, ProviderEvents.PaymentProvidersEventArgs args, CancellationToken ct)
    {
        if (!string.Equals(args.StoreAlias, "store1", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        args.Providers = args.Providers.Where(x => x.Alias != "invoice");
        return Task.CompletedTask;
    }
}
```

## Notes

- Provider events expose both sync and async variants, but async is the preferred pattern.
- The event args let you replace the full provider collection.
- These events run before providers are returned from the API.

## Related pages

- [Payment Provider Selection](payment-provider-selection.md)
- [Shipping Provider Selection](shipping-provider-selection.md)
- [Payment Providers Overview](payment-providers-overview.md)
- [Shipping Providers Overview](shipping-providers-overview.md)
