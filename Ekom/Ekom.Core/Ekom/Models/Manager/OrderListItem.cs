using LinqToDB.Mapping;

namespace Ekom.Models.Manager;

/// <summary>Lightweight manager search result with its computed review indicator.</summary>
public sealed class OrderListItem : OrderData
{
    [Column]
    public bool NeedsReview { get; set; }
}
