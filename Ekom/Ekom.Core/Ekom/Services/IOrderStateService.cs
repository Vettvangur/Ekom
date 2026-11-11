using Ekom.Models.Orders;

namespace Ekom.Services;

public sealed record OrderStateChangeRequest(int ExpectedRevision, string Action,
    IReadOnlyDictionary<Guid, decimal>? FulfilledQuantities = null, bool Notify = false);
public sealed record OrderStateMigrationItem(Guid OrderId, int ReferenceId, OrderStatusSnapshot Status, bool Eligible);
public sealed record OrderStateMigrationPage(IReadOnlyList<OrderStateMigrationItem> Orders, int NextReferenceId, bool HasMore, int Migrated = 0);

public interface IOrderStateService
{
    Task<OrderStatusSnapshot> ChangeAsync(Guid orderId, OrderStateChangeRequest request, CancellationToken ct = default);
    Task<OrderStateMigrationPage> PreviewAsync(int afterReferenceId = 0, CancellationToken ct = default);
    Task<OrderStateMigrationPage> MigrateAsync(int afterReferenceId = 0, CancellationToken ct = default);
}
