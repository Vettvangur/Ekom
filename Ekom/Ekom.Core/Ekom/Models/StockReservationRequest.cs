namespace Ekom.Models;

/// <summary>A single product, variant, discount or coupon stock reservation.</summary>
public sealed record StockReservationRequest
{
    public Guid Key { get; init; }
    public decimal Quantity { get; init; }
    /// <summary>Stock that must remain available after reservation (for example a stock buffer).</summary>
    public decimal MinimumRemainingStock { get; init; }
    public bool IsDiscount { get; init; }
    public string? StoreAlias { get; init; }
    public string? Coupon { get; init; }
    public TimeSpan Duration { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? OrderId { get; init; }
    public string? PaymentAttemptId { get; init; }
}

public enum StockReservationStatus
{
    Created, AlreadyExists, Conflict, InsufficientStock,
    Consumed, Released, Expired, AlreadyConsumed, AlreadyReleased, AlreadyExpired,
    NotFound, LateConsumption, NotDue
}

public enum StockReservationState { Active, Consumed, Released, Expired }

/// <summary>Terminal outcomes are explicit; late payment must be reconciled by the caller.</summary>
public sealed record StockReservationResult(
    string? ReservationId,
    StockReservationStatus Status,
    StockReservationState? State = null,
    DateTime? ExpiresUtc = null);
