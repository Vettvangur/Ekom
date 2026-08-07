namespace Ekom.Models.Manager;

public sealed class OrderLineAddRequest
{
    public Guid ProductId { get; init; }
    public Guid? VariantId { get; init; }
    public decimal Quantity { get; init; }
}
