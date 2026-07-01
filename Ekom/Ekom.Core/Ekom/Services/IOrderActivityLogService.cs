using Ekom.Models;

namespace Ekom.Services;

public interface IOrderActivityLogService
{
    Task AddOrderLogAsync(Guid orderId, string message, string? userName = null, OrderActivityLogType logType = OrderActivityLogType.Info, CancellationToken ct = default);

    Task<IReadOnlyList<OrderActivityLogEntry>> GetOrderLogsAsync(Guid orderId, CancellationToken ct = default);
}
