namespace Ekom.Models.Manager;

public sealed class OrderShippingProviderUpdateRequest
{
    public Guid ProviderId { get; init; }
    public Dictionary<string, string>? CustomData { get; init; }
}
