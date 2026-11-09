namespace Ekom.Models.Manager;

public class MostSoldProduct
{
    public string SKU { get; set; }
    public string Title { get; set; }
    public string? VariantTitle { get; set; }
    public Guid Key { get; set; }
    public int Id { get; set; }
    public decimal ProductCount { get; set; }
    public decimal Total { get; set; }
}
