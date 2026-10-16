using Ekom.Models.Manager;

namespace Ekom.Services;

public interface IOrderManagerGiftcardRecoveryService
{
    Task<IReadOnlyList<GiftcardRecoveryView>> GetGiftcardRecoveryAsync(Guid orderId,
        IReadOnlyCollection<string> allowedStoreAliases, CancellationToken ct = default);

    Task<GiftcardRecoveryResponse> RecoverGiftcardAsync(Guid orderId, GiftcardRecoveryRequest request,
        bool reconcile, IReadOnlyCollection<string> allowedStoreAliases, string? userName,
        CancellationToken ct = default);
}
