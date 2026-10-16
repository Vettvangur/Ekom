namespace Ekom.Models.Manager;

public sealed class GiftcardRecoveryView
{
    public Guid AttemptId { get; init; }
    public DateTime CreatedUtc { get; init; }
    public string State { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool HasUnknownEffects { get; init; }
    public bool CanReconcile { get; init; }
    public string? ReconcileToken { get; init; }
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<GiftcardRecoveryClaimView> Claims { get; init; } = Array.Empty<GiftcardRecoveryClaimView>();
}

public sealed class GiftcardRecoveryClaimView
{
    public string Id { get; init; } = string.Empty;
    public string CardReference { get; init; } = string.Empty;
    public string ClaimReference { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Account { get; init; } = string.Empty;
    public decimal RequestedAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? RawReturnedAmount { get; init; }
    public string? ReturnedAmountType { get; init; }
    public string? RawExpiry { get; init; }
    public string CleanupState { get; init; } = string.Empty;
    public bool CanRelease { get; init; }
    public string? ReleaseToken { get; init; }
    public string? UnavailableReason { get; init; }
}

public sealed class GiftcardRecoveryRequest
{
    public Guid AttemptId { get; init; }
    public string? ClaimId { get; init; }
    public string Token { get; init; } = string.Empty;
}

public sealed record GiftcardRecoveryResponse(string Message, bool Resolved);
