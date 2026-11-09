namespace Ekom.Models;

/// <summary>A single store/currency price edit, leaving other prices unchanged.</summary>
public sealed class TablePriceRequest
{
    public string StoreAlias { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal? Price { get; set; }
}
