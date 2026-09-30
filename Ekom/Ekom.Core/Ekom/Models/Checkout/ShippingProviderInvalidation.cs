namespace Ekom.Models;

public sealed class ShippingProviderInvalidation
{
    public Guid ProviderKey { get; init; }
    public string ProviderName { get; init; } = string.Empty;
}
