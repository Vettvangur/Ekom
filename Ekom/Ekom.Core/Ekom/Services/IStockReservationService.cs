using Ekom.Models;

namespace Ekom.Services;

public interface IStockReservationService
{
    Task<StockReservationResult> ReserveAsync(StockReservationRequest request, CancellationToken ct = default);
    Task<StockReservationResult> ConsumeAsync(string reservationId, CancellationToken ct = default);
    Task<StockReservationResult> ReleaseAsync(string reservationId, CancellationToken ct = default);
    Task<StockReservationResult> ExpireAsync(string reservationId, CancellationToken ct = default);
    Task<int> ExpireDueAsync(int batchSize, CancellationToken ct = default);
    Task<int> CleanupAsync(DateTime completedBeforeUtc, int batchSize, CancellationToken ct = default);
}
