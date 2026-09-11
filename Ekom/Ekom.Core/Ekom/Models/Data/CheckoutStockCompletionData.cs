using LinqToDB.Mapping;

namespace Ekom.Models;

/// <summary>Durable stock-completion receipt; retained for the lifetime of the order.</summary>
[Table(Name = "EkomCheckoutStockCompletion")]
internal sealed class CheckoutStockCompletionData
{
    [PrimaryKey, NotNull]
    public Guid OrderId { get; set; }
    [Column, NotNull]
    public DateTime CompletedUtc { get; set; }
}
