namespace Ekom.Models;

internal static class DiscountValueCalculator
{
    internal static decimal CalculateDiscountAmount(decimal price, IDiscount discount)
    {
        ArgumentNullException.ThrowIfNull(discount);

        return discount.Type switch
        {
            DiscountType.Fixed => discount.Amount,
            DiscountType.Percentage => price * discount.Amount,
            _ => 0,
        };
    }

    internal static bool IsBetterDiscount(decimal price, IDiscount candidate, IDiscount? current)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return current == null
            || CalculateDiscountAmount(price, candidate) > CalculateDiscountAmount(price, current);
    }

    internal static bool IsBetterLineDiscount(
        IPrice price,
        IDiscount candidate,
        IDiscount current,
        decimal vat,
        bool vatIncludedInPrice,
        decimal quantity)
    {
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(current);

        decimal candidateTotal = CreatePrice(candidate).Value;
        decimal currentTotal = CreatePrice(current).Value;
        return candidateTotal < currentTotal;

        Price CreatePrice(IDiscount discount)
            => new(
                discount.Stackable ? price.Value : price.OriginalValue,
                price.Currency,
                vat,
                vatIncludedInPrice,
                discount as OrderedDiscount ?? new OrderedDiscount(discount),
                quantity);
    }
}
