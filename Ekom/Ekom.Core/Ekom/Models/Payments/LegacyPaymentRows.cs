using LinqToDB.Mapping;

namespace Ekom.Models.Payments;

/// <summary>
/// A payment row of Ekom.Payments 1.0 and earlier (table EkomPaymentOrders). Only read, to copy old
/// payments onto their orders.
/// </summary>
[Table(Name = "EkomPaymentOrders")]
internal sealed class LegacyPaymentOrderRow
{
    [Column, NotNull]
    public Guid UniqueId { get; set; }

    [PrimaryKey, NotNull]
    public long ReferenceId { get; set; }

    [Column]
    public Guid? Member { get; set; }

    [Column, NotNull]
    public decimal Amount { get; set; }

    /// <summary>Local server time.</summary>
    [Column, NotNull]
    public DateTime Date { get; set; }

    [Column]
    public string? IPAddress { get; set; }

    [Column]
    public string? UserAgent { get; set; }

    [Column, NotNull]
    public bool Paid { get; set; }

    [Column]
    public string? EkomPaymentSettingsData { get; set; }

    [Column]
    public string? EkomPaymentProviderData { get; set; }

    /// <summary>The Ekom order id for most providers; a provider's own lookup key for others.</summary>
    [Column]
    public string? CustomData { get; set; }
}

/// <summary>What a provider stored about a paid payment in Ekom.Payments 1.0 and earlier (table EkomPayments).</summary>
[Table(Name = "EkomPayments")]
internal sealed class LegacyPaymentDataRow
{
    [PrimaryKey, NotNull]
    public Guid Id { get; set; }

    [Column]
    public string? CardNumber { get; set; }

    [Column]
    public string? PaymentMethod { get; set; }

    /// <summary>The provider response as JSON, or an authorization code.</summary>
    [Column]
    public string? CustomData { get; set; }

    [Column]
    public DateTime Date { get; set; }
}

/// <summary>How far the copy of old payments has come. One row.</summary>
[Table(Name = "EkomOrderPaymentMigration")]
internal sealed class OrderPaymentMigrationState
{
    internal const string LegacyPayments = "legacy-payments";

    [PrimaryKey, NotNull, Column(Length = 50)]
    public string Name { get; set; } = LegacyPayments;

    /// <summary>The last legacy row that was looked at.</summary>
    [Column, NotNull]
    public long LastReferenceId { get; set; }

    [Column, NotNull]
    public int Migrated { get; set; }

    /// <summary>Rows whose payment was already on its order.</summary>
    [Column, NotNull]
    public int AlreadyPresent { get; set; }

    /// <summary>Rows whose order could not be found. They stay in the old table.</summary>
    [Column, NotNull]
    public int Unresolved { get; set; }

    [Column, NotNull]
    public DateTime UpdatedUtc { get; set; }
}
