using Ekom.Models;

namespace Ekom.Services;

/// <summary>Integration-owned, idempotent external giftcard holds for a payment attempt.</summary>
/// <remarks>
/// Implementations must key holds by order and attempt, return renewed claim metadata
/// from ReserveAsync, and confirm all claims are released before ReleaseAsync returns.
/// Release must tolerate repeated calls and partially successful previous calls,
/// and throw on unsuccessful provider responses even when the provider does not.
/// Active holds may have ClaimId with Claimed=false. Claimed=true, TransactionId,
/// ClaimDate, or UsedDate indicates redemption/settlement and must not be released.
/// Never release another attempt's claims. Exceptions leave checkout fail-closed.
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
