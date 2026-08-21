namespace Ekom.Services;

/// <summary>
/// Injectable accessor for the ambient <see cref="PricingContext"/> activated per line during
/// order discount calculation. Prefer reading the context from pricing event args
/// (<c>DiscountEvents.*EventArgs.PricingContext</c>, <c>PriceCache.PriceGenerationEventArgs.PricingContext</c>);
/// this type exists for code that does not run inside one of those events.
/// </summary>
public sealed class OrderDiscountCalculationContextAccessor
{
    public IReadOnlyDictionary<string, string>? Current => PricingContext.Current;

    public IDisposable? Activate(IReadOnlyDictionary<string, string>? pricingContext)
        => PricingContext.Activate(pricingContext);
}
