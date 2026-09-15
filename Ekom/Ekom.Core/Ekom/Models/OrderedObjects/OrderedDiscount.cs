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
    public OrderedDiscount(
        Guid key,
        string title,
        bool stackable,
        decimal amount,
        DiscountType type,
        List<string> discountItems,
        List<string> excludeDiscountItems,
        Constraints? constraints,
        bool hasMasterStock,
        bool globalDiscount)
        : this(
            key,
            title,
            stackable,
            amount,
            type,
            discountItems,
            excludeDiscountItems,
            constraints,
            hasMasterStock,
            globalDiscount,
            OrderDiscountQuantityMode.None,
            null,
            0,
            0)
    {
    }

    [JsonConstructor]
    public OrderedDiscount(
        Guid key,
        string title,
        bool stackable,
        decimal amount,
        DiscountType type,
        List<string> discountItems,
        List<string> excludeDiscountItems,
        Constraints? constraints,
        bool hasMasterStock,
        bool globalDiscount,
        OrderDiscountQuantityMode quantityDiscountMode,
        List<string>? qualifyingItems,
        int requiredQuantity,
        int rewardQuantity)
    {
        Key = key;
        Stackable = stackable;
        DiscountItems = discountItems;
        ExcludeDiscountItems = excludeDiscountItems;
        Amount = amount;
        Title = title;
        Type = type;
        Constraints = constraints ?? new Constraints();
        HasMasterStock = hasMasterStock;
        GlobalDiscount = globalDiscount;
        QuantityDiscountMode = quantityDiscountMode;
        QualifyingItems = qualifyingItems ?? [];
        RequiredQuantity = requiredQuantity;
        RewardQuantity = rewardQuantity;
    }

    /// <summary>
    /// ctor
    /// </summary>
    public OrderedDiscount(IDiscount discount)
    {
        discount = discount ?? throw new ArgumentNullException(nameof(discount));
        Stackable = discount.Stackable;
        Key = discount.Key;
        Title = discount.Title;
        DiscountItems = discount.DiscountItems;
        ExcludeDiscountItems = discount.ExcludeDiscountItems;
        Amount = discount.Amount;
        Type = discount.Type;
        Constraints = discount.Constraints is { } constraints
            ? new Constraints(constraints)
            : new Constraints();
        HasMasterStock = discount.HasMasterStock;
        GlobalDiscount = discount.GlobalDiscount;
        QuantityDiscountMode = discount.QuantityDiscountMode;
        QualifyingItems = discount.QualifyingItems;
        RequiredQuantity = discount.RequiredQuantity;
        RewardQuantity = discount.RewardQuantity;
    }

    /// <summary>
    /// 
    /// </summary>
    public Guid Key { get; internal set; }

    public DiscountType Type { get; internal set; }

    public decimal Amount { get; internal set; }

    public IReadOnlyCollection<string> DiscountItems { get; }

    public IReadOnlyCollection<string> ExcludeDiscountItems { get; }

    public IReadOnlyCollection<string> QualifyingItems { get; }

    public OrderDiscountQuantityMode QuantityDiscountMode { get; }

    public int RequiredQuantity { get; }

    public int RewardQuantity { get; }
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

    public string Title { get; set; }

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
