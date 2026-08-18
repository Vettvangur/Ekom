using Ekom.Exceptions;
using Ekom.Models;

namespace Ekom.Services;

internal static class OrderLineVariantValidator
{
    public static void Validate(IProduct product, IVariant? variant)
    {
        if (variant is null)
        {
            if (product.AllVariants.Any())
                throw new VariantRequiredException($"A variant is required for product '{product.Key}'.");
        }
    }
}
