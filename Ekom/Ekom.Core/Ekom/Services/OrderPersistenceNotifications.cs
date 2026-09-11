using Microsoft.Extensions.Logging;

namespace Ekom.Services;

/// <summary>Post-commit failures cannot turn a persisted reservation attachment into a failed save.</summary>
internal static class OrderPersistenceNotifications
{
    internal static async Task RunAsync(Guid orderId, ILogger logger, params Func<Task>[] notifications)
    {
        foreach (var notify in notifications)
        {
            try { await notify().ConfigureAwait(false); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Order {OrderId} was persisted, but a post-save notification failed", orderId);
            }
        }
    }
}
