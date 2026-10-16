using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Ekom.Utilities;
using LinqToDB;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Net;

namespace Ekom.Services;

internal sealed partial class CheckoutPaymentAttemptService : IOrderManagerLockService
{
    private sealed record ManagerRecoveryState(OrderData Order, CheckoutPaymentOperationData? Operation,
        CheckoutPreparationData? Preparation, CheckoutPaymentAttemptData? Attempt, List<string> Ids,
        IReadOnlyList<CheckoutPaymentAttemptData> Snapshots, string Token, string? UnavailableReason, bool GiftcardRecoveryRequired);

    private static JObject? ReadManagerRecoveryMetadata(CheckoutPaymentAttemptData attempt)
        => string.IsNullOrWhiteSpace(attempt.SubmittedOrderData) ? null
            : JObject.Parse(attempt.SubmittedOrderData)["ManagerRecovery"] as JObject;

    private static async Task<(List<CheckoutPaymentAttemptData> Snapshots, string? Error)> ReadManagerRecoveryChainAsync(
        DbContext db, Guid orderId, CheckoutPaymentAttemptData? attempt, CancellationToken ct)
    {
        var snapshots = new List<CheckoutPaymentAttemptData>();
        var visited = new HashSet<Guid>();
        while (attempt != null)
        {
            if (attempt.OrderId != orderId || !visited.Add(attempt.AttemptId) || snapshots.Count >= 256)
                return (snapshots, "Checkout history could not be verified. Contact support before unlocking this order.");
            snapshots.Add(attempt);
            var link = ReadManagerRecoveryMetadata(attempt)?["InterruptedAttemptId"];
            if (link == null || link.Type == JTokenType.Null) break;
            if (!Guid.TryParse(link.ToString(), out var previousId))
                return (snapshots, "Checkout history could not be verified. Contact support before unlocking this order.");
            attempt = await db.GetTable<CheckoutPaymentAttemptData>().SingleOrDefaultAsync(x => x.AttemptId == previousId, ct).ConfigureAwait(false);
            if (attempt == null)
                return (snapshots, "Checkout history is missing. Contact support before unlocking this order.");
        }
        return (snapshots, null);
    }

    private static IEnumerable<string> ReadManagerReservationIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var json = JObject.Parse(value);
        return (json[nameof(OrderInfo.ReservationIds)]?.ToObject<List<string>>() ?? [])
            .Concat(json[nameof(OrderInfo.HangfireJobs)]?.ToObject<List<string>>() ?? []);
    }

    private static async Task<ManagerRecoveryState> ReadManagerRecoveryAsync(DbContext db, Guid orderId, CancellationToken ct)
    {
        var data = await ReadOrderAsync(db, orderId, ct).ConfigureAwait(false);
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleOrDefaultAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false);
        var preparation = await db.GetTable<CheckoutPreparationData>().SingleOrDefaultAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false);
        var attempt = operation?.ActiveAttemptId is Guid activeId
            ? await db.GetTable<CheckoutPaymentAttemptData>().SingleOrDefaultAsync(x => x.AttemptId == activeId && x.OrderId == orderId, ct).ConfigureAwait(false)
            : null;
        var chain = await ReadManagerRecoveryChainAsync(db, orderId, attempt, ct).ConfigureAwait(false);
        var currentIds = await ReadOwnedIdsAsync(db, data, ct).ConfigureAwait(false);
        var includeHistory = attempt?.State != CheckoutPaymentAttemptState.Released || operation?.Owner != null ||
            preparation?.Owner != null || operation?.ReconciliationRequired == true || currentIds.Count != 0 || HasClaims(ReadGiftcards(data.OrderInfo));
        List<CheckoutPaymentAttemptData> snapshots = includeHistory ? chain.Snapshots : [];
        var ids = currentIds
            .Concat(ReadManagerReservationIds(data.OrderInfo))
            .Concat(JsonConvert.DeserializeObject<List<string>>(preparation?.ProtectedIds ?? "[]") ?? [])
            .Concat(snapshots.SelectMany(snapshot => JsonConvert.DeserializeObject<List<string>>(snapshot.ReservationIds) ?? []))
            .Concat(snapshots.SelectMany(snapshot => ReadManagerReservationIds(snapshot.SubmittedOrderInfo)))
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var orderKey = orderId.ToString();
        var receipts = await db.GetTable<StockReservationData>()
            .Where(x => x.OrderId == orderKey || ids.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct).ConfigureAwait(false);
        var paid = await IsPaidOrCompletedAsync(db, data, ct).ConfigureAwait(false);
        var giftcardRecovery = await db.GetTable<GiftcardReservationRecoveryData>().Where(x => x.OrderId == orderId)
            .OrderBy(x => x.AttemptId).ToListAsync(ct).ConfigureAwait(false);
        var giftcardRecoveryRequired = giftcardRecovery.Any(x => x.State is GiftcardReservationRecoveryState.Pending or GiftcardReservationRecoveryState.Unresolved);
        var reason = giftcardRecoveryRequired ? "Gift-card reservation effects require dedicated gift-card recovery. Reconcile or release the recorded claims before unlocking checkout." : (operation?.ActiveAttemptId != null && attempt == null
            ? "Checkout history is missing. Contact support before unlocking this order." : chain.Error) ?? (paid || chain.Snapshots.Any(snapshot => snapshot.State == CheckoutPaymentAttemptState.Completed)
            ? "This order has already been paid or completed and cannot be unlocked."
            : receipts.Any(x => x.State == StockReservationState.Consumed)
                ? "Stock has already been used to complete this order. Check the order before unlocking it."
            : receipts.Any(x => x.OrderId != null && !string.Equals(x.OrderId, orderKey, StringComparison.OrdinalIgnoreCase))
                ? "This order refers to stock reserved for another order. Contact support before unlocking it."
            : operation?.Owner == null && preparation?.Owner == null && operation?.ReconciliationRequired != true &&
                (attempt == null || attempt.State == CheckoutPaymentAttemptState.Released) && ids.Count == 0 && !HasClaims(ReadGiftcards(data.OrderInfo))
                ? "This checkout is not locked and has no reservations to release." : null);
        var token = HashManagerValue(JsonConvert.SerializeObject(new { Order = data, Operation = operation,
            Preparation = preparation, Attempts = chain.Snapshots, ReservationIds = ids, Receipts = receipts, Paid = paid, GiftcardRecovery = giftcardRecovery }));
        return new(data, operation, preparation, attempt, ids, snapshots, token, reason, giftcardRecoveryRequired);
    }

    internal static async Task<OrderLockStatus> ReadManagerLockStatusAsync(DbContext db, Guid orderId, CancellationToken ct)
    {
        var state = await ReadManagerRecoveryAsync(db, orderId, ct).ConfigureAwait(false);
        return new OrderLockStatus
        {
            PaymentOperationLocked = state.Operation?.Owner != null,
            PreparationLocked = state.Preparation?.Owner != null,
            ReconciliationRequired = state.GiftcardRecoveryRequired || state.Operation?.ReconciliationRequired == true || state.Attempt?.State == CheckoutPaymentAttemptState.ReconciliationRequired,
            AttemptId = state.Operation?.ActiveAttemptId,
            AttemptState = state.Attempt?.State.ToString(),
            ReleaseToken = state.UnavailableReason == null ? state.Token : null,
            CanRelease = state.UnavailableReason == null,
            ReleaseUnavailableReason = state.UnavailableReason,
        };
    }

    public async Task<OrderLockReleaseResponse> ReleaseOrderLockAsync(Guid orderId, string releaseToken,
        IReadOnlyCollection<string> allowedStoreAliases, string? userName, CancellationToken ct = default)
    {
        var recoveryId = Guid.NewGuid();
        var recoveryOwner = Guid.NewGuid().ToString();
        ManagerRecoveryState state;
        JObject recoverySnapshot;
        await using (var auditDb = _factory.GetDatabase())
        {
            state = await ReadManagerRecoveryAsync(auditDb, orderId, ct).ConfigureAwait(false);
            if (!allowedStoreAliases.Contains(state.Order.StoreAlias, StringComparer.OrdinalIgnoreCase))
                throw new EkomHttpException(HttpStatusCode.Forbidden, "You do not have access to this order's store.");
        }
        await LogManagerReleaseAsync(orderId, $"Manager lock recovery requested; recovery={recoveryId}; interruptedAttempt={state.Attempt?.AttemptId}", userName).ConfigureAwait(false);
        // Compare the exact persisted purchase and both owners while acquiring both fences.
        try
        {
            await using var db = _factory.GetDatabase();
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            state = await ReadManagerRecoveryAsync(db, orderId, ct).ConfigureAwait(false);
            if (!allowedStoreAliases.Contains(state.Order.StoreAlias, StringComparer.OrdinalIgnoreCase))
                throw new EkomHttpException(HttpStatusCode.Forbidden, "You do not have access to this order's store.");
            if (!string.Equals(state.Token, releaseToken, StringComparison.Ordinal))
                throw Conflict("Checkout state changed. Refresh the order and confirm Release Lock again.");
            if (state.UnavailableReason != null) throw Conflict(state.UnavailableReason);
            if (await HasUnresolvedGiftcardRecoveryAsync(db, orderId, ct).ConfigureAwait(false))
                throw Conflict("Use dedicated gift-card recovery before unlocking checkout.");
            if (state.Operation == null)
                await db.InsertAsync(new CheckoutPaymentOperationData { OrderId = orderId, Owner = recoveryOwner, ActiveAttemptId = recoveryId }, token: ct).ConfigureAwait(false);
            else if (await db.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == orderId && x.Owner == state.Operation.Owner)
                .Set(x => x.Owner, recoveryOwner).Set(x => x.ActiveAttemptId, (Guid?)recoveryId).UpdateAsync(ct).ConfigureAwait(false) != 1)
                throw Conflict("Checkout ownership changed. Refresh before recovery.");
            if (state.Preparation == null)
                await db.InsertAsync(new CheckoutPreparationData { OrderId = orderId, Owner = recoveryOwner }, token: ct).ConfigureAwait(false);
            else if (await db.GetTable<CheckoutPreparationData>().Where(x => x.OrderId == orderId && x.Owner == state.Preparation.Owner)
                .Set(x => x.Owner, recoveryOwner).UpdateAsync(ct).ConfigureAwait(false) != 1)
                throw Conflict("Checkout preparation changed. Refresh before recovery.");
            recoverySnapshot = JObject.FromObject(state.Order);
            recoverySnapshot["ManagerRecovery"] = JObject.FromObject(new
            {
                InterruptedAttemptId = state.Attempt?.AttemptId,
                InterruptedAttemptState = state.Attempt?.State.ToString(),
                InterruptedAttemptClosedUtc = state.Attempt?.ClosedUtc,
                PaymentOperationLocked = state.Operation?.Owner != null,
                PreparationLocked = state.Preparation?.Owner != null,
                ReconciliationRequired = state.Operation?.ReconciliationRequired == true,
                ProtectedIds = state.Preparation?.ProtectedIds,
                CurrentClaimsAttemptId = state.Attempt?.State == CheckoutPaymentAttemptState.Preparing && ReadManagerRecoveryMetadata(state.Attempt) == null
                    ? state.Attempt.AttemptId : (Guid?)null,
                Editor = userName,
                Outcome = "Acquired",
            });
            await db.InsertAsync(new CheckoutPaymentAttemptData
            {
                AttemptId = recoveryId, OrderId = orderId, State = CheckoutPaymentAttemptState.Released,
                SubmittedOrderInfo = state.Order.OrderInfo,
                SubmittedOrderData = recoverySnapshot.ToString(Formatting.None),
                ReservationIds = JsonConvert.SerializeObject(state.Ids), CreatedUtc = DateTime.UtcNow, ClosedUtc = DateTime.UtcNow,
            }, token: ct).ConfigureAwait(false);
            if (state.Attempt != null && state.Attempt.State != CheckoutPaymentAttemptState.Released)
                await db.GetTable<CheckoutPaymentAttemptData>().Where(x => x.AttemptId == state.Attempt.AttemptId)
                    .Set(x => x.State, CheckoutPaymentAttemptState.Released).Set(x => x.ClosedUtc, (DateTime?)DateTime.UtcNow).UpdateAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await LogManagerReleaseAsync(orderId, $"Manager lock recovery blocked or acquisition uncertain; recovery={recoveryId}; interruptedAttempt={state.Attempt?.AttemptId}; outcome=BlockedOrAcquisitionUnconfirmed", userName).ConfigureAwait(false);
            throw;
        }

        // Never abandon a fenced recovery because the editor closed the browser.
        string? warning = null;
        try
        {
            await using var db = _factory.GetDatabase();
            foreach (var id in state.Ids)
            {
                var row = await db.GetTable<StockReservationData>().SingleOrDefaultAsync(x => x.Id == id, CancellationToken.None).ConfigureAwait(false);
                if (row?.OrderId != null && !string.Equals(row.OrderId, orderId.ToString(), StringComparison.OrdinalIgnoreCase))
                    throw Conflict("This order refers to stock reserved for another order. Contact support before trying again.");
                var result = await _stock.ReleaseAsync(id, CancellationToken.None).ConfigureAwait(false);
                if (result.Status is not (StockReservationStatus.Released or StockReservationStatus.AlreadyReleased or StockReservationStatus.AlreadyExpired or StockReservationStatus.Expired or StockReservationStatus.NotFound))
                    throw Conflict("Stock release could not be confirmed. Check the order before trying again.");
            }
            var current = ReadGiftcards(state.Order.OrderInfo);
            // Original frozen snapshots carry the adapter's payment-attempt identity. Recovery
            // snapshots only archive evidence and must never introduce a new provider identity.
            var originals = state.Snapshots.Where(snapshot => ReadManagerRecoveryMetadata(snapshot) == null).ToArray();
            var knownClaims = originals.SelectMany(snapshot => ReadGiftcards(snapshot.SubmittedOrderInfo)
                .Select(card => (Card: card, AttemptId: snapshot.AttemptId))).ToList();
            foreach (var snapshot in state.Snapshots)
            {
                var association = ReadManagerRecoveryMetadata(snapshot)?["CurrentClaimsAttemptId"];
                if (Guid.TryParse(association?.ToString(), out var associatedId) && originals.Any(original => original.AttemptId == associatedId))
                    knownClaims.AddRange(ReadGiftcards(snapshot.SubmittedOrderInfo).Select(card => (Card: card, AttemptId: associatedId)));
            }
            var archivedCards = state.Snapshots.SelectMany(snapshot => ReadGiftcards(snapshot.SubmittedOrderInfo)).ToArray();
            var allCards = archivedCards.Concat(current).ToArray();
            var hasUsedCards = HasSettledGiftcards(allCards);
            const string externalWarning = "Checkout unlocked. Gift-card release could not be confirmed. Check the gift-card provider before the customer tries again.";
            const string usedWarning = "Used gift cards were not refunded. Check the gift-card provider before the customer tries again.";
            if (hasUsedCards) warning = "Checkout unlocked. " + usedWarning;
            var claims = knownClaims.Concat(allCards.Select(card =>
                {
                    var known = knownClaims.FirstOrDefault(claim => claim.Card.Code == card.Code && claim.Card.ClaimId == card.ClaimId);
                    var attemptId = known.Card != null ? known.AttemptId
                        : state.Attempt?.State == CheckoutPaymentAttemptState.Preparing && ReadManagerRecoveryMetadata(state.Attempt) == null
                            ? state.Attempt.AttemptId : Guid.Empty;
                    return (Card: card, AttemptId: attemptId);
                }))
                .Where(x => !string.IsNullOrWhiteSpace(x.Card.ClaimId) &&
                    !allCards.Any(card => card.Code == x.Card.Code && card.ClaimId == x.Card.ClaimId && HasSettledGiftcards([card])))
                .DistinctBy(x => (x.Card.Code, x.Card.ClaimId)).ToArray();
            foreach (var claim in claims)
            {
                if (_giftcards == null)
                {
                    warning = externalWarning + (hasUsedCards ? " " + usedWarning : string.Empty);
                    break;
                }
                try { await _giftcards.ReleaseAsync(orderId, claim.AttemptId, [claim.Card], CancellationToken.None).ConfigureAwait(false); }
                catch (Exception)
                {
                    warning = externalWarning + (hasUsedCards ? " " + usedWarning : string.Empty);
                }
            }
            var json = JObject.Parse(state.Order.OrderInfo);
            json[nameof(OrderInfo.ReservationIds)] = new JArray();
            json[nameof(OrderInfo.HangfireJobs)] = new JArray();
            foreach (var card in json[nameof(OrderInfo.Giftcards)]?.OfType<JObject>() ?? [])
            {
                card[nameof(Giftcard.Claimed)] = false;
                foreach (var field in new[] { nameof(Giftcard.ClaimId), nameof(Giftcard.ClaimDate), nameof(Giftcard.TransactionId), nameof(Giftcard.UsedDate), "HoldValidUntil", "holdValidUntil" })
                    if (card.Property(field, StringComparison.Ordinal) != null) card[field] = null;
            }
            state.Order.OrderInfo = json.ToString(Formatting.None);
            state.Order.OrderStatus = OrderStatus.Incomplete;
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None).ConfigureAwait(false);
            if (await HasUnresolvedGiftcardRecoveryAsync(db, orderId, CancellationToken.None).ConfigureAwait(false))
                throw Conflict("Gift-card reservation evidence now requires dedicated recovery. Checkout remains locked.");
            var scope = new CheckoutPaymentOperationScope(orderId, recoveryOwner, null);
            await SaveOrderAsync(db, scope, state.Order, CancellationToken.None).ConfigureAwait(false);
            recoverySnapshot["ManagerRecovery"]!["Outcome"] = warning == null ? "Released" : "LocalResetGiftcardsRequireReview";
            recoverySnapshot["ManagerRecovery"]!["Warning"] = warning;
            await db.GetTable<CheckoutPaymentAttemptData>().Where(x => x.OrderId == orderId && x.AttemptId == recoveryId)
                .Set(x => x.SubmittedOrderData, recoverySnapshot.ToString(Formatting.None))
                .UpdateAsync(CancellationToken.None).ConfigureAwait(false);
            await db.InsertAsync(new OrderActivityLog
            {
                UniqueID = Guid.NewGuid(),
                Key = orderId,
                Log = $"Manager lock recovery completed; recovery={recoveryId}; interruptedAttempt={state.Attempt?.AttemptId}; outcome={(warning == null ? "Released" : "LocalResetGiftcardsRequireReview")}",
                UserName = string.IsNullOrWhiteSpace(userName) ? "Manager" : userName,
                Date = DateTime.Now,
                LogType = OrderActivityLogType.Info,
            }, token: CancellationToken.None).ConfigureAwait(false);
            if (await db.GetTable<CheckoutPreparationData>().Where(x => x.OrderId == orderId && x.Owner == recoveryOwner)
                .Set(x => x.ProtectedIds, "[]").Set(x => x.Owner, (string?)null).UpdateAsync(CancellationToken.None).ConfigureAwait(false) != 1)
                throw Conflict("Checkout changed while unlocking it. Refresh the order and check before trying again.");
            if (await db.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == orderId && x.Owner == recoveryOwner && x.ActiveAttemptId == recoveryId)
                .Set(x => x.ReconciliationRequired, false).Set(x => x.Owner, (string?)null).UpdateAsync(CancellationToken.None).ConfigureAwait(false) != 1)
                throw Conflict("Checkout changed while unlocking it. Refresh the order and check before trying again.");
            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            ClearCache(orderId);
        }
        catch (Exception)
        {
            await LogManagerReleaseAsync(orderId, $"Manager lock recovery failed or acknowledgement uncertain; recovery={recoveryId}; interruptedAttempt={state.Attempt?.AttemptId}; outcome=Unconfirmed", userName).ConfigureAwait(false);
            throw;
        }
        return new("Checkout unlocked. The customer can try again.", warning);
    }
}
