using Ekom.Models;

namespace Ekom.Services;

/// <summary>Integration-owned, idempotent external giftcard holds for a payment attempt.</summary>
/// <remarks>
/// Implementations must key holds by order and attempt, return renewed claim metadata
/// from ReserveAsync, and confirm all supplied claims are released before ReleaseAsync returns.
/// ReleaseAsync may receive a single-card subset for a Manager release: never release
/// other selections belonging to the same order or attempt. Guid.Empty identifies legacy
/// holds without a tracked attempt; verify the supplied claim's ownership with the provider.
/// Release must tolerate repeated calls and partially successful previous calls,
/// and throw on unsuccessful provider responses even when the provider does not.
/// Active holds may have ClaimId with Claimed=false. Claimed=true, TransactionId,
/// ClaimDate, or UsedDate indicates redemption/settlement and must not be released.
/// Never release another attempt's claims. Exceptions leave checkout fail-closed,
/// except for <see cref="Ekom.Exceptions.GiftcardReservationRejectedException"/> from
/// ReserveAsync. Opt into that contract only after confirming the entire call created
/// no new external holds and changed no existing holds, including partially processed
/// selections. Existing persisted claim metadata must remain valid and unchanged.
/// Provider rejection text (including insufficient balance), timeouts, and uncertain
/// or partially successful outcomes are not proof of no effects. Confirmed rejection
/// closes the new attempt without releasing prior holds or discarding claim metadata.
/// Codes and amounts remain selected when a claim is released. ValidUntil may mean
/// card validity or provider hold expiry: preserve it on release, and return the
/// renewed provider expiry from ReserveAsync when appropriate. This adapter must
/// be supplied by the consumer; Ekom does not assume external release succeeded.
/// </remarks>
public interface ICheckoutGiftcardReservations
{
    Task<IReadOnlyList<Giftcard>> ReserveAsync(Guid orderId, Guid attemptId,
        IReadOnlyList<Giftcard> selections, CancellationToken ct = default);
    Task ReleaseAsync(Guid orderId, Guid attemptId,
        IReadOnlyList<Giftcard> selections, CancellationToken ct = default);
}
