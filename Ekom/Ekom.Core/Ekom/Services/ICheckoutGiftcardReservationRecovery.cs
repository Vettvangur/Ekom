using Ekom.Models;

namespace Ekom.Services;

/// <summary>Optional capability implemented by the registered gift-card reservation adapter.</summary>
/// <remarks>
/// Recovery receipts are never accepted payment contributions. Verify provider/account/order/attempt
/// ownership before releasing only the supplied newly acquired holds. Return Released only after
/// authoritative confirmation, including safe repeats. Throw or return an unresolved status otherwise.
/// Reconciliation must be read-only and may set AllEffectsAccountedFor only when the original request
/// is terminal and all effects are known; a missing claim, local expiry, or timeout is not proof.
/// Never release, redeem, or refund other claims or payments during reconciliation.
/// Enriching incomplete v1 diagnostics with provider/account or NewlyAcquired requires
/// authoritative ownership proof for the original order and attempt/request at the provider.
/// A card lookup or matching card code, claim reference, or amount alone is not proof.
/// </remarks>
public interface ICheckoutGiftcardReservationRecovery
{
    Task<IReadOnlyList<GiftcardRecoveryClaim>> ReleaseRecoveryClaimsAsync(Guid orderId, Guid attemptId,
        IReadOnlyList<GiftcardRecoveryClaim> claims, CancellationToken ct = default);

    Task<GiftcardReservationFailure> ReconcileReservationFailureAsync(Guid orderId, Guid attemptId,
        GiftcardReservationFailure evidence, CancellationToken ct = default);
}
