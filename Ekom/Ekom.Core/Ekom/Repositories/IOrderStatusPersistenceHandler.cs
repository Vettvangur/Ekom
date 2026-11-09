using Ekom.Models;

namespace Ekom.Repositories;

/// <summary>Optional transactional integration point for durable status-change records.</summary>
public interface IOrderStatusPersistenceHandler
{
    /// <summary>
    /// Called after an insert or status change, before its transaction commits. Use only the supplied
    /// database for local persistence. Do not commit, perform network calls, render content, or mutate
    /// the order. Exceptions roll back the order write. Previous status is null on insertion.
    /// </summary>
    Task PersistAsync(DbContext database, OrderData order, string? previousStatus, CancellationToken cancellationToken = default);
}
