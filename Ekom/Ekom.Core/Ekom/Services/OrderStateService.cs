using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Orders;
using Ekom.Repositories;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Caching.Memory;
using System.Data;
using System.Net;
using System.Text.Json;

namespace Ekom.Services;

internal sealed class OrderStateService : IOrderStateService
{
    private readonly DatabaseFactory _factory;
    private readonly Configuration _configuration;
    private readonly IManagerAccessService _access;
    private readonly IMemoryCache _cache;
    private readonly CheckoutPaymentAttemptService _attempts;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _http;
    private readonly IEnumerable<IOrderStatusPersistenceHandler> _handlers;

    public OrderStateService(DatabaseFactory factory, Configuration configuration, IManagerAccessService access,
        IMemoryCache cache, CheckoutPaymentAttemptService attempts, Microsoft.AspNetCore.Http.IHttpContextAccessor http,
        IEnumerable<IOrderStatusPersistenceHandler> handlers)
    {
        _factory = factory; _configuration = configuration; _access = access; _cache = cache;
        _attempts = attempts; _http = http; _handlers = handlers;
    }

    public async Task<OrderStatusSnapshot> ChangeAsync(Guid orderId, OrderStateChangeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var db = _factory.GetDatabase();
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        var data = await db.OrderData.SingleOrDefaultAsync(x => x.UniqueId == orderId, ct).ConfigureAwait(false)
            ?? throw new EkomHttpException(HttpStatusCode.NotFound, "Order not found.");
        EnsureAccess(data.StoreAlias);
        if (request.ExpectedRevision != data.StatusRevision)
            throw new EkomHttpException(HttpStatusCode.Conflict, "The order changed. Reload it before saving.");
        await _attempts.EnsureWriteAllowedAsync(db, orderId, ct).ConfigureAwait(false);
        var previousLegacy = data.OrderStatus;
        var before = data.Status;
        if (!OrderStateRules.Initialize(data, false) || data.Status.RequiresReview)
            throw new EkomHttpException(HttpStatusCode.Conflict, "Historical payment or fulfillment evidence needs review before this order can be changed.");
        var quantities = request.Action == "Fulfill"
            ? new OrderInfo(data).OrderLines.ToDictionary(x => x.Key, x => x.Quantity) : null;
        Apply(data, request, quantities);
        OrderStateRules.AutoClose(data, _configuration.AutoClosePaidAndFulfilledOrders(data.StoreAlias));
        data.OrderStatusCol = OrderStateRules.Project(data);
        data.StatusRevision = checked(data.StatusRevision + 1);
        data.UpdateDate = DateTime.Now;
        // Only status fields are updated here. Checkout payload, money, and inventory are untouched.
        await WriteStateAsync(db, data, ct).ConfigureAwait(false);
        if (request.Action.StartsWith("Payment", StringComparison.Ordinal) && data.Status.PaymentStatus is { } manualStatus)
        {
            // Manual payment changes join the order's payment history so the calculated status keeps honouring them.
            await db.InsertAsync(OrderPaymentService.ManualTransaction(orderId, manualStatus,
                _http.HttpContext?.User?.Identity?.Name), token: ct).ConfigureAwait(false);
        }
        foreach (var handler in _handlers)
            if (previousLegacy != data.OrderStatus) await handler.PersistAsync(db, data, previousLegacy.ToString(), ct).ConfigureAwait(false);
        await db.InsertAsync(new OrderActivityLog
        {
            UniqueID = Guid.NewGuid(), Key = orderId, Date = DateTime.Now,
            UserName = _http.HttpContext?.User?.Identity?.Name ?? string.Empty,
            LogType = OrderActivityLogType.Success,
            Log = $"{request.Action}: {before.OrderStatus}/{before.PaymentStatus}/{before.FulfillmentStatus} -> {data.Status.OrderStatus}/{data.Status.PaymentStatus}/{data.Status.FulfillmentStatus}"
                + (request.Action != "Close" && before.OrderStatus != OrderStatus.Closed && data.Status.OrderStatus == OrderStatus.Closed ? ". Closed automatically." : string.Empty),
        }, token: ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        Invalidate(orderId);
        if (request.Notify && previousLegacy != data.OrderStatus)
        {
            var args = new OrderStatusEventArgs { OrderUniqueId = orderId, PreviousStatus = previousLegacy, Status = data.OrderStatus, OrderInfo = new OrderInfo(data) };
            OrderEvents.OnOrderStatusChanged(this, args);
            await OrderEvents.OnOrderStatusChangedAsync(this, args, ct).ConfigureAwait(false);
        }
        return data.Status;
    }

    internal static void Apply(OrderData data, OrderStateChangeRequest request, IReadOnlyDictionary<Guid, decimal>? lineQuantities)
    {
        var state = data.Status;
        if (request.Action == "Reopen")
        {
            if (state.OrderStatus != OrderStatus.Closed) throw Invalid("Only a closed order can be reopened.");
            data.OrderStateCol = nameof(OrderStatus.Open);
            data.AutoCloseSuppressed = true;
            return;
        }
        if (state.OrderStatus != OrderStatus.Open) throw Invalid("This action requires an open order.");
        switch (request.Action)
        {
            case "PaymentPending":
            case "PaymentAuthorized":
            case "PaymentPaid":
            case "PaymentPartiallyPaid":
            case "PaymentFailed":
            case "PaymentVoided":
                // Manual classification only: never invoke a provider or rewrite payment amounts.
                data.PaymentStateCol = request.Action[7..]; return;
            case "Cancel": data.OrderStateCol = nameof(OrderStatus.Cancelled); return;
            case "Close":
                if (state.PaymentStatus != PaymentStatus.Paid || state.FulfillmentStatus != FulfillmentStatus.Fulfilled)
                    throw Invalid("Payment must be Paid and all items fulfilled before closing.");
                data.OrderStateCol = nameof(OrderStatus.Closed); return;
            case "Unfulfilled":
            case "Picking":
            case "Picked":
            case "Packed":
            case "PartiallyFulfilled":
                // A manual correction replaces the fulfillment summary. Old quantities must
                // not silently restore the previous completion state on the next update.
                if (state.FulfillmentStatus.ToString() != request.Action)
                    data.FulfilledQuantitiesJson = null;
                data.FulfillmentStateCol = request.Action; return;
            case "Fulfill":
                if (lineQuantities == null || lineQuantities.Count == 0 || lineQuantities.Any(x => x.Value <= 0))
                    throw Invalid("The order must contain valid fulfillable quantities.");
                var recorded = OrderStateRules.Quantities(data);
                if (state.FulfillmentStatus == FulfillmentStatus.Fulfilled && recorded.Count == 0)
                    recorded = new Dictionary<Guid, decimal>(lineQuantities);
                var requested = request.FulfilledQuantities ?? lineQuantities;
                if (requested.Any(x => !lineQuantities.ContainsKey(x.Key) || x.Value < 0 || x.Value > lineQuantities[x.Key]
                    || (recorded.TryGetValue(x.Key, out var existing) && x.Value < existing)))
                    throw Invalid("Fulfilled quantities must belong to the order, must not decrease, and cannot exceed ordered quantities.");
                foreach (var pair in requested) recorded[pair.Key] = pair.Value;
                if (recorded.Any(x => !lineQuantities.ContainsKey(x.Key) || x.Value > lineQuantities[x.Key]))
                    throw Invalid("Order lines have changed since fulfillment. Review the recorded quantities.");
                data.FulfilledQuantitiesJson = JsonSerializer.Serialize(recorded);
                data.FulfillmentStateCol = lineQuantities.All(x => recorded.GetValueOrDefault(x.Key) == x.Value)
                    ? nameof(FulfillmentStatus.Fulfilled) : recorded.Any(x => x.Value > 0)
                    ? nameof(FulfillmentStatus.PartiallyFulfilled) : nameof(FulfillmentStatus.Unfulfilled);
                return;
            default: throw Invalid("Unsupported order action.");
        }
    }

    private static EkomHttpException Invalid(string message) => new(HttpStatusCode.BadRequest, message);
    private void EnsureAccess(string store)
    {
        if (!_access.CanAccessManager() || !_access.CanAccessStore(store))
            throw new EkomHttpException(HttpStatusCode.Forbidden, "You do not have access to this store.");
    }
    private void Invalidate(Guid id) { _cache.Remove(id); _cache.Remove(id.ToString()); }

    public Task<OrderStateMigrationPage> PreviewAsync(int afterReferenceId = 0, CancellationToken ct = default)
        => MigrationPageAsync(afterReferenceId, false, ct);
    public Task<OrderStateMigrationPage> MigrateAsync(int afterReferenceId = 0, CancellationToken ct = default)
        => MigrationPageAsync(afterReferenceId, true, ct);

    private async Task<OrderStateMigrationPage> MigrationPageAsync(int after, bool apply, CancellationToken ct)
    {
        if (!_access.CanAccessManager()) throw new EkomHttpException(HttpStatusCode.Forbidden, "Manager access is required.");
        var stores = _access.GetAllowedStoreAliases().ToList();
        await using var db = _factory.GetDatabase();
        await using var transaction = apply ? await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false) : null;
        var rows = await db.OrderData.Where(x => x.StatusMappingVersion == 0 && x.ReferenceId > after && stores.Contains(x.StoreAlias))
            .OrderBy(x => x.ReferenceId).Take(101).ToListAsync(ct).ConfigureAwait(false);
        var items = new List<OrderStateMigrationItem>();
        var changed = new List<Guid>();
        foreach (var row in rows.Take(100))
        {
            var mapped = row.Status;
            var eligible = !mapped.RequiresReview;
            if (eligible)
            {
                var locked = await CheckoutPaymentAttemptService.ReadManagerLockStatusAsync(db, row.UniqueId, ct).ConfigureAwait(false);
                eligible = !locked.HasLock && (locked.AttemptState == null
                    || locked.AttemptState == nameof(CheckoutPaymentAttemptState.Released)
                    || locked.AttemptState == nameof(CheckoutPaymentAttemptState.Completed));
            }
            // Preview the same effective state and revision that backfill will persist.
            if (eligible)
            {
                OrderStateRules.Initialize(row, _configuration.AutoClosePaidAndFulfilledOrders(row.StoreAlias));
                row.StatusRevision = checked(row.StatusRevision + 1);
            }
            items.Add(new(row.UniqueId, row.ReferenceId, row.Status, eligible));
            if (!apply || !eligible) continue;
            await WriteStateAsync(db, row, ct).ConfigureAwait(false);
            changed.Add(row.UniqueId);
        }
        if (transaction != null) await transaction.CommitAsync(ct).ConfigureAwait(false);
        foreach (var id in changed) Invalidate(id);
        // No checkout, stock, payment, email, event or plugin replay during backfill.
        return new(items, items.LastOrDefault()?.ReferenceId ?? after, rows.Count > 100, changed.Count);
    }

    internal static Task<int> WriteStateAsync(DbContext db, OrderData data, CancellationToken ct)
        => db.OrderData.Where(x => x.UniqueId == data.UniqueId)
            .Set(x => x.OrderStateCol, data.OrderStateCol).Set(x => x.PaymentStateCol, data.PaymentStateCol)
            .Set(x => x.FulfillmentStateCol, data.FulfillmentStateCol).Set(x => x.OriginalOrderStatusCol, data.OriginalOrderStatusCol)
            .Set(x => x.FulfilledQuantitiesJson, data.FulfilledQuantitiesJson).Set(x => x.StatusMappingVersion, data.StatusMappingVersion)
            .Set(x => x.StatusRevision, data.StatusRevision).Set(x => x.AutoCloseSuppressed, data.AutoCloseSuppressed)
            .Set(x => x.OrderStatusCol, data.OrderStatusCol).Set(x => x.UpdateDate, data.UpdateDate).UpdateAsync(ct);
}
