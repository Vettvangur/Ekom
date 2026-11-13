using Ekom.Models.Orders;
using LinqToDB.Mapping;
using System.Text.Json;

namespace Ekom.Models.Payments;

/// <summary>
/// One payment attempt at a payment provider, always belonging to an Ekom order.
/// </summary>
[Table(Name = "EkomOrderPayments")]
public sealed class OrderPayment
{
    /// <summary>Numeric sequence. Some gateways require a number instead of a Guid.</summary>
    [PrimaryKey, Identity, NotNull]
    public int Sequence { get; set; }

    /// <summary>Identifies this payment attempt. Sent to gateways as the payment reference.</summary>
    [Column, NotNull]
    public Guid PaymentId { get; set; }

    /// <summary>The Ekom order (<see cref="OrderData.UniqueId"/>).</summary>
    [Column, NotNull]
    public Guid OrderUniqueId { get; set; }

    [Column(Length = 100)]
    public string? OrderNumber { get; set; }

    /// <summary>The Ekom checkout attempt this payment was started from.</summary>
    [Column]
    public Guid? AttemptId { get; set; }

    [Column(Length = 100)]
    public string? StoreAlias { get; set; }

    [Column]
    public Guid? ProviderKey { get; set; }

    [Column(Length = 200)]
    public string? ProviderName { get; set; }

    [Column(Precision = 18, Scale = 2), NotNull]
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code.</summary>
    [Column(Length = 10)]
    public string? Currency { get; set; }

    /// <summary>The gateway's own transaction reference, once known.</summary>
    [Column(Length = 200)]
    public string? TransactionId { get; set; }

    /// <summary>A provider-specific lookup key, for gateways that do not echo <see cref="PaymentId"/>.</summary>
    [Column(Length = 200)]
    public string? ProviderReference { get; set; }

    /// <summary>Payment settings JSON. Never contains card data.</summary>
    [Column(Length = int.MaxValue)]
    public string? Settings { get; set; }

    /// <summary>Provider configuration JSON used for this payment.</summary>
    [Column(Length = int.MaxValue)]
    public string? ProviderData { get; set; }

    [Column]
    public Guid? Member { get; set; }

    [Column(Length = 45)]
    public string? IPAddress { get; set; }

    [Column(Length = 4000)]
    public string? UserAgent { get; set; }

    [Column, NotNull]
    public DateTime CreatedUtc { get; set; }
}

/// <summary>What happened to a payment.</summary>
public enum PaymentTransactionType
{
    Requested = 0,
    Authorized = 1,
    Captured = 2,
    Failed = 3,
    Cancelled = 4,
    Voided = 5,
    Refunded = 6,

    /// <summary>A payment status set by a manager user. Never moves money.</summary>
    Manual = 7,
}

/// <summary>
/// One event on an order's payment. Rows are append-only: the order's payment status is calculated from them.
/// </summary>
[Table(Name = "EkomOrderPaymentTransactions")]
public sealed class OrderPaymentTransaction
{
    [PrimaryKey, Identity, NotNull]
    public int Id { get; set; }

    /// <summary>The payment this belongs to. Null for manual changes.</summary>
    [Column]
    public Guid? PaymentId { get; set; }

    [Column, NotNull]
    public Guid OrderUniqueId { get; set; }

    /// <summary>Database representation of <see cref="Type"/>.</summary>
    [Column(Length = 32), NotNull]
    public string TypeCol { get; set; } = nameof(PaymentTransactionType.Requested);

    [Column(IsColumn = false)]
    public PaymentTransactionType Type
    {
        get => Enum.TryParse<PaymentTransactionType>(TypeCol, out var type) ? type : PaymentTransactionType.Requested;
        set => TypeCol = value.ToString();
    }

    /// <summary>For <see cref="PaymentTransactionType.Manual"/> rows, the payment status the user chose.</summary>
    [Column(Length = 32)]
    public string? ManualStatusCol { get; set; }

    [Column(IsColumn = false)]
    public PaymentStatus? ManualStatus
        => Enum.TryParse<PaymentStatus>(ManualStatusCol, out var status) ? status : null;

    [Column(Precision = 18, Scale = 2), NotNull]
    public decimal Amount { get; set; }

    [Column(Length = 10)]
    public string? Currency { get; set; }

    [Column(Length = 200)]
    public string? TransactionId { get; set; }

    [Column(Length = 100)]
    public string? AuthorizationCode { get; set; }

    [Column(Length = 30)]
    public string? MaskedCardNumber { get; set; }

    [Column(Length = 50)]
    public string? CardBrand { get; set; }

    [Column(Length = 100)]
    public string? PaymentMethod { get; set; }

    [Column(Length = 2000)]
    public string? Message { get; set; }

    /// <summary>The provider name, "Customer", or the manager user name.</summary>
    [Column(Length = 200)]
    public string? Actor { get; set; }

    /// <summary>When the provider says it happened.</summary>
    [Column, NotNull]
    public DateTime OccurredUtc { get; set; }

    /// <summary>When Ekom stored it.</summary>
    [Column, NotNull]
    public DateTime ReceivedUtc { get; set; }

    /// <summary>Repeated provider callbacks with the same key are stored once.</summary>
    [Column(Length = 300), NotNull]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>JSON list of <see cref="PaymentDetail"/>: provider-specific fields shown to staff.</summary>
    [Column(Length = int.MaxValue)]
    public string? Details { get; set; }

    /// <summary>The provider's response as received, with secrets and full card numbers removed.</summary>
    [Column(Length = int.MaxValue)]
    public string? RawData { get; set; }

    /// <summary>Reads <see cref="RawData"/> as a provider response type.</summary>
    public T? GetData<T>() where T : class
        => string.IsNullOrWhiteSpace(RawData) ? null : JsonSerializer.Deserialize<T>(RawData);

    public IReadOnlyList<PaymentDetail> GetDetails()
        => string.IsNullOrWhiteSpace(Details)
            ? Array.Empty<PaymentDetail>()
            : JsonSerializer.Deserialize<List<PaymentDetail>>(Details) ?? (IReadOnlyList<PaymentDetail>)Array.Empty<PaymentDetail>();
}

/// <summary>A provider-specific field a provider chooses to show on the order's payment.</summary>
public sealed record PaymentDetail(string Key, string Label, string? Value, bool Copyable = false);
