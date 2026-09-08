using LinqToDB.Mapping;

namespace Ekom.Models;

/// <summary>
/// Persisted warehouse stock balance.
/// </summary>
[Table(Name = "EkomWarehouseStock")]
public sealed class WarehouseStockData
{
    /// <summary>
    /// Store alias that owns the balance.
    /// </summary>
    [PrimaryKey(1), NotNull, Column(Length = 255)]
    public string StoreAlias { get; internal set; } = string.Empty;

    /// <summary>
    /// Warehouse identifier.
    /// </summary>
    [PrimaryKey(2), NotNull]
    public Guid WarehouseKey { get; internal set; }

    /// <summary>
    /// Normalized SKU.
    /// </summary>
    [PrimaryKey(3), NotNull, Column(Length = 255)]
    public string Sku { get; internal set; } = string.Empty;

    /// <summary>
    /// Warehouse balance.
    /// </summary>
    [Column(DbType = "decimal(18,2)"), NotNull]
    public decimal Balance { get; internal set; }

    /// <summary>
    /// When the record was created.
    /// </summary>
    [Column, NotNull]
    public DateTime CreateDate { get; internal set; }

    /// <summary>
    /// When the balance was last set.
    /// </summary>
    [Column, NotNull]
    public DateTime UpdateDate { get; internal set; }
}
