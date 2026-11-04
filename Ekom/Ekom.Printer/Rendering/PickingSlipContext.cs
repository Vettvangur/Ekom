using Ekom.Utilities;
using Ekom.Models;

namespace Ekom.Printer.Rendering;

/// <summary>A persisted snapshot; rendering never needs to reload a changing order.</summary>
public sealed record PickingSlipContext(
    Guid OrderId,
    string OrderNumber,
    string StoreAlias,
    string StationAlias,
    OrderStatus TriggerStatus,
    DateTime CreatedUtc,
    IReadOnlyList<PickingSlipLine> Lines)
{
    /// <summary>Client-specific captured values, such as delivery details and warehouse references.</summary>
    public IReadOnlyDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();
}

public sealed record PickingSlipLine(string Sku, string Product, string? Variant, decimal Quantity)
{
    public IReadOnlyDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();
}

/// <summary>Customize the stored snapshot while the original order is available.</summary>
public interface IPickingSlipContextFactory
{
    PickingSlipContext Create(IOrderInfo order, string stationAlias, OrderStatus triggerStatus);
}

internal sealed class PickingSlipContextFactory : IPickingSlipContextFactory
{
    public PickingSlipContext Create(IOrderInfo order, string stationAlias, OrderStatus triggerStatus)
        => new(order.UniqueId, order.OrderNumber, order.StoreInfo.Alias, stationAlias, triggerStatus, DateTime.UtcNow,
            order.OrderLines.Select(line => new PickingSlipLine(line.Variant?.SKU ?? line.Product.SKU,
                line.Product.Title, line.Variant?.Title, line.Quantity)).ToArray());
}

/// <summary>Return a complete, self-contained HTML document. Encode untrusted values.</summary>
public interface IPickingSlipHtmlRenderer
{
    Task<string> RenderAsync(PickingSlipContext context, CancellationToken cancellationToken = default);
}
