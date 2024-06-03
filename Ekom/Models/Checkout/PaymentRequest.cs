namespace Ekom.Models;

/// <summary>
/// </summary>
public class PaymentRequest
{
    public Guid PaymentProvider { get; set; }

    public Guid ShippingProvider { get; set; }
    public string CardNumber { get; set; } = "";
    public string CVV { get; set; } = "";
    public int Year { get; set; }
    public int Month { get; set; }
    public string ReturnUrl { get; set; } = "";
    public string? StoreAlias { get; set; }
}
