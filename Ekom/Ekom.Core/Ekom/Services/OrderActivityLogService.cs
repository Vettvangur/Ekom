using Ekom.Models;
using Ekom.Repositories;
using Microsoft.Extensions.Logging;

namespace Ekom.Services;

public sealed class OrderActivityLogService : IOrderActivityLogService
{
    private readonly ActivityLogRepository _activityLogRepository;
    private readonly IOrderActivityLogDispatcher _dispatcher;
    private readonly ILogger<OrderActivityLogService> _logger;

    public OrderActivityLogService(
        ActivityLogRepository activityLogRepository,
        IOrderActivityLogDispatcher dispatcher,
        ILogger<OrderActivityLogService> logger)
    {
        _activityLogRepository = activityLogRepository;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task AddOrderLogAsync(Guid orderId, string message, string? userName = null, OrderActivityLogType logType = OrderActivityLogType.Info, CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id cannot be empty.", nameof(orderId));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(message));
        }

        string normalizedUserName = string.IsNullOrWhiteSpace(userName)
            ? "Customer"
            : userName.Trim();

        await _dispatcher.EnqueueAsync(
                new OrderActivityLogWrite(
                    orderId,
                    message,
                    normalizedUserName,
                    DateTime.Now,
                    logType),
                CancellationToken.None)
            .ConfigureAwait(false);

        _logger.LogDebug("Queued activity log for order {OrderId}", orderId);
    }

    public async Task<IReadOnlyList<OrderActivityLogEntry>> GetOrderLogsAsync(Guid orderId, CancellationToken ct = default)
    {
        if (orderId == Guid.Empty)
        {
            return Array.Empty<OrderActivityLogEntry>();
        }

        List<OrderActivityLog> logs = await _activityLogRepository.GetLogsAsync(orderId)
            .ConfigureAwait(false);

        return logs
            .Select(x => new OrderActivityLogEntry
            {
                Message = x.Log,
                UserName = x.UserName,
                Date = x.Date,
                LogType = x.LogType,
            })
            .ToList();
    }
}
