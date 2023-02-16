namespace Ekom.Payments;

/// <summary>
/// A single item in an <see cref="OrderStatus"/>
/// </summary>
public class OrderItem
{
    /// <summary>
    /// 
    /// </summary>
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Discount in percentages for <see cref="OrderItem"/>
    /// </summary>
    public int Discount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public int Quantity { get; set; }
}
