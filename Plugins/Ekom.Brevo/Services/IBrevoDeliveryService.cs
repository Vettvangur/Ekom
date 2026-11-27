using Ekom.Brevo.Models;

namespace Ekom.Brevo.Services;

/// <summary>Durable email tracking. Call from trusted application code; no public status endpoint is exposed.</summary>
public interface IBrevoDeliveryService
{
    Task<BrevoEmailStatus?> GetAsync(string storeAlias, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BrevoEmailStatus>> GetByOrderAsync(string storeAlias, string orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BrevoEmailStatus>> GetFailuresAsync(string storeAlias, int take = 100, CancellationToken cancellationToken = default);
    /// <summary>Requeues only a definitive Failed result. Unknown/accepted sends cannot be replayed.</summary>
    Task<bool> RetryAsync(string storeAlias, string correlationId, CancellationToken cancellationToken = default);
}
