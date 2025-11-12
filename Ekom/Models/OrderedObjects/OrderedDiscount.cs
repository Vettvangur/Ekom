using Newtonsoft.Json;

namespace Ekom.Models;

/// <summary>
/// Frozen <see cref="Discount"/> with coupons and <see cref="DiscountAmount"/>
/// </summary>
public class OrderedDiscount : IComparable<IDiscount>, IDiscount
{
    /// <summary>
    /// 
    /// </summary>
    [JsonConstructor]
    public OrderedDiscount(
        Guid key,
        bool stackable,
        decimal amount,
        DiscountType type,
        List<string> discountItems,
        List<string> excludeDiscountItems,
        Constraints constraints,
        bool hasMasterStock,
        bool globalDiscount)
    {
        Key = key;
        Stackable = stackable;
        DiscountItems = discountItems;
        ExcludeDiscountItems = excludeDiscountItems;
        Amount = amount;
        Type = type;
        Constraints = constraints;
        HasMasterStock = hasMasterStock;
        GlobalDiscount = globalDiscount;
    }

    /// <summary>
    /// ctor
    /// </summary>
    public OrderedDiscount(IDiscount discount)
    {
        discount = discount ?? throw new ArgumentNullException(nameof(discount));
        Stackable = discount.Stackable;
        Key = discount.Key;
        DiscountItems = discount.DiscountItems;
        ExcludeDiscountItems = discount.ExcludeDiscountItems;
        Amount = discount.Amount;
        Type = discount.Type;
        Constraints = new Constraints(discount.Constraints);
        HasMasterStock = discount.HasMasterStock;
        GlobalDiscount = discount.GlobalDiscount;
    }

    /// <summary>
    /// 
    /// </summary>
    public Guid Key { get; internal set; }

    public DiscountType Type { get; internal set; }

    public decimal Amount { get; internal set; }

    public IReadOnlyCollection<string> DiscountItems { get; }

    public IReadOnlyCollection<string> ExcludeDiscountItems { get; }
    /// <summary>
    /// Ranges
    /// </summary>
    public IConstraints Constraints { get; internal set; }
    /// <summary>
    /// If discount is stackable with productDiscounts
    /// </summary>
    public bool Stackable { get; }

    /// <summary>
    /// Coupon code activations left
    /// </summary>
    public bool HasMasterStock { get; internal set; }

    public bool GlobalDiscount { get; set; }

    /// <summary>
    /// <see cref="IComparable{T}"/> implementation
    /// </summary>
    public int CompareTo(IDiscount other)
    {
        if (other == null)
            return 1;

        else if (Type != other.Type)
            throw new FormatException("Discounts are not equal, please compare type before comparing value.");
        else if (Amount == other.Amount)
            return 0;
        else if (Amount > other.Amount)
            return 1;
        else
            return -1;
    }
}
