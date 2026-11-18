using Ekom.Models;

namespace Ekom.Utilities;

public static class StockBufferHelper
{
    public static decimal GetEffectiveStock(decimal stock, IProduct product, IVariant? variant = null)
    {
        var bufferStock = GetConfiguredStockBuffer(product, variant);

        return Math.Max(0, stock - bufferStock);
    }

    public static decimal GetConfiguredStockBuffer(IProduct product, IVariant? variant = null)
    {
        return GetConfiguredStockBuffer(variant?.StockBuffer)
            ?? GetConfiguredStockBuffer(product.StockBuffer)
            ?? product.CategoryAncestors?
                .Select(c => GetConfiguredStockBuffer(c.StockBuffer))
                .FirstOrDefault(x => x.HasValue)
            ?? 0;
    }

    /// <summary>
    /// Whether an item may be sold beyond its stock. Backorder on the product covers all of its variants,
    /// and a variant can also turn it on for itself.
    /// </summary>
    public static bool AllowsBackorder(IProduct product, IVariant? variant = null)
        => product.Backorder || variant?.Backorder == true;

    /// <inheritdoc cref="AllowsBackorder(IProduct, IVariant?)"/>
    public static bool AllowsBackorder(IOrderLine line)
        => line.Product.Backorder || line.Variant?.Backorder == true;

    private static decimal? GetConfiguredStockBuffer(decimal? stockBuffer)
    {
        return stockBuffer is > 0 ? stockBuffer : null;
    }
}
