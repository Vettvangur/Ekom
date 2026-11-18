using Ekom.Models.Orders;

namespace Ekom.Services;

public sealed record OrderStateChangeRequest(int ExpectedRevision, string Action,
    IReadOnlyDictionary<Guid, decimal>? FulfilledQuantities = null, bool Notify = false);
/// <summary>
/// A status change made by code that runs without a backoffice user, such as a background job,
/// a queue handler or an integration.
/// </summary>
/// <param name="Action">The same action names the manager uses, for example <c>Fulfill</c>, <c>Packed</c>, <c>PaymentPaid</c> or <c>Cancel</c>.</param>
/// <param name="Actor">Who made the change. It is stored as the user name on the order's activity log entry, so name the job or integration.</param>
/// <param name="ExpectedRevision">The order's status revision the caller read. Leave <c>null</c> to apply the change to the order as it is now.</param>
/// <param name="FulfilledQuantities">For <c>Fulfill</c>: cumulative quantities per order line. Leave <c>null</c> to fulfill every line.</param>
/// <param name="Notify">Whether the status change events are raised.</param>
public sealed record OrderSystemStateChange(string Action, string Actor, int? ExpectedRevision = null,
    IReadOnlyDictionary<Guid, decimal>? FulfilledQuantities = null, bool Notify = true);
public sealed record OrderStateMigrationItem(Guid OrderId, int ReferenceId, OrderStatusSnapshot Status, bool Eligible);
public sealed record OrderStateMigrationPage(IReadOnlyList<OrderStateMigrationItem> Orders, int NextReferenceId, bool HasMore, int Migrated = 0);

public interface IOrderStateService
{
    /// <summary>
    /// Changes an order's status for the signed-in backoffice user. The user needs manager access to the order's store.
    /// </summary>
    Task<OrderStatusSnapshot> ChangeAsync(Guid orderId, OrderStateChangeRequest request, CancellationToken ct = default);
    /// <summary>
    /// Changes an order's status from code that has no backoffice user. No manager or store access is checked,
    /// so call it only from trusted server code, never with an action or order id taken from a storefront request.
    /// Every other rule of <see cref="ChangeAsync"/> applies.
    /// </summary>
    Task<OrderStatusSnapshot> ChangeAsSystemAsync(Guid orderId, OrderSystemStateChange change, CancellationToken ct = default);
    Task<OrderStateMigrationPage> PreviewAsync(int afterReferenceId = 0, CancellationToken ct = default);
    Task<OrderStateMigrationPage> MigrateAsync(int afterReferenceId = 0, CancellationToken ct = default);
}
