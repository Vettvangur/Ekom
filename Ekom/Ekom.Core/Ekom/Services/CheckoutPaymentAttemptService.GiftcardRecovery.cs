using Ekom.Models;
using Ekom.Repositories;
using LinqToDB;
using Newtonsoft.Json;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace Ekom.Services;

internal sealed partial class CheckoutPaymentAttemptService
{
    private static Task<bool> HasUnresolvedGiftcardRecoveryAsync(DbContext db, Guid orderId, CancellationToken ct = default)
        => db.GetTable<GiftcardReservationRecoveryData>().AnyAsync(x => x.OrderId == orderId &&
            (x.State == GiftcardReservationRecoveryState.Pending || x.State == GiftcardReservationRecoveryState.Unresolved), ct);

    private static GiftcardReservationFailure ReadGiftcardRecoveryEvidence(GiftcardReservationRecoveryData recovery)
    {
        try
        {
            if (recovery.EvidenceJson.Length > 131072) return new GiftcardReservationFailure { OrderId = recovery.OrderId, AttemptId = recovery.AttemptId };
            var evidence = JsonConvert.DeserializeObject<GiftcardReservationFailure>(recovery.EvidenceJson);
            if (evidence != null && evidence.OrderId == recovery.OrderId && evidence.AttemptId == recovery.AttemptId &&
                evidence.Claims != null && evidence.Claims.Count <= 256 && evidence.Claims.All(x => x != null) &&
                Enum.IsDefined(evidence.Outcome) && evidence.Claims.All(x => Enum.IsDefined(x.Status))) return evidence;
        }
        catch (JsonException) { }
        return new GiftcardReservationFailure { OrderId = recovery.OrderId, AttemptId = recovery.AttemptId };
    }

    private static bool IsGiftcardRecoveryResolved(GiftcardReservationFailure evidence)
    {
        if (!evidence.AllEffectsAccountedFor || evidence.Claims == null) return false;
        if (evidence.Outcome == GiftcardReservationFailureOutcome.NoEffects) return evidence.Claims.Count == 0;
        return evidence.Outcome == GiftcardReservationFailureOutcome.Compensated && evidence.Claims.Count > 0 &&
            evidence.Claims.All(x => x != null && x.NewlyAcquired && x.Status == GiftcardRecoveryClaimStatus.Released &&
                !string.IsNullOrWhiteSpace(x.Provider) && !string.IsNullOrWhiteSpace(x.Account) &&
                !string.IsNullOrWhiteSpace(x.ClaimId) && !string.IsNullOrWhiteSpace(x.CardCode));
    }

    private static string GiftcardRecoveryClaimKey(GiftcardRecoveryClaim claim)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(
            new[] { claim.Provider, claim.Account, claim.ClaimId, claim.CardCode }))));

    // Never releases external holds or rewrites the basket. Only independently empty
    // stock/preparation ownership permits a failed invocation to close automatically.
    private async Task<bool> CompleteFailedGiftcardRecoveryAsync(DbContext db, CheckoutPaymentOperationScope scope,
        GiftcardReservationRecoveryData recovery, CancellationToken ct = default)
    {
        scope.PreserveOwnership = true;
        if (scope.OrderId != recovery.OrderId || !IsGiftcardRecoveryResolved(ReadGiftcardRecoveryEvidence(recovery))) return false;
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleAsync(x => x.OrderId == scope.OrderId, ct).ConfigureAwait(false);
        var active = await ReadActiveAsync(db, scope.OrderId, ct).ConfigureAwait(false);
        if (operation.Owner != scope.Owner || operation.ReconciliationRequired || operation.ActiveAttemptId != recovery.AttemptId ||
            active?.State != CheckoutPaymentAttemptState.Preparing || active.SubmittedUtc != null || active.SubmittedOrderInfo != null ||
            active.SubmittedOrderData != null) return false;
        var current = await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync(x => x.AttemptId == recovery.AttemptId, ct).ConfigureAwait(false);
        if (current.Revision != recovery.Revision || current.Owner != recovery.Owner ||
            current.State is not (GiftcardReservationRecoveryState.Pending or GiftcardReservationRecoveryState.Unresolved) ||
            !IsGiftcardRecoveryResolved(ReadGiftcardRecoveryEvidence(current))) return false;
        async Task<bool> RetainAsync(string reason)
        {
            await UpdateGiftcardRecoveryAsync(db, scope, recovery, GiftcardReservationRecoveryState.Unresolved,
                recovery.EvidenceJson, reason, ct).ConfigureAwait(false);
            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            return false;
        }
        if (await db.GetTable<GiftcardReservationRecoveryData>().AnyAsync(x => x.OrderId == scope.OrderId && x.AttemptId != recovery.AttemptId &&
            (x.State == GiftcardReservationRecoveryState.Pending || x.State == GiftcardReservationRecoveryState.Unresolved), ct).ConfigureAwait(false))
            return await RetainAsync("Giftcard effects terminal, but another reservation invocation remains unresolved").ConfigureAwait(false);
        var data = await ReadOrderAsync(db, scope.OrderId, ct).ConfigureAwait(false);
        if (await IsPaidOrCompletedAsync(db, data, ct).ConfigureAwait(false) ||
            HasSettledGiftcards(ReadGiftcards(data.OrderInfo)) || await HasOwnedHoldsAsync(db, data, ct).ConfigureAwait(false))
            return await RetainAsync("Giftcard effects terminal, but payment or stock ownership prevents safe closure").ConfigureAwait(false);
        var preparation = await db.GetTable<CheckoutPreparationData>().SingleOrDefaultAsync(x => x.OrderId == scope.OrderId, ct).ConfigureAwait(false);
        if (preparation != null && (preparation.Owner != null && preparation.Owner != recovery.PreparationOwner ||
            (JsonConvert.DeserializeObject<List<string>>(preparation.ProtectedIds) ?? []).Count != 0))
            return await RetainAsync("Giftcard effects terminal, but preparation ownership or protected stock prevents safe closure").ConfigureAwait(false);
        if (preparation?.Owner != null)
        {
            var cleared = await db.GetTable<CheckoutPreparationData>().Where(x => x.OrderId == scope.OrderId && x.Owner == recovery.PreparationOwner)
                .Set(x => x.Owner, (string?)null).UpdateAsync(ct).ConfigureAwait(false);
            if (cleared != 1) throw Conflict("Checkout preparation ownership changed.");
        }
        await UpdateGiftcardRecoveryAsync(db, scope, recovery, GiftcardReservationRecoveryState.Resolved,
            recovery.EvidenceJson, "Failed reservation effects verified terminal; no stock ownership remains", ct).ConfigureAwait(false);
        var closed = await db.GetTable<CheckoutPaymentAttemptData>().Where(x => x.OrderId == scope.OrderId && x.AttemptId == recovery.AttemptId &&
            x.State == CheckoutPaymentAttemptState.Preparing && x.SubmittedUtc == null)
            .Set(x => x.State, CheckoutPaymentAttemptState.Released).Set(x => x.ClosedUtc, (DateTime?)DateTime.UtcNow).UpdateAsync(ct).ConfigureAwait(false);
        if (closed != 1) throw Conflict("Payment attempt changed during recovery.");
        var changed = await db.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == scope.OrderId && x.Owner == scope.Owner && x.ActiveAttemptId == recovery.AttemptId)
            .Set(x => x.Owner, (string?)null).UpdateAsync(ct).ConfigureAwait(false);
        if (changed != 1) throw Conflict("Checkout ownership changed during recovery.");
        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        // Keep the capability identity: disposal must not run broad claim compensation.
        scope.PreserveOwnership = true;
        return true;
    }

    private static async Task UpdateGiftcardRecoveryAsync(DbContext db, CheckoutPaymentOperationScope scope,
        GiftcardReservationRecoveryData recovery, GiftcardReservationRecoveryState state, string evidence, string reason, CancellationToken ct)
    {
        var revision = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var changed = await db.GetTable<GiftcardReservationRecoveryData>()
            .Where(x => x.AttemptId == recovery.AttemptId && x.OrderId == scope.OrderId && x.Owner == recovery.Owner && x.Revision == recovery.Revision &&
                db.GetTable<CheckoutPaymentOperationData>().Any(o => o.OrderId == scope.OrderId && o.Owner == scope.Owner && o.ActiveAttemptId == x.AttemptId) &&
                db.GetTable<CheckoutPaymentAttemptData>().Any(a => a.AttemptId == x.AttemptId && a.OrderId == scope.OrderId && a.State == CheckoutPaymentAttemptState.Preparing && a.SubmittedUtc == null))
            .Set(x => x.State, state).Set(x => x.EvidenceJson, evidence).Set(x => x.Reason, reason)
            .Set(x => x.Revision, revision).Set(x => x.UpdatedUtc, now).UpdateAsync(ct).ConfigureAwait(false);
        if (changed != 1) throw Conflict("Giftcard recovery ownership or revision changed.");
        recovery.State = state;
        recovery.EvidenceJson = evidence;
        recovery.Reason = reason;
        recovery.Revision = revision;
        recovery.UpdatedUtc = now;
    }

    private static string CaptureGiftcardFailure(GiftcardReservationRecoveryData recovery, GiftcardReservationFailure failure)
    {
        // Retain bounded diagnostic evidence, but never let a mismatched receipt
        // provide actionable ownership. ReadGiftcardRecoveryEvidence fails closed.
        var json = JsonConvert.SerializeObject(failure);
        return json.Length <= 131072 ? json : JsonConvert.SerializeObject(new
        {
            recovery.OrderId, recovery.AttemptId, Outcome = GiftcardReservationFailureOutcome.Unknown,
            AllEffectsAccountedFor = false, Claims = Array.Empty<GiftcardRecoveryClaim>(),
            TruncatedEvidence = json[..65536],
        });
    }

    private static GiftcardReservationFailure ReturnedGiftcardEvidence(Guid orderId, Guid attemptId, IReadOnlyList<Giftcard> renewed)
        => new()
        {
            OrderId = orderId, AttemptId = attemptId, Outcome = GiftcardReservationFailureOutcome.Unknown,
            // The v1 response has no provider/account or provenance guarantee. It is
            // useful evidence, never authorization for automatic external release.
            Claims = renewed.Take(256).Select(x => new GiftcardRecoveryClaim
            {
                CardCode = x.Code, ClaimId = x.ClaimId ?? string.Empty, RequestedAmount = x.Amount,
                RawReturnedAmount = x.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                RawExpiry = x.ValidUntil?.ToString("O"), Status = IsSettledClaim(x)
                    ? GiftcardRecoveryClaimStatus.Settled : GiftcardRecoveryClaimStatus.Unknown,
            }).ToArray(),
        };
}
