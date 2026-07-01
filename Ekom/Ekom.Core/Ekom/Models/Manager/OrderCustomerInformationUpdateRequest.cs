namespace Ekom.Models.Manager;

public sealed class OrderCustomerInformationUpdateRequest
{
    public Guid OrderId { get; init; }

    public Dictionary<string, string?> Customer { get; init; } = [];

    public Dictionary<string, string?> Shipping { get; init; } = [];
}
