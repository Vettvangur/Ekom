namespace Ekom.Models;

/// <summary>External effects of a failed reservation invocation, not payment contributions.</summary>
public sealed record GiftcardReservationFailure
{
    public Guid OrderId { get; init; }
    public Guid AttemptId { get; init; }
    public GiftcardReservationFailureOutcome Outcome { get; init; }
    /// <summary>True only when the entire invocation is terminal and every external effect is known.</summary>
    public bool AllEffectsAccountedFor { get; init; }
    public IReadOnlyList<GiftcardRecoveryClaim> Claims { get; init; } = Array.Empty<GiftcardRecoveryClaim>();
}

public enum GiftcardReservationFailureOutcome
{
    Unknown,
    NoEffects,
    Compensated,
    Unresolved,
}

/// <summary>Provider evidence preserved even when amount/date validation failed.</summary>
public sealed record GiftcardRecoveryClaim
{
    public string Provider { get; init; } = string.Empty;
    /// <summary>Non-secret merchant/site identity; never an API key or authentication token.</summary>
    public string Account { get; init; } = string.Empty;
    public string CardCode { get; init; } = string.Empty;
    public string ClaimId { get; init; } = string.Empty;
    public string? ProviderRequestId { get; init; }
    public decimal RequestedAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? RawReturnedAmount { get; init; }
    public string? ReturnedAmountType { get; init; }
    public string? RawExpiry { get; init; }
    public DateTimeOffset? ExpiresUtc { get; init; }
    /// <summary>True only for a new hold belonging to this invocation; never infer this from code/amount.</summary>
    public bool NewlyAcquired { get; init; }
    public GiftcardRecoveryClaimStatus Status { get; init; }
}

public enum GiftcardRecoveryClaimStatus
{
    Unknown,
    Active,
    ReleaseUnconfirmed,
    Released,
    Settled,
}
