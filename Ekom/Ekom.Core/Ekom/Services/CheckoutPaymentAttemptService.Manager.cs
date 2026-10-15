using Ekom.Models;
using Ekom.Repositories;
using LinqToDB;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ekom.Services;

internal sealed partial class CheckoutPaymentAttemptService
{
    internal sealed record ManagerReleaseState(string Token, bool HasGiftcardClaims, bool HasCheckoutReservations,
        bool HasActiveAttempt, bool CanRelease);

    internal async Task<ManagerReleaseState?> GetManagerReleaseStateAsync(Guid orderId, CancellationToken ct)
    {
        await using var db = _factory.GetDatabase();
        var data = await ReadOrderAsync(db, orderId, ct).ConfigureAwait(false);
        var attempt = await ReadActiveAsync(db, orderId, ct).ConfigureAwait(false);
        return await ReadManagerReleaseStateAsync(db, data, attempt, ct).ConfigureAwait(false);
    }

    private async Task<ManagerReleaseState?> ReadManagerReleaseStateAsync(
        DbContext db, OrderData data, CheckoutPaymentAttemptData? attempt, CancellationToken ct)
    {
        if (await IsPaidOrCompletedAsync(db, data, ct).ConfigureAwait(false) ||
            await RequiresReconciliationAsync(db, data.UniqueId, ct).ConfigureAwait(false) ||
            attempt?.State is CheckoutPaymentAttemptState.Completed or CheckoutPaymentAttemptState.CompletionPending or
                CheckoutPaymentAttemptState.ReconciliationRequired)
            return null;

        var active = attempt?.State != CheckoutPaymentAttemptState.Released ? attempt : null;
        var currentGiftcards = ReadGiftcards(data.OrderInfo);
        var submittedGiftcards = ReadGiftcards(active?.SubmittedOrderInfo);
        if (HasSettledGiftcards(currentGiftcards) || HasSettledGiftcards(submittedGiftcards))
            return null;

        var ids = await ReadOwnedIdsAsync(db, data, ct).ConfigureAwait(false);
        var hasClaims = HasClaims(currentGiftcards) || HasClaims(submittedGiftcards);
        // Bind confirmations to the saved purchase and attempt. A stale Manager page must not
        // cancel a newer payment or release a different set of external claims.
        var snapshot = JsonConvert.SerializeObject(new
        {
            data.UniqueId,
            data.StoreAlias,
            data.OrderInfo,
            AttemptId = attempt?.AttemptId,
            SubmittedOrderInfo = active?.SubmittedOrderInfo,
            ReservationIds = ids.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        });
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
        return new ManagerReleaseState(token, hasClaims, active != null || ids.Count > 0 || hasClaims,
            active != null, !hasClaims || _giftcards != null);
    }

    internal async Task<bool> ReleaseForManagerAsync(Guid orderId, string storeAlias, string token,
        bool giftcardsOnly, string? userName, CancellationToken ct)
    {
        var action = giftcardsOnly ? "giftcard reservations" : "checkout reservations";
        await LogManagerReleaseAsync(orderId, $"Manual release requested; target={action}", userName).ConfigureAwait(false);
        try
        {
            using var suppression = CheckoutPaymentOperationScope.Suppress();
            await using var scope = await AcquireAsync(orderId, ct).ConfigureAwait(false);
            using var activation = scope.Enter();
            await using var db = _factory.GetDatabase();
            var data = await ReadOrderAsync(db, orderId, ct).ConfigureAwait(false);
            if (!string.Equals(data.StoreAlias, storeAlias, StringComparison.Ordinal))
                throw Conflict("The order's store changed. Refresh the order before releasing reservations.");
            var attempt = await ReadActiveAsync(db, orderId, ct).ConfigureAwait(false);
            var state = await ReadManagerReleaseStateAsync(db, data, attempt, ct).ConfigureAwait(false);
            if (state == null)
                throw Conflict("Paid, completing, redeemed, or reconciliation-required orders cannot release reservations.", CheckoutConflictReason.PaymentReview);
            if (!state.HasCheckoutReservations || giftcardsOnly && !state.HasGiftcardClaims)
            {
                await LogManagerReleaseAsync(orderId, $"Manual release completed; target={action}; outcome=AlreadyReleased", userName).ConfigureAwait(false);
                return false;
            }
            if (!string.Equals(state.Token, token, StringComparison.Ordinal))
                throw Conflict("Reservations changed since this action was loaded. Refresh the order and confirm again.");
            if (!state.CanRelease)
                throw Conflict("The gift-card reservation integration is not configured. No reservations were released.");

            if (attempt?.SubmittedOrderInfo != null && attempt.State != CheckoutPaymentAttemptState.Released)
            {
                var currentClaims = ReadGiftcards(data.OrderInfo).Where(card => HasClaims([card]))
                    .Select(card => (card.Code, card.ClaimId)).ToHashSet();
                var submittedClaims = ReadGiftcards(attempt.SubmittedOrderInfo).Where(card => HasClaims([card]))
                    .Select(card => (card.Code, card.ClaimId)).ToHashSet();
                if (!currentClaims.SetEquals(submittedClaims))
                    throw Conflict("Gift-card claims differ from the submitted payment. Reconciliation is required before release.", CheckoutConflictReason.PaymentReview);
                var submittedIds = (JsonConvert.DeserializeObject<List<string>>(attempt.ReservationIds) ?? []).ToHashSet(StringComparer.Ordinal);
                var ownedIds = await ReadOwnedIdsAsync(db, data, ct).ConfigureAwait(false);
                if (ownedIds.Any(id => !submittedIds.Contains(id)))
                    throw Conflict("Stock reservations exist outside the submitted payment. Reconciliation is required before release.", CheckoutConflictReason.PaymentReview);
            }

            // Once external release starts, finish persisting the result even if the browser disconnects.
            if (!giftcardsOnly || state.HasActiveAttempt)
            {
                await ReleaseOwnedAsync(db, scope,
                    attempt?.State == CheckoutPaymentAttemptState.Released ? null : attempt,
                    data, "Manual Manager reservation release", CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                var selections = ReadGiftcards(data.OrderInfo);
                // Preserve stock and order state for standalone legacy giftcard holds. The adapter
                // is idempotent, so an external failure leaves metadata intact for a safe retry.
                await _giftcards!.ReleaseAsync(orderId, Guid.Empty, selections, CancellationToken.None).ConfigureAwait(false);
                var json = JObject.Parse(data.OrderInfo);
                foreach (var giftcard in json[nameof(OrderInfo.Giftcards)]?.OfType<JObject>() ?? [])
                {
                    giftcard[nameof(Giftcard.Claimed)] = false;
                    foreach (var field in new[] { nameof(Giftcard.ClaimId), nameof(Giftcard.ClaimDate), nameof(Giftcard.TransactionId), nameof(Giftcard.UsedDate), "HoldValidUntil", "holdValidUntil" })
                        if (giftcard.Property(field, StringComparison.Ordinal) != null) giftcard[field] = null;
                }
                data.OrderInfo = json.ToString(Formatting.None);
                scope.PreserveOwnership = true;
                await SaveOrderAsync(db, scope, data, CancellationToken.None).ConfigureAwait(false);
                scope.PreserveOwnership = false;
            }
            await LogManagerReleaseAsync(orderId, $"Manual release completed; target={action}; outcome=Released", userName).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            await LogManagerReleaseAsync(orderId, $"Manual release failed or blocked; target={action}; FailureType={ex.GetType().Name}", userName).ConfigureAwait(false);
            throw;
        }
    }

    private Task LogManagerReleaseAsync(Guid orderId, string message, string? userName)
        => _activity.InsertAsync([new OrderActivityLogWrite(orderId, message,
            string.IsNullOrWhiteSpace(userName) ? "Manager" : userName, DateTime.Now, OrderActivityLogType.Info)], CancellationToken.None);
}
