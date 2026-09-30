namespace Ekom.Models;

public sealed class ShippingValidationError
{
    public string Code => "invalidShippingProvider";
    public Guid ProviderKey { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public decimal CurrentAmount { get; init; }
    public decimal? MinimumAmount { get; init; }
    public decimal? MaximumAmount { get; init; }
}
