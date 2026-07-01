using Ekom.Models;

namespace Ekom.Utilities;

internal static class StockBufferHelper
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

    private static decimal? GetConfiguredStockBuffer(decimal? stockBuffer)
    {
        return stockBuffer is > 0 ? stockBuffer : null;
    }
}
