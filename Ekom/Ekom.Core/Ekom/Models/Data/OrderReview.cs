using LinqToDB.Mapping;

namespace Ekom.Models;

/// <summary>A persistent issue requiring explicit review, independent of order status.</summary>
[Table(Name = "EkomOrderReviews")]
public sealed class OrderReview
{
    [PrimaryKey, NotNull]
    public Guid Id { get; set; }
    [Column, NotNull]
    public Guid OrderId { get; set; }
    [Column(Length = 100), NotNull]
    public string Source { get; set; } = string.Empty;
    [Column(Length = 100), NotNull]
    public string Code { get; set; } = string.Empty;
    [Column(Length = 200), NotNull]
    public string Title { get; set; } = string.Empty;
    [Column(Length = 4000), NotNull]
    public string Message { get; set; } = string.Empty;
    [Column, NotNull]
    public DateTime CreatedUtc { get; set; }
    [Column]
    public DateTime? ResolvedUtc { get; set; }
    [Column(Length = 200)]
    public string? ResolvedBy { get; set; }
    [Column(Length = 4000)]
    public string? ResolutionComment { get; set; }
}
