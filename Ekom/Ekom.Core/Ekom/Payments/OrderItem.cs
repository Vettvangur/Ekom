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
    /// 
    /// </summary>
    public decimal VAT { get; set; }
    
    /// <summary>
    /// Discount in percentages for <see cref="OrderItem"/>
    /// </summary>
    public decimal Discount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public decimal Quantity { get; set; }
}
