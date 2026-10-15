using Ekom.Models.Manager;

namespace Ekom.Services;

public interface IOrderManagerLockService
{
    Task<OrderLockReleaseResponse> ReleaseOrderLockAsync(Guid orderId, string releaseToken,
        IReadOnlyCollection<string> allowedStoreAliases, string? userName, CancellationToken ct = default);
}
