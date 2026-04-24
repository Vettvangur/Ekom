# Discount Events

Discount events let you inspect discount evaluation and the discounts that Ekom considers applicable.

These events are useful when you want to add custom discount logic around the product discount pipeline.

## When to use discount events

Use discount events when you want to:

- inspect the product discount evaluation input
- add custom logging around discount evaluation
- inspect which discounts became applicable

## Recommended registration pattern

Discount events are not static. They are registered on the `DiscountEvents` service instance.

```csharp
using Ekom.Events;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class DiscountEventHandlers : IComponent
{
    private readonly DiscountEvents _discountEvents;

    public DiscountEventHandlers(DiscountEvents discountEvents)
    {
        _discountEvents = discountEvents;
    }

    public void Initialize()
    {
        _discountEvents.BeforeEvaluateDiscounts += OnBeforeEvaluateDiscounts;
    }

    public void Terminate()
    {
        _discountEvents.BeforeEvaluateDiscounts -= OnBeforeEvaluateDiscounts;
    }

    private void OnBeforeEvaluateDiscounts(object? sender, DiscountEvents.ProductDiscountEvaluationEventArgs args)
    {
    }
}
```

## Available events

### `BeforeEvaluateDiscounts`

Runs before Ekom evaluates product discounts.

Use this when you want to inspect the incoming path, store alias, price, or categories.

### `AfterApplicableDiscounts`

Runs after Ekom has resolved the applicable product discounts.

Use this when you want to inspect which discounts matched.

## Example

This example logs when discount evaluation runs for a product path.

```csharp
using Ekom.Events;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;

namespace MySite;

public sealed class DiscountEventHandlers : IComponent
{
    private readonly DiscountEvents _discountEvents;
    private readonly ILogger<DiscountEventHandlers> _logger;

    public DiscountEventHandlers(DiscountEvents discountEvents, ILogger<DiscountEventHandlers> logger)
    {
        _discountEvents = discountEvents;
        _logger = logger;
    }

    public void Initialize()
    {
        _discountEvents.BeforeEvaluateDiscounts += OnBeforeEvaluateDiscounts;
    }

    public void Terminate()
    {
        _discountEvents.BeforeEvaluateDiscounts -= OnBeforeEvaluateDiscounts;
    }

    private void OnBeforeEvaluateDiscounts(object? sender, DiscountEvents.ProductDiscountEvaluationEventArgs args)
    {
        _logger.LogInformation("Evaluating discounts for {Path} in store {StoreAlias}", args.Path, args.StoreAlias);
    }
}
```

## Notes

- Discount events currently use a service-based sync event model.
- These events are best for observing and extending discount evaluation, not for replacing the whole discount system.

## Related pages

- [Discounts Overview](discounts-overview.md)
- [Order Discounts](order-discounts.md)
- [Product Discounts](product-discounts.md)
- [Coupon Discounts](coupon-discounts.md)
