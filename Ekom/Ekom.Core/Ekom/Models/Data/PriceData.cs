using LinqToDB.Mapping;

namespace Ekom.Models;

/// <summary>A price override independent of Umbraco content versions.</summary>
[Table(Name = "EkomPrice")]
public sealed class PriceData
{
    [PrimaryKey(1), NotNull]
    public Guid NodeKey { get; set; }

    [PrimaryKey(2), Column(Length = 100), NotNull]
    public string PropertyAlias { get; set; } = string.Empty;

    [PrimaryKey(3), Column(Length = 100), NotNull]
    public string StoreAlias { get; set; } = string.Empty;

    [PrimaryKey(4), Column(Length = 50), NotNull]
    public string Currency { get; set; } = string.Empty;

    [Column(DbType = "decimal(18,4)"), NotNull]
    public decimal Price { get; set; }

    [Column, NotNull]
    public DateTime UpdateDate { get; set; }
}
