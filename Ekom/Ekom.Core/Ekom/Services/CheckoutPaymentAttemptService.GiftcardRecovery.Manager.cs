using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Ekom.Utilities;
using LinqToDB;
using Newtonsoft.Json;
using System.Data;
using System.Net;

namespace Ekom.Services;

internal sealed partial class CheckoutPaymentAttemptService : IOrderManagerGiftcardRecoveryService
{
    private const string GiftcardRecoveryRunning = "Manager gift-card recovery in progress";

    private sealed record GiftcardManagerState(ManagerRecoveryState Checkout,
        IReadOnlyList<GiftcardReservationRecoveryData> Rows, bool Paid, string Token);

    private static async Task<GiftcardManagerState> ReadGiftcardManagerStateAsync(DbContext db, Guid orderId, CancellationToken ct)
    {
        var checkout = await ReadManagerRecoveryAsync(db, orderId, ct).ConfigureAwait(false);
        var rows = await db.GetTable<GiftcardReservationRecoveryData>().Where(x => x.OrderId == orderId)
            .OrderBy(x => x.CreatedUtc).ThenBy(x => x.AttemptId).ToListAsync(ct).ConfigureAwait(false);
        var paid = await IsPaidOrCompletedAsync(db, checkout.Order, ct).ConfigureAwait(false) ||
            HasSettledGiftcards(ReadGiftcards(checkout.Order.OrderInfo));
        return new(checkout, rows, paid, HashManagerValue(JsonConvert.SerializeObject(new { checkout.Token, Rows = rows, Paid = paid })));
    }

    private static void AuthorizeGiftcardRecovery(GiftcardManagerState state, IReadOnlyCollection<string> allowedStores)
    {
        if (!allowedStores.Contains(state.Checkout.Order.StoreAlias, StringComparer.OrdinalIgnoreCase))
            throw new Ekom.Exceptions.EkomHttpException(HttpStatusCode.Forbidden, "You do not have access to this order's store.");
    }

    private string? GiftcardRecoveryUnavailable(GiftcardManagerState state, GiftcardReservationRecoveryData row)
    {
        if (_giftcards is not ICheckoutGiftcardReservationRecovery)
            return "The gift-card integration does not support reservation recovery.";
        if (row.State is not (GiftcardReservationRecoveryState.Pending or GiftcardReservationRecoveryState.Unresolved))
            return "This evidence is historical and does not require recovery.";
        if (row.Reason == GiftcardRecoveryRunning)
            return "Recovery is in progress. Refresh before taking another action. If it remains interrupted, contact support.";
        var checkout = state.Checkout;
        if (state.Paid || checkout.Operation?.ReconciliationRequired == true || checkout.Attempt?.State != CheckoutPaymentAttemptState.Preparing ||
            checkout.Operation?.ActiveAttemptId != row.AttemptId || checkout.Attempt.SubmittedUtc != null ||
            checkout.Attempt.SubmittedOrderInfo != null || checkout.Attempt.SubmittedOrderData != null)
            return "Only the same active, unsubmitted and unpaid preparation attempt can be recovered here.";
        if (checkout.Operation.Owner != null && checkout.Operation.Owner != row.Owner ||
            checkout.Preparation?.Owner != null && checkout.Preparation.Owner != row.PreparationOwner)
            return "Checkout ownership changed. Contact support before recovering these effects.";
        return null;
    }

    private static bool IsReleasableRecoveryClaim(GiftcardRecoveryClaim claim)
        => claim.NewlyAcquired && claim.Status is GiftcardRecoveryClaimStatus.Active or GiftcardRecoveryClaimStatus.ReleaseUnconfirmed &&
            !string.IsNullOrWhiteSpace(claim.Provider) && !string.IsNullOrWhiteSpace(claim.Account) &&
            !string.IsNullOrWhiteSpace(claim.ClaimId) && !string.IsNullOrWhiteSpace(claim.CardCode);

    private static string GiftcardActionToken(GiftcardManagerState state, GiftcardReservationRecoveryData row, string action)
        => HashManagerValue(state.Token + ":" + row.AttemptId + ":" + row.Revision + ":" + action);

    private static string MaskRecoveryReference(string? value)
        => string.IsNullOrEmpty(value) ? string.Empty : "****" + value[^Math.Min(4, value.Length)..];

    private static string? SafeRecoveryText(string? value)
        => value == null ? null : new string(value.Take(128).Select(c => char.IsControl(c) || c is '<' or '>' ? ' ' : c).ToArray());

    private static string? SafeRecoveryDiagnostic(string? value, IReadOnlyList<GiftcardRecoveryClaim> claims)
    {
        if (value == null) return null;
        // Malformed scalar values are evidence too. Redact before truncating so that
        // a boundary cannot expose a prefix of a known sensitive reference.
        foreach (var reference in claims.SelectMany(x => new[] { x.CardCode, x.ClaimId, x.Account, x.ProviderRequestId })
            .Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal).OrderByDescending(x => x!.Length))
            value = value.Replace(reference!, "[redacted]", StringComparison.Ordinal);
        return WebUtility.HtmlEncode(new string(value.Take(128).Select(c => char.IsControl(c) ? ' ' : c).ToArray()));
    }

    public async Task<IReadOnlyList<GiftcardRecoveryView>> GetGiftcardRecoveryAsync(Guid orderId,
        IReadOnlyCollection<string> allowedStoreAliases, CancellationToken ct = default)
    {
        await using var db = _factory.GetDatabase();
        var state = await ReadGiftcardManagerStateAsync(db, orderId, ct).ConfigureAwait(false);
        AuthorizeGiftcardRecovery(state, allowedStoreAliases);
        return state.Rows.Where(row => row.State != GiftcardReservationRecoveryState.Accepted).Select(row =>
        {
            var evidence = ReadGiftcardRecoveryEvidence(row);
            var reason = GiftcardRecoveryUnavailable(state, row);
            return new GiftcardRecoveryView
            {
                AttemptId = row.AttemptId,
                CreatedUtc = row.CreatedUtc,
                State = row.State.ToString(),
                Reason = row.State == GiftcardReservationRecoveryState.Resolved ? "Recorded reservation effects are resolved." : "Reservation preparation did not complete. Provider effects require verification.",
                HasUnknownEffects = !evidence.AllEffectsAccountedFor || evidence.Outcome == GiftcardReservationFailureOutcome.Unknown,
                CanReconcile = reason == null,
                ReconcileToken = reason == null ? GiftcardActionToken(state, row, "reconcile") : null,
                UnavailableReason = reason,
                Claims = evidence.Claims.Select(claim =>
                {
                    var claimReason = reason ?? (row.State != GiftcardReservationRecoveryState.Unresolved
                        ? "The reservation request must first be reconciled as terminal."
                        : !IsReleasableRecoveryClaim(claim) ? "This is not a proven newly acquired, unsettled claim awaiting cleanup." : null);
                    var id = GiftcardRecoveryClaimKey(claim);
                    return new GiftcardRecoveryClaimView
                    {
                        Id = id, CardReference = MaskRecoveryReference(claim.CardCode), ClaimReference = MaskRecoveryReference(claim.ClaimId),
                        Provider = SafeRecoveryText(claim.Provider) ?? string.Empty, Account = MaskRecoveryReference(claim.Account),
                        RequestedAmount = claim.RequestedAmount, Currency = SafeRecoveryText(claim.Currency) ?? string.Empty,
                        RawReturnedAmount = SafeRecoveryDiagnostic(claim.RawReturnedAmount, evidence.Claims), ReturnedAmountType = SafeRecoveryDiagnostic(claim.ReturnedAmountType, evidence.Claims),
                        RawExpiry = SafeRecoveryDiagnostic(claim.RawExpiry, evidence.Claims), CleanupState = claim.Status.ToString(),
                        CanRelease = claimReason == null, UnavailableReason = claimReason,
                        ReleaseToken = claimReason == null ? GiftcardActionToken(state, row, "release:" + id) : null,
                    };
                }).ToArray(),
            };
        }).ToArray();
    }

    // Status alone may advance; provider ownership, returned metadata and request identity may not.
    private static bool SameRecoveryClaim(GiftcardRecoveryClaim left, GiftcardRecoveryClaim right)
        => (left with { Status = right.Status }) == right;

    // Reconciliation alone may enrich v1 diagnostics. The recovery adapter must prove
    // ownership from the original order/attempt request at the provider; matching a
    // card or amount (including a card lookup) is never sufficient authorization.
    private static GiftcardRecoveryClaim PromoteVerifiedRecoveryDiagnostic(GiftcardRecoveryClaim diagnostic, GiftcardRecoveryClaim verified)
    {
        if (!string.IsNullOrEmpty(diagnostic.Provider) || !string.IsNullOrEmpty(diagnostic.Account) || diagnostic.NewlyAcquired ||
            string.IsNullOrWhiteSpace(diagnostic.CardCode) || string.IsNullOrWhiteSpace(diagnostic.ClaimId) ||
            diagnostic.CardCode != verified.CardCode || diagnostic.ClaimId != verified.ClaimId ||
            string.IsNullOrWhiteSpace(verified.Provider) || string.IsNullOrWhiteSpace(verified.Account) || !verified.NewlyAcquired)
            throw Conflict("The provider did not prove ownership of the original diagnostic claim. Contact support.");
        var enriched = diagnostic with
        {
            Provider = verified.Provider,
            Account = verified.Account,
            NewlyAcquired = true,
            Currency = string.IsNullOrEmpty(diagnostic.Currency) ? verified.Currency : diagnostic.Currency,
            ProviderRequestId = string.IsNullOrEmpty(diagnostic.ProviderRequestId) ? verified.ProviderRequestId : diagnostic.ProviderRequestId,
            Status = verified.Status,
        };
        if (enriched != verified || diagnostic.Status == GiftcardRecoveryClaimStatus.Settled && verified.Status != GiftcardRecoveryClaimStatus.Settled)
            throw Conflict("The provider changed the original diagnostic claim's metadata or settled status. Contact support.");
        return diagnostic.Status == GiftcardRecoveryClaimStatus.Released ? verified with { Status = GiftcardRecoveryClaimStatus.Released } : verified;
    }

    private static GiftcardReservationFailure MergeReconciledEvidence(GiftcardReservationFailure prior, GiftcardReservationFailure result)
    {
        if (result.OrderId != prior.OrderId || result.AttemptId != prior.AttemptId || result.Claims == null ||
            result.Claims.Count > 256 || !Enum.IsDefined(result.Outcome) ||
            result.Claims.Any(x => x == null || !Enum.IsDefined(x.Status)))
            throw Conflict("The provider returned evidence for a different reservation request.");
        var claims = prior.Claims.ToDictionary(GiftcardRecoveryClaimKey, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var promoted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in result.Claims)
        {
            var key = GiftcardRecoveryClaimKey(claim);
            if (!seen.Add(key)) throw Conflict("The provider returned ambiguous reservation evidence.");
            if (claims.TryGetValue(key, out var existing))
            {
                if (!SameRecoveryClaim(existing, claim)) throw Conflict("The provider changed reservation ownership or metadata. Contact support.");
                if (existing.Status == GiftcardRecoveryClaimStatus.Settled && claim.Status != GiftcardRecoveryClaimStatus.Settled)
                    throw Conflict("The provider changed a settled reservation's status. Contact support.");
                claims[key] = existing.Status == GiftcardRecoveryClaimStatus.Released ? existing : claim;
            }
            else
            {
                var matches = prior.Claims.Where(x => !string.IsNullOrWhiteSpace(x.ClaimId) && x.ClaimId == claim.ClaimId).ToArray();
                if (matches.Length > 1) throw Conflict("The provider returned ambiguous ownership for diagnostic claims. Contact support.");
                if (matches.Length == 1)
                {
                    if (result.Claims.Count(x => x.ClaimId == claim.ClaimId) != 1)
                        throw Conflict("The provider returned multiple ownership assertions for one diagnostic claim.");
                    var diagnosticKey = GiftcardRecoveryClaimKey(matches[0]);
                    if (!promoted.Add(diagnosticKey)) throw Conflict("The provider returned multiple ownership assertions for one diagnostic claim.");
                    var verified = PromoteVerifiedRecoveryDiagnostic(matches[0], claim);
                    claims.Remove(diagnosticKey);
                    claims.Add(key, verified);
                }
                else claims.Add(key, claim);
            }
        }
        // A terminal assertion never erases a previously known unresolved claim.
        var merged = result with { Claims = claims.Values.ToArray() };
        if (merged.Claims.Count > 256 || JsonConvert.SerializeObject(merged).Length > 131072)
            throw Conflict("The provider returned excessive reservation evidence. Contact support.");
        return merged;
    }

    public async Task<GiftcardRecoveryResponse> RecoverGiftcardAsync(Guid orderId, GiftcardRecoveryRequest request,
        bool reconcile, IReadOnlyCollection<string> allowedStoreAliases, string? userName, CancellationToken ct = default)
    {
        using var suppression = CheckoutPaymentOperationScope.Suppress();
        var executionId = Guid.NewGuid();
        var owner = Guid.NewGuid().ToString();
        GiftcardReservationRecoveryData row;
        GiftcardReservationFailure evidence;
        GiftcardRecoveryClaim? selected = null;
        await using (var db = _factory.GetDatabase())
        {
            var initial = await ReadGiftcardManagerStateAsync(db, orderId, ct).ConfigureAwait(false);
            AuthorizeGiftcardRecovery(initial, allowedStoreAliases);
        }
        await LogManagerReleaseAsync(orderId, $"Manager gift-card recovery requested; attempt={request.AttemptId}; execution={executionId}; action={(reconcile ? "Reconcile" : "Release")}", userName).ConfigureAwait(false);
        try
        {
            await using var db = _factory.GetDatabase();
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            var state = await ReadGiftcardManagerStateAsync(db, orderId, ct).ConfigureAwait(false);
            AuthorizeGiftcardRecovery(state, allowedStoreAliases);
            row = state.Rows.SingleOrDefault(x => x.AttemptId == request.AttemptId) ?? throw Conflict("Reservation evidence changed. Refresh before recovery.");
            evidence = ReadGiftcardRecoveryEvidence(row);
            if (evidence.OrderId != orderId || evidence.AttemptId != request.AttemptId)
                throw Conflict("Reservation evidence could not be verified. Contact support.");
            if (!reconcile)
                selected = evidence.Claims.SingleOrDefault(x => GiftcardRecoveryClaimKey(x) == request.ClaimId);
            if (!string.IsNullOrWhiteSpace(request.Token) && state.Checkout.Operation?.ActiveAttemptId == request.AttemptId &&
                (reconcile && row.State == GiftcardReservationRecoveryState.Resolved || !reconcile && selected?.Status == GiftcardRecoveryClaimStatus.Released))
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                await LogManagerReleaseAsync(orderId, $"Manager gift-card recovery result; attempt={request.AttemptId}; execution={executionId}; outcome=AlreadyComplete", userName).ConfigureAwait(false);
                return new("The recorded recovery action is already complete.", row.State == GiftcardReservationRecoveryState.Resolved);
            }
            var action = reconcile ? "reconcile" : "release:" + request.ClaimId;
            if (!string.Equals(request.Token, GiftcardActionToken(state, row, action), StringComparison.Ordinal))
                throw Conflict("Checkout or reservation evidence changed. Refresh and confirm recovery again.");
            var reason = GiftcardRecoveryUnavailable(state, row);
            if (reason != null) throw Conflict(reason);
            if (!reconcile && (row.State != GiftcardReservationRecoveryState.Unresolved || selected == null || !IsReleasableRecoveryClaim(selected)))
                throw Conflict("Only a proven newly acquired claim awaiting cleanup can be released.");
            var checkout = state.Checkout;
            if (await db.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == orderId && x.Owner == checkout.Operation!.Owner && x.ActiveAttemptId == row.AttemptId)
                .Set(x => x.Owner, owner).UpdateAsync(ct).ConfigureAwait(false) != 1)
                throw Conflict("Checkout ownership changed. Refresh before recovery.");
            if (checkout.Preparation == null)
                await db.InsertAsync(new CheckoutPreparationData { OrderId = orderId, Owner = owner }, token: ct).ConfigureAwait(false);
            else if (await db.GetTable<CheckoutPreparationData>().Where(x => x.OrderId == orderId && x.Owner == checkout.Preparation.Owner)
                .Set(x => x.Owner, owner).UpdateAsync(ct).ConfigureAwait(false) != 1)
                throw Conflict("Checkout preparation ownership changed. Refresh before recovery.");
            var revision = Guid.NewGuid();
            if (await db.GetTable<GiftcardReservationRecoveryData>().Where(x => x.AttemptId == row.AttemptId && x.Revision == row.Revision && x.Owner == row.Owner)
                .Set(x => x.Owner, owner).Set(x => x.PreparationOwner, owner).Set(x => x.Revision, revision)
                .Set(x => x.Reason, GiftcardRecoveryRunning).Set(x => x.UpdatedUtc, DateTime.UtcNow).UpdateAsync(ct).ConfigureAwait(false) != 1)
                throw Conflict("Reservation evidence changed. Refresh before recovery.");
            row.Owner = owner; row.PreparationOwner = owner; row.Revision = revision;
            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            await LogManagerReleaseAsync(orderId, $"Manager gift-card recovery blocked or acquisition unconfirmed; attempt={request.AttemptId}; execution={executionId}", userName).ConfigureAwait(false);
            throw;
        }

        // After acquiring both fences, cancellation must not discard external-effect evidence.
        var confirmed = false;
        var failed = false;
        try
        {
            var adapter = (ICheckoutGiftcardReservationRecovery)_giftcards!;
            if (reconcile)
            {
                var result = await adapter.ReconcileReservationFailureAsync(orderId, row.AttemptId, evidence, CancellationToken.None).ConfigureAwait(false);
                evidence = MergeReconciledEvidence(evidence, result);
                confirmed = result.AllEffectsAccountedFor && result.Outcome != GiftcardReservationFailureOutcome.Unknown;
                if (!confirmed) evidence = evidence with { AllEffectsAccountedFor = false, Outcome = GiftcardReservationFailureOutcome.Unknown };
            }
            else
            {
                var results = await adapter.ReleaseRecoveryClaimsAsync(orderId, row.AttemptId, [selected!], CancellationToken.None).ConfigureAwait(false);
                if (results == null || results.Count != 1 || results[0] == null ||
                    !SameRecoveryClaim(selected!, results[0]) || !Enum.IsDefined(results[0].Status))
                    throw Conflict("The provider did not confirm the identity of the exact selected claim.");
                evidence = evidence with { Claims = evidence.Claims.Select(x => GiftcardRecoveryClaimKey(x) == request.ClaimId ? results[0] : x).ToArray() };
                if (evidence.AllEffectsAccountedFor && evidence.Outcome != GiftcardReservationFailureOutcome.Unknown &&
                    evidence.Claims.All(x => x.NewlyAcquired && x.Status == GiftcardRecoveryClaimStatus.Released))
                    evidence = evidence with { Outcome = GiftcardReservationFailureOutcome.Compensated };
                confirmed = results[0].Status == GiftcardRecoveryClaimStatus.Released;
                failed = !confirmed;
            }
        }
        catch
        {
            failed = true;
            if (selected != null)
                evidence = evidence with { Claims = evidence.Claims.Select(x => GiftcardRecoveryClaimKey(x) == request.ClaimId
                    ? x with { Status = GiftcardRecoveryClaimStatus.ReleaseUnconfirmed } : x).ToArray() };
        }

        bool resolved;
        try
        {
            await using var db = _factory.GetDatabase();
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None).ConfigureAwait(false);
            var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleOrDefaultAsync(x => x.OrderId == orderId, CancellationToken.None).ConfigureAwait(false);
            var preparation = await db.GetTable<CheckoutPreparationData>().SingleOrDefaultAsync(x => x.OrderId == orderId, CancellationToken.None).ConfigureAwait(false);
            if (operation?.Owner != owner || operation.ActiveAttemptId != row.AttemptId || preparation?.Owner != owner)
                throw Conflict("Recovery ownership changed. Provider effects require manual verification.");
            row.EvidenceJson = JsonConvert.SerializeObject(evidence);
            // A still-running original reserve is never promoted into releasable cleanup evidence.
            row.State = row.State == GiftcardReservationRecoveryState.Pending && !(reconcile && confirmed && !failed)
                ? GiftcardReservationRecoveryState.Pending : GiftcardReservationRecoveryState.Unresolved;
            row.Reason = failed ? "Provider recovery could not be confirmed" : "Provider recovery evidence recorded";
            var revision = Guid.NewGuid();
            if (await db.GetTable<GiftcardReservationRecoveryData>().Where(x => x.AttemptId == row.AttemptId && x.Owner == owner && x.Revision == row.Revision)
                .Set(x => x.EvidenceJson, row.EvidenceJson).Set(x => x.State, row.State).Set(x => x.Reason, row.Reason)
                .Set(x => x.Revision, revision).Set(x => x.UpdatedUtc, DateTime.UtcNow).UpdateAsync(CancellationToken.None).ConfigureAwait(false) != 1)
                throw Conflict("Recovery evidence changed. Provider effects require manual verification.");
            row.Revision = revision;
            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            // The strict closure helper owns its transaction and rechecks the persisted revision.
            await using var scope = new CheckoutPaymentOperationScope(orderId, owner, null) { AttemptId = row.AttemptId, PreserveOwnership = true };
            using var activation = scope.Enter();
            resolved = !failed && confirmed && IsGiftcardRecoveryResolved(evidence) &&
                await CompleteFailedGiftcardRecoveryAsync(db, scope, row, CancellationToken.None).ConfigureAwait(false);
            ClearCache(orderId);
        }
        catch
        {
            await LogManagerReleaseAsync(orderId, $"Manager gift-card recovery persistence unconfirmed; attempt={request.AttemptId}; execution={executionId}", userName).ConfigureAwait(false);
            throw;
        }
        await LogManagerReleaseAsync(orderId, $"Manager gift-card recovery result; attempt={request.AttemptId}; execution={executionId}; outcome={(failed ? "Unconfirmed" : resolved ? "Resolved" : "EvidenceRecordedCheckoutStillBlocked")}", userName).ConfigureAwait(false);
        return new(resolved ? "Gift-card effects are resolved and checkout preparation was safely closed."
            : failed ? "Provider recovery could not be confirmed. Checkout remains blocked; reconcile before retrying."
            : "Recovery evidence was recorded. Checkout remains blocked until all effects and preparation issues are resolved.", resolved);
    }
}
