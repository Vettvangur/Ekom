using Ekom.Models.Orders;
using Ekom.Models.Payments;

namespace Ekom.Services;

/// <summary>Starts a payment attempt for an order.</summary>
public sealed record NewOrderPayment
{
    public required Guid OrderUniqueId { get; init; }
    public string? OrderNumber { get; init; }

    /// <summary>Leave empty to have one generated.</summary>
    public Guid PaymentId { get; init; }
    public Guid? AttemptId { get; init; }
    public string? StoreAlias { get; init; }
    public Guid? ProviderKey { get; init; }
    public string? ProviderName { get; init; }

    /// <summary>The base payment provider handling the payment, e.g. "borgun".</summary>
    public string? ProviderType { get; init; }
    public decimal Amount { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public string? Currency { get; init; }
    public string? ProviderReference { get; init; }
    public string? Settings { get; init; }
    public string? ProviderData { get; init; }
    public Guid? Member { get; init; }
    public string? IPAddress { get; init; }
    public string? UserAgent { get; init; }

    /// <summary>"Customer" when empty.</summary>
    public string? Actor { get; init; }

    /// <summary>When the payment was requested. Defaults to now; set when copying a payment that already exists elsewhere.</summary>
    public DateTime? CreatedUtc { get; init; }
}

/// <summary>Something that happened to a payment, reported by a provider.</summary>
public sealed record PaymentTransaction
{
    public required Guid PaymentId { get; init; }
    public required PaymentTransactionType Type { get; init; }

    /// <summary>Defaults to the payment's amount.</summary>
    public decimal? Amount { get; init; }
    public string? TransactionId { get; init; }
    public string? AuthorizationCode { get; init; }
    public string? MaskedCardNumber { get; init; }
    public string? CardBrand { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Message { get; init; }

    /// <summary>The provider name when empty.</summary>
    public string? Actor { get; init; }

    /// <summary>When the provider says it happened. Defaults to now.</summary>
    public DateTime? OccurredUtc { get; init; }

    /// <summary>Defaults to payment id + type + transaction id, so a repeated callback is stored once.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Provider-specific fields to show to staff.</summary>
    public IReadOnlyList<PaymentDetail>? Details { get; init; }

    /// <summary>The provider response as JSON, without secrets or full card numbers.</summary>
    public string? RawData { get; init; }

    /// <summary>Set by checkout paths that complete the order themselves, such as offline payments.</summary>
    internal bool SkipCheckoutCompletion { get; init; }
}

/// <param name="Recorded">False when the same transaction was already stored.</param>
/// <param name="PaymentStatus">The order's payment status after recording.</param>
/// <param name="Transaction">The stored transaction.</param>
/// <param name="CheckoutCompleted">True when this call completed the order's checkout.</param>
public sealed record PaymentRecordResult(bool Recorded, PaymentStatus? PaymentStatus, OrderPaymentTransaction? Transaction,
    bool CheckoutCompleted = false);

public sealed record OrderPaymentEvent(string Type, decimal Amount, string? Currency, DateTime OccurredUtc,
    string? Message, string? Actor, string? TransactionId, string? ManualStatus);

public sealed record OrderPaymentAttempt
{
    public Guid? PaymentId { get; init; }
    public Guid? AttemptId { get; init; }
    public string? Provider { get; init; }
    public required string Status { get; init; }
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
    public string? TransactionId { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime? AuthorizedUtc { get; init; }
    public DateTime? CapturedUtc { get; init; }
    public string? AuthorizationCode { get; init; }
    public string? MaskedCardNumber { get; init; }
    public string? CardBrand { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<PaymentDetail> Details { get; init; } = Array.Empty<PaymentDetail>();
    public IReadOnlyList<OrderPaymentEvent> Events { get; init; } = Array.Empty<OrderPaymentEvent>();
}

/// <summary>An order's payments, as shown in the manager.</summary>
/// <param name="Current">The paid or authorized payment, otherwise the latest attempt.</param>
/// <param name="Attempts">Every payment attempt, newest first.</param>
/// <param name="ManualChanges">Payment status changes made by manager users, newest first.</param>
public sealed record OrderPaymentsView(OrderPaymentAttempt? Current, IReadOnlyList<OrderPaymentAttempt> Attempts,
    IReadOnlyList<OrderPaymentEvent> ManualChanges);

/// <summary>
/// The payments of Ekom orders. Payment providers create a payment and record what happens to it;
/// Ekom derives the order's payment status, activity log and manager view from those records.
/// </summary>
public interface IOrderPayments
{
    /// <summary>Stores a new payment attempt and its Requested transaction.</summary>
    Task<OrderPayment> CreateAsync(NewOrderPayment payment, CancellationToken ct = default);

    /// <summary>Stores a transaction once and recalculates the order's payment status.</summary>
    Task<PaymentRecordResult> RecordAsync(PaymentTransaction transaction, CancellationToken ct = default);

    Task<OrderPayment?> GetAsync(Guid paymentId, CancellationToken ct = default);

    /// <summary>
    /// Finds the latest payment by the provider's own lookup key, for gateways that do not echo the payment id.
    /// </summary>
    /// <param name="providerType">The base payment provider, e.g. "siminnPay".</param>
    /// <param name="providerReference">The key the provider stored when it created the payment.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OrderPayment?> GetByProviderReferenceAsync(string providerType, string providerReference, CancellationToken ct = default);

    /// <summary>
    /// The payments of a provider that were requested and have had nothing recorded since. A provider that
    /// polls its gateway uses this to pick its unfinished payments up again after a restart.
    /// </summary>
    /// <param name="providerType">The base payment provider, e.g. "teyaConsumerloans".</param>
    /// <param name="createdAfterUtc">Only payments requested after this time.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<OrderPayment>> GetOpenPaymentsAsync(string providerType, DateTime createdAfterUtc, CancellationToken ct = default);

    Task<OrderPaymentsView> GetForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Finds the order of a payment id or gateway transaction id.</summary>
    Task<Guid?> FindOrderAsync(string paymentIdOrTransactionId, CancellationToken ct = default);
}
