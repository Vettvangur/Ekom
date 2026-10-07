using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Net;

namespace Ekom.Services;

internal sealed class CheckoutPaymentAttemptService
{
    private readonly DatabaseFactory _factory;
    private readonly IStockReservationService _stock;
    private readonly ActivityLogRepository _activity;
    private readonly ILogger<CheckoutPaymentAttemptService> _logger;
    private readonly IMemoryCache _cache;
    private readonly ICheckoutGiftcardReservations? _giftcards;

    public CheckoutPaymentAttemptService(DatabaseFactory factory, IStockReservationService stock,
        ActivityLogRepository activity, ILogger<CheckoutPaymentAttemptService> logger,
        IMemoryCache cache, ICheckoutGiftcardReservations? giftcards = null)
    {
        _factory = factory;
        _stock = stock;
        _activity = activity;
        _logger = logger;
        _cache = cache;
        _giftcards = giftcards;
    }

    public async Task<CheckoutPaymentOperationScope> BeginCheckoutAsync(IOrderInfo order, CancellationToken ct = default)
    {
        var scope = await AcquireAsync(order.UniqueId, ct).ConfigureAwait(false);
        try
        {
            using var activation = scope.Enter();
            await using var db = _factory.GetDatabase();
            var data = await ReadOrderAsync(db, order.UniqueId, ct).ConfigureAwait(false);
            if (await RequiresReconciliationAsync(db, order.UniqueId, ct).ConfigureAwait(false))
                throw Conflict("A payment callback requires manual reconciliation before checkout.", CheckoutConflictReason.PaymentReview);
            if (await IsPaidOrCompletedAsync(db, data, ct).ConfigureAwait(false))
                throw Conflict("Paid or completed orders cannot start another payment attempt.", CheckoutConflictReason.Completed);
            var active = await ReadActiveAsync(db, order.UniqueId, ct).ConfigureAwait(false);
            if (active != null && active.State != CheckoutPaymentAttemptState.Released)
            {
                if (active.State == CheckoutPaymentAttemptState.Completed)
                    throw Conflict("The current payment attempt is completed.", CheckoutConflictReason.Completed);
                if (active.State == CheckoutPaymentAttemptState.CompletionPending ||
                    active.State == CheckoutPaymentAttemptState.ReconciliationRequired)
                    throw Conflict("Payment requires reconciliation before retrying checkout.", CheckoutConflictReason.PaymentReview);
                if (active.State != CheckoutPaymentAttemptState.ReleasePending &&
                    active.State != CheckoutPaymentAttemptState.Preparing &&
                    !await HasExpiredAsync(db, active, data, ct).ConfigureAwait(false))
                    throw Conflict("A payment attempt is already active. Cancel it before retrying.", CheckoutConflictReason.Busy);
                var resetReason = active.State == CheckoutPaymentAttemptState.Preparing
                    ? "Unsubmitted attempt before checkout retry"
                    : "Expired or release-pending attempt before checkout retry";
                data = await ReleaseOwnedAsync(db, scope, active, data, resetReason, ct).ConfigureAwait(false);
                Apply(order, data);
            }
            else if (await HasOwnedHoldsAsync(db, data, ct).ConfigureAwait(false) || HasClaims(ReadGiftcards(data.OrderInfo)))
            {
                data = await ReleaseOwnedAsync(db, scope, null, data, "Legacy holds before checkout retry", ct).ConfigureAwait(false);
                Apply(order, data);
            }

            // The caller may have fetched this cart before a different node's
            // edit committed. Always refresh under the acquired owner before
            // checkout's customer/provider mutations can save that stale cart.
            Apply(order, data);

            var attemptId = Guid.NewGuid();
            await using (var transaction = await db.BeginTransactionAsync().ConfigureAwait(false))
            {
                await db.InsertAsync(new CheckoutPaymentAttemptData
                {
                    AttemptId = attemptId, OrderId = order.UniqueId,
                    State = CheckoutPaymentAttemptState.Preparing, CreatedUtc = DateTime.UtcNow,
                }, token: ct).ConfigureAwait(false);
                var changed = await db.GetTable<CheckoutPaymentOperationData>()
                    .Where(x => x.OrderId == scope.OrderId && x.Owner == scope.Owner)
                    .Set(x => x.ActiveAttemptId, (Guid?)attemptId).UpdateAsync(ct).ConfigureAwait(false);
                if (changed != 1) throw Conflict("Checkout ownership changed.");
                scope.PreserveOwnership = true;
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                scope.PreserveOwnership = false;
            }
            scope.AttemptId = attemptId;
            scope.IsCheckout = true;

            var selections = ReadGiftcards(data.OrderInfo);
            if (_giftcards != null && selections.Count > 0)
            {
                // Persist renewed claims before any provider invocation. Uncertain writes
                // retain ownership so another node cannot release unknown external holds.
                scope.PreserveOwnership = true;
                var renewed = await _giftcards.ReserveAsync(order.UniqueId, attemptId, selections, ct).ConfigureAwait(false);
                if (HasSettledGiftcards(renewed))
                {
                    await ReconciliationAsync(order.UniqueId, attemptId, "Giftcard reserve returned redeemed or settled selection").ConfigureAwait(false);
                    throw Conflict("A redeemed or settled giftcard requires reconciliation before another payment can be submitted.", CheckoutConflictReason.PaymentReview);
                }
                var json = JObject.Parse(data.OrderInfo);
                json[nameof(OrderInfo.Giftcards)] = JArray.FromObject(renewed);
                data.OrderInfo = json.ToString(Formatting.None);
                await SaveOrderAsync(db, scope, data, ct).ConfigureAwait(false);
                Apply(order, data);
                scope.PreserveOwnership = false;
            }
            return scope;
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<Guid> SubmitAsync(IOrderInfo order, CancellationToken ct = default)
    {
        var scope = CheckoutPaymentOperationScope.Current;
        if (scope == null || !scope.IsCheckout || scope.OrderId != order.UniqueId || scope.AttemptId == null)
            throw Conflict("Submitting payment requires the current checkout operation scope.");
        await using var db = _factory.GetDatabase();
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        await EnsureWriteAllowedAsync(db, order.UniqueId, ct).ConfigureAwait(false);
        var data = await ReadOrderAsync(db, order.UniqueId, ct).ConfigureAwait(false);
        if (await IsPaidOrCompletedAsync(db, data, ct).ConfigureAwait(false))
            throw Conflict("The order is already paid or completed.", CheckoutConflictReason.Completed);
        var actual = JsonConvert.SerializeObject(order, EkomJsonDotNet.Settings);
        if (!SamePurchase(data.OrderInfo, actual))
            throw Conflict("The submitted order differs from its persisted purchase. Save it before payment.");
        var active = await ReadActiveAsync(db, order.UniqueId, ct).ConfigureAwait(false);
        if (active?.AttemptId != scope.AttemptId || active.State != CheckoutPaymentAttemptState.Preparing)
            throw Conflict("Only the current preparing payment attempt may be submitted.");
        var ids = ReadIds(data.OrderInfo).Distinct(StringComparer.Ordinal).ToList();
        if (!await HoldsActiveAsync(db, ids, false, ct).ConfigureAwait(false))
            throw Conflict("Payment reservations expired or closed before submission.");
        scope.PreserveOwnership = true;
        DateTime? deadline = null;
        if (ids.Count > 0)
        {
            deadline = await db.GetTable<StockReservationData>().Where(x => ids.Contains(x.Id))
                .MinAsync(x => (DateTime?)x.ExpiresUtc, ct).ConfigureAwait(false);
            if (deadline == null) throw Conflict("Payment reservations disappeared before submission.");
            var now = DateTime.UtcNow;
            var orderId = order.UniqueId.ToString();
            var aligned = await db.GetTable<StockReservationData>()
                .Where(x => ids.Contains(x.Id) && x.OrderId == orderId && x.State == StockReservationState.Active && x.ExpiresUtc > now)
                .Set(x => x.ExpiresUtc, deadline.Value).UpdateAsync(ct).ConfigureAwait(false);
            if (aligned != ids.Count) throw Conflict("Payment reservations changed before submission.");
        }
        else
        {
            // No hold ends this attempt, so without a deadline an abandoned payment keeps the basket busy until it is edited.
            deadline = DateTime.UtcNow + Configuration.Instance.GetReservationTimeout(order.StoreInfo.Alias);
        }
        var updated = await db.GetTable<CheckoutPaymentAttemptData>()
            .Where(x => x.AttemptId == active.AttemptId && x.State == CheckoutPaymentAttemptState.Preparing)
            .Set(x => x.SubmittedOrderData, JsonConvert.SerializeObject(data))
            .Set(x => x.SubmittedOrderInfo, data.OrderInfo)
            .Set(x => x.ReservationIds, JsonConvert.SerializeObject(ids))
            .Set(x => x.SubmittedUtc, (DateTime?)DateTime.UtcNow)
            .Set(x => x.ExpiresUtc, deadline)
            .Set(x => x.State, CheckoutPaymentAttemptState.Submitted)
            .UpdateAsync(ct).ConfigureAwait(false);
        if (updated != 1) throw Conflict("The payment attempt changed before submission.");
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        scope.PreserveOwnership = false;
        return active.AttemptId;
    }

    public async Task<OrderData?> GetSubmittedOrderAsync(Guid orderId, Guid attemptId, CancellationToken ct = default)
    {
        await using var db = _factory.GetDatabase();
        var snapshot = await db.GetTable<CheckoutPaymentAttemptData>()
            .Where(x => x.OrderId == orderId && x.AttemptId == attemptId)
            .Select(x => x.SubmittedOrderData).SingleOrDefaultAsync(ct).ConfigureAwait(false);
        if (snapshot == null) return null;
        var data = JsonConvert.DeserializeObject<OrderData>(snapshot);
        return data?.UniqueId == orderId ? data : null;
    }

    public async Task<OrderData?> ReleaseAsync(Guid orderId, Guid? attemptId, string reason, CancellationToken ct = default)
    {
        await LogAsync(orderId, $"Payment release attempted; attempt={attemptId}; reason={reason}; outcome=Requested", true).ConfigureAwait(false);
        CheckoutPaymentOperationScope scope;
        try
        {
            scope = await AcquireAsync(orderId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await LogAsync(orderId, $"Payment release blocked; attempt={attemptId}; reason={reason}; outcome=Blocked; FailureType={ex.GetType().Name}").ConfigureAwait(false);
            throw;
        }
        await using var ownership = scope;
        using var activation = scope.Enter();
        await using var db = _factory.GetDatabase();
        var data = await ReadOrderAsync(db, orderId, ct).ConfigureAwait(false);
        var active = await ReadActiveAsync(db, orderId, ct).ConfigureAwait(false);
        if ((active != null && active.AttemptId != attemptId) || (active == null && attemptId != null))
        {
            await LogAsync(orderId, $"Payment release ignored: stale or missing attempt token; attempt={attemptId}; reason={reason}; outcome=StaleSkipped").ConfigureAwait(false);
            return null;
        }
        if (await IsPaidOrCompletedAsync(db, data, ct).ConfigureAwait(false) || await RequiresReconciliationAsync(db, orderId, ct).ConfigureAwait(false) ||
            active?.State is CheckoutPaymentAttemptState.Completed or CheckoutPaymentAttemptState.CompletionPending or CheckoutPaymentAttemptState.ReconciliationRequired)
        {
            await ReconciliationAsync(orderId, attemptId, "Release requested for paid/completing order: " + reason, false).ConfigureAwait(false);
            return null;
        }
        if (active?.State == CheckoutPaymentAttemptState.Released)
        {
            await LogAsync(orderId, $"Payment release already terminal; attempt={attemptId}; reason={reason}; outcome=AlreadyReleased").ConfigureAwait(false);
            return null;
        }
        if (active == null && !await HasOwnedHoldsAsync(db, data, ct).ConfigureAwait(false) && !HasClaims(ReadGiftcards(data.OrderInfo)))
        {
            await LogAsync(orderId, $"Payment release already terminal; attempt={attemptId}; reason={reason}; outcome=NoOwnedHolds").ConfigureAwait(false);
            return null;
        }
        return await ReleaseOwnedAsync(db, scope, active, data, reason, ct).ConfigureAwait(false);
    }

    public async Task<CheckoutPaymentOperationScope> BeginEditAsync(OrderInfo order, string reason, CancellationToken ct = default)
    {
        var scope = await AcquireAsync(order.UniqueId, ct).ConfigureAwait(false);
        // Nested customer/provider saves during checkout must not release its own holds.
        if (CheckoutPaymentOperationScope.Current?.OrderId == order.UniqueId) return scope;
        try
        {
            using var activation = scope.Enter();
            await using var db = _factory.GetDatabase();
            var data = await ReadOrderAsync(db, order.UniqueId, ct).ConfigureAwait(false);
            if (await RequiresReconciliationAsync(db, order.UniqueId, ct).ConfigureAwait(false))
                throw Conflict("A payment callback requires manual reconciliation before editing.", CheckoutConflictReason.PaymentReview);
            var active = await ReadActiveAsync(db, order.UniqueId, ct).ConfigureAwait(false);
            if (active?.State == CheckoutPaymentAttemptState.Completed)
                throw Conflict("The current payment attempt is completed.", CheckoutConflictReason.Completed);
            if (active?.State is CheckoutPaymentAttemptState.CompletionPending or CheckoutPaymentAttemptState.ReconciliationRequired)
            {
                _logger.LogWarning("Payment requires reconciliation before editing order {OrderId} ({OrderNumber}); attempt {AttemptId} is {AttemptState}; reason: {EditReason}",
                    data.UniqueId, data.OrderNumber, active.AttemptId, active.State, reason);
                throw Conflict("Payment requires reconciliation before editing this order.", CheckoutConflictReason.PaymentReview);
            }
            if (await IsPaidOrCompletedAsync(db, data, ct).ConfigureAwait(false))
                throw Conflict("Paid or completed orders cannot be edited as an unpaid cart.", CheckoutConflictReason.Completed);
            if (active != null && active.State != CheckoutPaymentAttemptState.Released ||
                await HasOwnedHoldsAsync(db, data, ct).ConfigureAwait(false) || HasClaims(ReadGiftcards(data.OrderInfo)))
            {
                data = await ReleaseOwnedAsync(db, scope, active, data, reason, ct).ConfigureAwait(false);
                Apply(order, data);
            }
            // Nested scopes returned above deliberately preserve in-progress
            // mutations; outer edits always start from the durable purchase.
            Apply(order, data);
            return scope;
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<CheckoutPaymentOperationScope> BeginOrderInformationUpdateAsync(OrderInfo order, CancellationToken ct = default)
    {
        var nested = CheckoutPaymentOperationScope.Current?.OrderId == order.UniqueId;
        var scope = await AcquireAsync(order.UniqueId, orderInformationUpdate: true, ct).ConfigureAwait(false);
        try
        {
            // Information updates retain payment state and holds, even during reconciliation.
            // Refresh outer updates under ownership; nested updates must retain in-flight changes.
            if (!nested)
            {
                await using var db = _factory.GetDatabase();
                Apply(order, await ReadOrderAsync(db, order.UniqueId, ct).ConfigureAwait(false));
            }
            return scope;
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<CheckoutPaymentOperationScope> BeginReservationUpdateAsync(OrderInfo order, CancellationToken ct = default)
    {
        var scope = await AcquireAsync(order.UniqueId, ct).ConfigureAwait(false);
        if (CheckoutPaymentOperationScope.Current?.OrderId == order.UniqueId) return scope;
        try
        {
            using var activation = scope.Enter();
            await using var db = _factory.GetDatabase();
            var data = await ReadOrderAsync(db, order.UniqueId, ct).ConfigureAwait(false);
            if (await IsPaidOrCompletedAsync(db, data, ct).ConfigureAwait(false))
                throw Conflict("Paid or completed orders cannot attach or clear reservation metadata.", CheckoutConflictReason.Completed);
            if (await RequiresReconciliationAsync(db, order.UniqueId, ct).ConfigureAwait(false))
                throw Conflict("Reconciliation-required orders cannot attach or clear reservation metadata.", CheckoutConflictReason.PaymentReview);
            var active = await ReadActiveAsync(db, order.UniqueId, ct).ConfigureAwait(false);
            if (active != null && active.State != CheckoutPaymentAttemptState.Released)
                throw Conflict("Reservation metadata cannot change while a payment attempt is active or completing.",
                    active.State == CheckoutPaymentAttemptState.Completed ? CheckoutConflictReason.Completed
                        : active.State is CheckoutPaymentAttemptState.CompletionPending or CheckoutPaymentAttemptState.ReconciliationRequired
                            ? CheckoutConflictReason.PaymentReview : CheckoutConflictReason.Busy);
            // Do not inspect/release owned rows here: incoming external holds may
            // have just been created and still need attaching by the caller.
            Apply(order, data);
            return scope;
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<bool> CompleteAsync(Guid orderId, Guid? attemptId, Func<Task> complete, CancellationToken ct = default)
    {
        await using var scope = await AcquireAsync(orderId, ct).ConfigureAwait(false);
        using var activation = scope.Enter();
        // Internal offline completion can omit the return token only while it
        // carries the same checkout capability; external callbacks cannot do this.
        if (attemptId == null && scope.IsCheckout && scope.OrderId == orderId)
            attemptId = scope.AttemptId;
        await using var db = _factory.GetDatabase();
        var data = await ReadOrderAsync(db, orderId, ct).ConfigureAwait(false);
        var active = await ReadActiveAsync(db, orderId, ct).ConfigureAwait(false);
        if (await RequiresReconciliationAsync(db, orderId, ct).ConfigureAwait(false))
        {
            await ReconciliationAsync(orderId, attemptId, "Order already requires manual payment reconciliation", false).ConfigureAwait(false);
            return false;
        }
        if (active == null)
        {
            // Missing tokens are supported only for genuinely legacy orders, never
            // for orders whose attempt history could represent a late callback.
            if (attemptId != null || await db.GetTable<CheckoutPaymentAttemptData>().AnyAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false))
            {
                await ReconciliationAsync(orderId, attemptId, "No current payment attempt").ConfigureAwait(false);
                return false;
            }
            if (IsPaid(data)) return true;
            // A stock receipt prevents release/re-reservation, but is not proof
            // that legacy completion's remaining order/status work succeeded.
            var stockCompleted = await db.GetTable<CheckoutStockCompletionData>()
                .AnyAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false);
            if (!await HoldsActiveAsync(db, ReadIds(data.OrderInfo), stockCompleted, ct).ConfigureAwait(false))
            {
                await ReconciliationAsync(orderId, attemptId, "Legacy payment reservations expired or closed").ConfigureAwait(false);
                return false;
            }
            scope.PreserveOwnership = true;
            try
            {
                await complete().ConfigureAwait(false);
                scope.PreserveOwnership = false;
                return true;
            }
            catch
            {
                await ReconciliationAsync(orderId, null, "Legacy paid completion failed; durable owner retained for recovery", false).ConfigureAwait(false);
                throw;
            }
        }
        if (active.AttemptId == attemptId && active.State == CheckoutPaymentAttemptState.Completed) return true;
        if (active.AttemptId != attemptId ||
            active.State is not (CheckoutPaymentAttemptState.Submitted or CheckoutPaymentAttemptState.CompletionPending) ||
            active.SubmittedOrderInfo == null || !SamePurchase(active.SubmittedOrderInfo, data.OrderInfo) ||
            !SameOrderData(active.SubmittedOrderData, data) ||
            !await HoldsActiveAsync(db, JsonConvert.DeserializeObject<List<string>>(active.ReservationIds) ?? [],
                active.State == CheckoutPaymentAttemptState.CompletionPending, ct).ConfigureAwait(false))
        {
            await ReconciliationAsync(orderId, attemptId, "Attempt is stale, closed, expired, or its frozen purchase changed").ConfigureAwait(false);
            return false;
        }
        scope.AttemptId = active.AttemptId;
        scope.PreserveOwnership = true;
        await SetStateAsync(db, active.AttemptId, CheckoutPaymentAttemptState.CompletionPending, ct).ConfigureAwait(false);
        scope.PreserveOwnership = false;
        try
        {
            await complete().ConfigureAwait(false);
            scope.PreserveOwnership = true;
            await db.GetTable<CheckoutPaymentAttemptData>().Where(x => x.AttemptId == active.AttemptId)
                .Set(x => x.State, CheckoutPaymentAttemptState.Completed)
                .Set(x => x.ClosedUtc, (DateTime?)DateTime.UtcNow).UpdateAsync(ct).ConfigureAwait(false);
            scope.PreserveOwnership = false;
            return true;
        }
        catch
        {
            // CompletionPending is durable and blocks release/edit/retry. The same
            // verified payment callback may retry idempotent completion.
            await ReconciliationAsync(orderId, attemptId, "Paid completion failed; retry verified completion or reconcile", false).ConfigureAwait(false);
            throw;
        }
    }

    public async Task EnsureWriteAllowedAsync(DbContext db, Guid orderId, CancellationToken ct = default)
    {
        var ambient = CheckoutPaymentOperationScope.Current;
        if (ambient?.OrderId == orderId && ambient.IsManagerOverride) return;
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleOrDefaultAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false);
        if (operation?.ReconciliationRequired == true &&
            !(ambient?.OrderId == orderId && ambient.IsOrderInformationUpdate))
            throw Conflict("This order requires manual payment reconciliation before it can be changed.", CheckoutConflictReason.PaymentReview);
        if (operation?.Owner != null && (ambient?.OrderId != orderId || ambient.Owner != operation.Owner))
            throw Conflict("The order is owned by another checkout operation.", CheckoutConflictReason.Busy);
        if (ambient?.OrderId == orderId && operation?.Owner != ambient.Owner)
            throw Conflict("Checkout operation ownership was lost.");
        var active = await ReadActiveAsync(db, orderId, ct).ConfigureAwait(false);
        if (active != null && active.State != CheckoutPaymentAttemptState.Released && active.State != CheckoutPaymentAttemptState.Completed &&
            (operation?.Owner == null || ambient?.OrderId != orderId))
            throw Conflict("Release the current payment attempt under an edit operation before changing this order.",
                active.State is CheckoutPaymentAttemptState.CompletionPending or CheckoutPaymentAttemptState.ReconciliationRequired
                    ? CheckoutConflictReason.PaymentReview : CheckoutConflictReason.Busy);
    }

    private Task<CheckoutPaymentOperationScope> AcquireAsync(Guid orderId, CancellationToken ct)
        => AcquireAsync(orderId, orderInformationUpdate: false, ct);

    private async Task<CheckoutPaymentOperationScope> AcquireAsync(Guid orderId, bool orderInformationUpdate, CancellationToken ct)
    {
        var ambient = CheckoutPaymentOperationScope.Current;
        if (ambient?.OrderId == orderId)
        {
            if (ambient.IsInformationOnly && !orderInformationUpdate)
                throw Conflict("An information update cannot authorize another checkout operation.", CheckoutConflictReason.Busy);
            await using var nestedDb = _factory.GetDatabase();
            var nested = new CheckoutPaymentOperationScope(orderId, ambient.Owner, null, ambient)
            {
                AttemptId = ambient.AttemptId,
                IsCheckout = ambient.IsCheckout,
                IsOrderInformationUpdate = orderInformationUpdate || ambient.IsOrderInformationUpdate,
                IsInformationOnly = ambient.IsInformationOnly,
                IsManagerOverride = ambient.IsManagerOverride,
            };
            using var activation = nested.Enter();
            await EnsureWriteAllowedAsync(nestedDb, orderId, ct).ConfigureAwait(false);
            return nested;
        }
        await using var db = _factory.GetDatabase();
        var owner = Guid.NewGuid().ToString();
        if (!await db.GetTable<CheckoutPaymentOperationData>().AnyAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false))
        {
            try
            {
                await db.InsertAsync(new CheckoutPaymentOperationData { OrderId = orderId }, token: ct).ConfigureAwait(false);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Only tolerate a competing first insert; all other errors propagate.
                if (!await db.GetTable<CheckoutPaymentOperationData>().AnyAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false)) throw;
            }
        }
        var claimed = await db.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == orderId && x.Owner == null)
            .Set(x => x.Owner, owner).UpdateAsync(ct).ConfigureAwait(false);
        if (claimed != 1)
        {
            if (await RequiresReconciliationAsync(db, orderId, ct).ConfigureAwait(false))
                throw Conflict("This order requires manual payment reconciliation before checkout.", CheckoutConflictReason.PaymentReview);
            var active = await ReadActiveAsync(db, orderId, ct).ConfigureAwait(false);
            if (active?.State == CheckoutPaymentAttemptState.Completed)
                throw Conflict("The current payment attempt is completed.", CheckoutConflictReason.Completed);
            if (active?.State is CheckoutPaymentAttemptState.CompletionPending or CheckoutPaymentAttemptState.ReconciliationRequired)
                throw Conflict("The current payment attempt requires review before checkout.", CheckoutConflictReason.PaymentReview);
            throw Conflict("Another checkout operation owns this order. Retry after it finishes.", CheckoutConflictReason.Busy);
        }
        CheckoutPaymentOperationScope? scope = null;
        scope = new CheckoutPaymentOperationScope(orderId, owner, async () =>
        {
            await using var releaseDb = _factory.GetDatabase();
            try
            {
                // A validation return or exception before submission must not
                // strand a Preparing attempt. Preparation compensation has run
                // before the outer checkout scope is disposed by the caller.
                if (scope is { IsCheckout: true, AttemptId: not null })
                {
                    var active = await ReadActiveAsync(releaseDb, orderId, CancellationToken.None).ConfigureAwait(false);
                    if (active?.AttemptId == scope.AttemptId && active.State == CheckoutPaymentAttemptState.Preparing)
                    {
                        await EnsurePreparationAllowedAsync(releaseDb, orderId, CancellationToken.None).ConfigureAwait(false);
                        var data = await ReadOrderAsync(releaseDb, orderId, CancellationToken.None).ConfigureAwait(false);
                        await ReleaseOwnedAsync(releaseDb, scope, active, data, "Checkout ended before payment submission", CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (scope?.PreserveOwnership != true)
                    await releaseDb.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == orderId && x.Owner == owner)
                        .Set(x => x.Owner, (string?)null).UpdateAsync(CancellationToken.None).ConfigureAwait(false);
            }
        })
        {
            IsOrderInformationUpdate = orderInformationUpdate,
            IsInformationOnly = orderInformationUpdate,
        };
        try
        {
            await EnsurePreparationAllowedAsync(db, orderId, ct).ConfigureAwait(false);
            return scope;
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task EnsurePreparationAllowedAsync(DbContext db, Guid orderId, CancellationToken ct)
    {
        var preparation = await db.GetTable<CheckoutPreparationData>().SingleOrDefaultAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false);
        if (preparation?.Owner != null &&
            (CheckoutPreparationScope.Current?.Ownership.OrderId != orderId || CheckoutPreparationScope.Current.Ownership.Owner != preparation.Owner))
            throw Conflict("Checkout preparation still owns this order. Its holds cannot be edited or released.", CheckoutConflictReason.Busy);
    }

    private async Task<OrderData> ReleaseOwnedAsync(DbContext db, CheckoutPaymentOperationScope scope,
        CheckoutPaymentAttemptData? attempt, OrderData data, string reason, CancellationToken ct)
    {
        if (await IsPaidOrCompletedAsync(db, data, ct).ConfigureAwait(false))
            throw Conflict("Paid or completed orders cannot release payment holds.", CheckoutConflictReason.Completed);
        // Audit persistence precedes all release effects; if it fails, release fails closed.
        await LogAsync(data.UniqueId, $"Payment release attempted; attempt={attempt?.AttemptId}; reason={reason}", true).ConfigureAwait(false);
        if (attempt != null) await SetStateAsync(db, attempt.AttemptId, CheckoutPaymentAttemptState.ReleasePending, ct).ConfigureAwait(false);
        var ids = attempt?.SubmittedOrderInfo != null
            ? JsonConvert.DeserializeObject<List<string>>(attempt.ReservationIds) ?? []
            : await ReadOwnedIdsAsync(db, data, ct).ConfigureAwait(false);
        var giftcards = ReadGiftcards(attempt?.SubmittedOrderInfo ?? data.OrderInfo);
        try
        {
            if (HasSettledGiftcards(giftcards))
            {
                await ReconciliationAsync(data.UniqueId, attempt?.AttemptId, "Redeemed or settled giftcard cannot be released as a payment hold").ConfigureAwait(false);
                throw Conflict("A redeemed or settled giftcard requires reconciliation; it cannot be released or reacquired for payment.", CheckoutConflictReason.PaymentReview);
            }
            if (HasClaims(giftcards) && _giftcards == null)
                throw Conflict("Register ICheckoutGiftcardReservations to release external giftcard claims before canceling or editing this order.");
            foreach (var id in ids.Distinct(StringComparer.Ordinal))
            {
                var reservation = await db.GetTable<StockReservationData>().SingleOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
                if (reservation?.OrderId != null && !string.Equals(reservation.OrderId, data.UniqueId.ToString(), StringComparison.OrdinalIgnoreCase))
                    throw Conflict("A reservation belongs to a different order and cannot be released by this payment attempt.");
                if (reservation?.State == StockReservationState.Consumed)
                {
                    await ReconciliationAsync(data.UniqueId, attempt?.AttemptId, "Consumed payment stock cannot be released as an unpaid cart").ConfigureAwait(false);
                    throw Conflict("Consumed payment stock requires reconciliation; it cannot be reset as an unpaid cart.", CheckoutConflictReason.PaymentReview);
                }
                StockReservationResult result;
                try
                {
                    result = await _stock.ReleaseAsync(id, ct).ConfigureAwait(false);
                }
                catch
                {
                    await LogAsync(data.UniqueId, FormattableString.Invariant($"Payment reservation release failed; attempt={attempt?.AttemptId}; id={id}; stockKey={reservation?.Key}; quantity={reservation?.Quantity ?? 0}; restored=0; outcome=Failed; reason={reason}")).ConfigureAwait(false);
                    throw;
                }
                var restored = result.Status == StockReservationStatus.Released ? reservation?.Quantity ?? 0 : 0;
                await LogAsync(data.UniqueId, FormattableString.Invariant($"Payment reservation release; attempt={attempt?.AttemptId}; id={id}; stockKey={reservation?.Key}; quantity={reservation?.Quantity ?? 0}; restored={restored}; outcome={result.Status}; reason={reason}")).ConfigureAwait(false);
                if (result.Status is not (StockReservationStatus.Released or StockReservationStatus.AlreadyReleased or StockReservationStatus.AlreadyExpired or StockReservationStatus.Expired or StockReservationStatus.NotFound))
                {
                    if (result.Status is StockReservationStatus.AlreadyConsumed or StockReservationStatus.Consumed or StockReservationStatus.LateConsumption)
                    {
                        await ReconciliationAsync(data.UniqueId, attempt?.AttemptId, "Reservation release found consumed stock").ConfigureAwait(false);
                        throw Conflict("Consumed payment stock requires reconciliation before checkout.", CheckoutConflictReason.PaymentReview);
                    }
                    throw Conflict("Payment reservation could not be released; retry release or reconcile.");
                }
            }
            if (_giftcards != null && giftcards.Count > 0)
                await _giftcards.ReleaseAsync(data.UniqueId, attempt?.AttemptId ?? Guid.Empty, giftcards, ct).ConfigureAwait(false);

            var json = JObject.Parse(data.OrderInfo);
            var released = ids.ToHashSet(StringComparer.Ordinal);
            var remaining = ReadIds(data.OrderInfo).Where(x => !released.Contains(x)).ToArray();
            // Unexpected newer holds must never be wiped by this attempt's reset.
            if (remaining.Length != 0) throw Conflict("Order contains holds outside the released payment attempt; reconciliation required.");
            json[nameof(OrderInfo.ReservationIds)] = new JArray();
            json[nameof(OrderInfo.HangfireJobs)] = new JArray();
            foreach (var giftcard in json[nameof(OrderInfo.Giftcards)]?.OfType<JObject>() ?? [])
            {
                // All selections belong to the same frozen purchase while this owner
                // fences edits. Do not clear the giftcard validity or code/amount.
                giftcard[nameof(Giftcard.Claimed)] = false;
                foreach (var field in new[] { nameof(Giftcard.ClaimId), nameof(Giftcard.ClaimDate), nameof(Giftcard.TransactionId), nameof(Giftcard.UsedDate), "HoldValidUntil", "holdValidUntil" })
                    if (giftcard.Property(field) != null) giftcard[field] = null;
            }
            data.OrderInfo = json.ToString(Formatting.None);
            data.OrderStatus = OrderStatus.Incomplete;
            scope.PreserveOwnership = true;
            await using var transaction = await db.BeginTransactionAsync().ConfigureAwait(false);
            await SaveOrderAsync(db, scope, data, ct).ConfigureAwait(false);
            var preparation = await db.GetTable<CheckoutPreparationData>().SingleOrDefaultAsync(x => x.OrderId == data.UniqueId, ct).ConfigureAwait(false);
            if (preparation != null)
            {
                var protectedIds = JsonConvert.DeserializeObject<List<string>>(preparation.ProtectedIds) ?? [];
                await db.GetTable<CheckoutPreparationData>().Where(x => x.OrderId == data.UniqueId && x.Owner == preparation.Owner)
                    .Set(x => x.ProtectedIds, JsonConvert.SerializeObject(protectedIds.Where(x => !released.Contains(x))))
                    .UpdateAsync(ct).ConfigureAwait(false);
            }
            if (attempt != null)
                await db.GetTable<CheckoutPaymentAttemptData>().Where(x => x.AttemptId == attempt.AttemptId)
                    .Set(x => x.State, CheckoutPaymentAttemptState.Released)
                    .Set(x => x.ClosedUtc, (DateTime?)DateTime.UtcNow).UpdateAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            scope.PreserveOwnership = false;
            ClearCache(data.UniqueId);
            await LogAsync(data.UniqueId, $"Payment release completed; attempt={attempt?.AttemptId}; reason={reason}").ConfigureAwait(false);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError("Payment release failed. OrderId: {OrderId}; AttemptId: {AttemptId}; Reason: {Reason}; FailureType: {FailureType}", data.UniqueId, attempt?.AttemptId, reason, ex.GetType().Name);
            await LogAsync(data.UniqueId, $"Payment release failed; attempt={attempt?.AttemptId}; reason={reason}; outcome=Failed; FailureType={ex.GetType().Name}").ConfigureAwait(false);
            throw;
        }
    }

    private async Task SaveOrderAsync(DbContext db, CheckoutPaymentOperationScope scope, OrderData data, CancellationToken ct)
    {
        data.UpdateDate = DateTime.Now;
        var updated = await db.GetTable<OrderData>()
            .Where(x => x.UniqueId == data.UniqueId && x.PaidDate == null &&
                db.GetTable<CheckoutPaymentOperationData>().Any(o => o.OrderId == x.UniqueId && o.Owner == scope.Owner))
            .Set(x => x.OrderInfo, data.OrderInfo).Set(x => x.OrderStatusCol, data.OrderStatusCol)
            .Set(x => x.UpdateDate, data.UpdateDate).UpdateAsync(ct).ConfigureAwait(false);
        if (updated != 1) throw Conflict("Payment operation lost ownership or the order became paid.");
        ClearCache(data.UniqueId);
    }

    private static Task<OrderData> ReadOrderAsync(DbContext db, Guid orderId, CancellationToken ct)
        => db.GetTable<OrderData>().SingleAsync(x => x.UniqueId == orderId, ct);

    private static async Task<CheckoutPaymentAttemptData?> ReadActiveAsync(DbContext db, Guid orderId, CancellationToken ct)
    {
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleOrDefaultAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false);
        return operation?.ActiveAttemptId is Guid id
            ? await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == id && x.OrderId == orderId, ct).ConfigureAwait(false)
            : null;
    }

    private static Task<int> SetStateAsync(DbContext db, Guid attemptId, CheckoutPaymentAttemptState state, CancellationToken ct)
        => db.GetTable<CheckoutPaymentAttemptData>().Where(x => x.AttemptId == attemptId).Set(x => x.State, state).UpdateAsync(ct);

    private static Task<bool> RequiresReconciliationAsync(DbContext db, Guid orderId, CancellationToken ct)
        => db.GetTable<CheckoutPaymentOperationData>().AnyAsync(x => x.OrderId == orderId && x.ReconciliationRequired, ct);

    private static async Task<bool> HasExpiredAsync(DbContext db, CheckoutPaymentAttemptData attempt, OrderData data, CancellationToken ct)
    {
        if (attempt.ExpiresUtc <= DateTime.UtcNow) return true;
        var ids = attempt.SubmittedOrderInfo == null
            ? await ReadOwnedIdsAsync(db, data, ct).ConfigureAwait(false)
            : JsonConvert.DeserializeObject<List<string>>(attempt.ReservationIds) ?? [];
        if (ids.Count == 0) return false;
        return !await HoldsActiveAsync(db, ids, false, ct).ConfigureAwait(false);
    }

    private static async Task<bool> HoldsActiveAsync(DbContext db, IReadOnlyCollection<string> ids, bool allowConsumed, CancellationToken ct)
    {
        foreach (var id in ids)
        {
            var hold = await db.GetTable<StockReservationData>().SingleOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
            if (hold == null || (hold.State != StockReservationState.Active || hold.ExpiresUtc <= DateTime.UtcNow) &&
                !(allowConsumed && hold.State == StockReservationState.Consumed)) return false;
        }
        return true;
    }

    private static List<string> ReadIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        var value = JObject.Parse(json);
        return (value[nameof(OrderInfo.ReservationIds)] ?? value[nameof(OrderInfo.HangfireJobs)])?.ToObject<List<string>>() ?? [];
    }

    private static async Task<List<string>> ReadOwnedIdsAsync(DbContext db, OrderData data, CancellationToken ct)
    {
        var orderId = data.UniqueId.ToString();
        var owned = await db.GetTable<StockReservationData>()
            .Where(x => x.OrderId == orderId && x.State == StockReservationState.Active)
            .Select(x => x.Id).ToListAsync(ct).ConfigureAwait(false);
        return ReadIds(data.OrderInfo).Concat(owned).Distinct(StringComparer.Ordinal).ToList();
    }

    private static async Task<bool> HasOwnedHoldsAsync(DbContext db, OrderData data, CancellationToken ct)
        => (await ReadOwnedIdsAsync(db, data, ct).ConfigureAwait(false)).Count > 0;

    private static List<Giftcard> ReadGiftcards(string? json)
        => string.IsNullOrWhiteSpace(json) ? []
            : JObject.Parse(json)[nameof(OrderInfo.Giftcards)]?.ToObject<List<Giftcard>>() ?? [];

    private static bool HasClaims(IEnumerable<Giftcard> selections)
        => selections.Any(x => x.Claimed || !string.IsNullOrWhiteSpace(x.ClaimId) ||
            !string.IsNullOrWhiteSpace(x.TransactionId) || x.ClaimDate != null || x.UsedDate != null);

    private static bool HasSettledGiftcards(IEnumerable<Giftcard> selections)
        => selections.Any(x => x.Claimed || !string.IsNullOrWhiteSpace(x.TransactionId) || x.ClaimDate != null || x.UsedDate != null);

    private static bool IsPaid(OrderData data)
        => data.PaidDate != null || data.OrderStatus is OrderStatus.OfflinePayment or OrderStatus.Closed or
            OrderStatus.ReadyForDispatch or OrderStatus.ReadyForDispatchWhenStockArrives or
            OrderStatus.ReadyForPickup or OrderStatus.Dispatched or OrderStatus.Returned;

    private static async Task<bool> IsPaidOrCompletedAsync(DbContext db, OrderData data, CancellationToken ct)
        => IsPaid(data) || await db.GetTable<CheckoutStockCompletionData>()
            .AnyAsync(x => x.OrderId == data.UniqueId, ct).ConfigureAwait(false);

    private static void Apply(IOrderInfo order, OrderData data)
    {
        if (order is not OrderInfo concrete) throw Conflict("Checkout payment operations require a mutable OrderInfo snapshot.");
        // A newly inserted empty cart has no serialized purchase yet; its caller
        // supplies the store snapshot needed for the first mutation.
        if (string.IsNullOrWhiteSpace(data.OrderInfo)) return;
        concrete.ApplyPersistedSnapshot(new OrderInfo(data), data);
    }

    internal static bool SamePurchase(string left, string right)
    {
        static JObject Normalize(string json)
        {
            var value = JObject.Parse(json);
            foreach (var property in value.Properties().ToArray())
                if (property.Name.Equals("OrderStatus", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Equals("OrderStatusCol", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Equals("UpdateDate", StringComparison.OrdinalIgnoreCase) ||
                    property.Name.Equals("PaidDate", StringComparison.OrdinalIgnoreCase)) property.Remove();
            // Fulfillment metadata can change after submission without changing the purchase.
            // Ignore only the properties writable through UpdateOrderLineMetadataAsync.
            if (value[nameof(OrderInfo.OrderLines)] is JArray lines)
            {
                foreach (var line in lines.OfType<JObject>())
                {
                    if (line[nameof(OrderLine.OrderLineInfo)]?.Type == JTokenType.Null)
                    {
                        line.Remove(nameof(OrderLine.OrderLineInfo));
                        continue;
                    }
                    if (line[nameof(OrderLine.OrderLineInfo)] is not JObject info) continue;
                    if (info[nameof(OrderLineInfo.Properties)] is JObject properties)
                    {
                        foreach (var property in properties.Properties().ToArray())
                            if (property.Name.StartsWith("orderline", StringComparison.OrdinalIgnoreCase)) property.Remove();
                        if (!properties.HasValues) info.Remove(nameof(OrderLineInfo.Properties));
                    }
                    else if (info[nameof(OrderLineInfo.Properties)]?.Type == JTokenType.Null)
                    {
                        info.Remove(nameof(OrderLineInfo.Properties));
                    }
                    if (!info.HasValues) line.Remove(nameof(OrderLine.OrderLineInfo));
                }
            }
            return value;
        }
        return JToken.DeepEquals(Normalize(left), Normalize(right));
    }

    private static bool SameOrderData(string? snapshot, OrderData current)
    {
        var submitted = snapshot == null ? null : JsonConvert.DeserializeObject<OrderData>(snapshot);
        return submitted != null && submitted.UniqueId == current.UniqueId &&
            submitted.ReferenceId == current.ReferenceId && submitted.OrderNumber == current.OrderNumber &&
            submitted.TotalAmount == current.TotalAmount && submitted.Currency == current.Currency &&
            submitted.StoreAlias == current.StoreAlias && submitted.CustomerId == current.CustomerId &&
            submitted.CustomerEmail == current.CustomerEmail && submitted.CustomerName == current.CustomerName &&
            submitted.CustomerUsername == current.CustomerUsername && submitted.ShippingCountry == current.ShippingCountry;
    }

    private void ClearCache(Guid orderId)
    {
        _cache.Remove(orderId);
        _cache.Remove(orderId.ToString());
    }

    private async Task LogAsync(Guid orderId, string message, bool required = false)
    {
        try
        {
            await _activity.InsertAsync([new OrderActivityLogWrite(orderId, message, "Customer", DateTime.Now, OrderActivityLogType.Info)], CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to persist payment attempt activity. OrderId: {OrderId}; FailureType: {FailureType}", orderId, ex.GetType().Name);
            if (required) throw;
        }
    }

    private async Task ReconciliationAsync(Guid orderId, Guid? attemptId, string reason, bool persist = true)
    {
        if (persist)
        {
            var scope = CheckoutPaymentOperationScope.Current;
            var previouslyPreserved = scope?.PreserveOwnership ?? false;
            if (scope != null) scope.PreserveOwnership = true;
            await using var db = _factory.GetDatabase();
            if (attemptId is Guid id)
            {
                // Never mark the current/newer attempt when a historical token
                // arrives. CompletionPending remains retriable by the verified
                // matching callback, and completed receipts remain terminal.
                await db.GetTable<CheckoutPaymentAttemptData>()
                    .Where(x => x.OrderId == orderId && x.AttemptId == id &&
                        x.State != CheckoutPaymentAttemptState.Completed && x.State != CheckoutPaymentAttemptState.CompletionPending)
                    .Set(x => x.State, CheckoutPaymentAttemptState.ReconciliationRequired)
                    .UpdateAsync(CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                await db.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == orderId)
                    .Set(x => x.ReconciliationRequired, true).UpdateAsync(CancellationToken.None).ConfigureAwait(false);
            }
            if (scope != null) scope.PreserveOwnership = previouslyPreserved;
        }
        _logger.LogWarning("Payment NEEDS RECONCILIATION. OrderId: {OrderId}; AttemptId: {AttemptId}; Reason: {Reason}", orderId, attemptId, reason);
        await LogAsync(orderId, $"Payment NEEDS RECONCILIATION; attempt={attemptId}; reason={reason}").ConfigureAwait(false);
    }

    private static EkomHttpException Conflict(string message, CheckoutConflictReason? reason = null)
        => reason.HasValue ? new CheckoutConflictException(reason.Value, message) : new EkomHttpException(HttpStatusCode.Conflict, message);
}
