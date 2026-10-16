using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using static Ekom.Tests.Tests.GiftcardPreparationRecoveryTests;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class ManagerGiftcardPreparationRecoveryTests
{
    private static readonly string[] Stores = { "main" };

    [Fact]
    public async Task RecoveryViewReadsIndependentEvidenceAndMasksCardAndClaimSecrets()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var (service, _) = await FailedAsync(f);
        using var db = f.Database.Factory.GetDatabase();
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        json["Giftcards"] = new JArray();
        await db.GetTable<OrderData>().Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();

        var view = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));

        var claim = Assert.Single(view.Claims);
        Assert.True(claim.CanRelease);
        Assert.False(string.IsNullOrWhiteSpace(claim.ReleaseToken));
        Assert.Equal(5, claim.RequestedAmount);
        Assert.Equal("not-a-decimal", claim.RawReturnedAmount);
        Assert.Equal("not-a-date", claim.RawExpiry);
        Assert.DoesNotContain("secret-code", JsonConvert.SerializeObject(view), StringComparison.Ordinal);
        Assert.DoesNotContain("external-claim", JsonConvert.SerializeObject(view), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthoritativeReconciliationPromotesV1SaveFailureDiagnosticsThenReleasesExactClaim()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var original = await SeedSelectionAsync(f);
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        var recovery = adapter.As<ICheckoutGiftcardReservationRecovery>();
        adapter.Setup(x => x.ReserveAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Giftcard { Code = "secret-code", Amount = 5, ClaimId = "returned-before-save" } });
        var service = f.NewService(adapter.Object);
        GiftcardReservationRecoveryData row;
        using (var db = f.Database.Factory.GetDatabase())
        {
            await db.InsertOrReplaceAsync(new CheckoutPreparationData { OrderId = f.OrderId });
            db.Execute("CREATE TRIGGER FailGiftcardSave BEFORE UPDATE OF OrderInfo ON EkomOrders BEGIN SELECT RAISE(ABORT, 'order save unavailable'); END");
            var order = new OrderInfo(await f.ReadAsync());
            await Assert.ThrowsAnyAsync<Exception>(() => service.BeginCheckoutAsync(order));
            db.Execute("DROP TRIGGER FailGiftcardSave");
            row = await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync();
        }
        var diagnostic = Assert.Single(ReadEvidence(row).Claims);
        Assert.Empty(diagnostic.Provider);
        Assert.Empty(diagnostic.Account);
        Assert.False(diagnostic.NewlyAcquired);
        var verified = diagnostic with
        {
            Provider = "provider", Account = "account", NewlyAcquired = true,
            Currency = "USD", ProviderRequestId = "original-provider-request", Status = GiftcardRecoveryClaimStatus.Active,
        };
        recovery.Setup(x => x.ReconcileReservationFailureAsync(f.OrderId, row.AttemptId, It.IsAny<GiftcardReservationFailure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GiftcardReservationFailure
            {
                OrderId = f.OrderId, AttemptId = row.AttemptId, AllEffectsAccountedFor = true,
                Outcome = GiftcardReservationFailureOutcome.Unresolved, Claims = new[] { verified },
            });
        var manager = (IOrderManagerGiftcardRecoveryService)service;
        var before = Assert.Single(await manager.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        Assert.False(Assert.Single(before.Claims).CanRelease);

        var reconciled = await manager.RecoverGiftcardAsync(f.OrderId,
            new GiftcardRecoveryRequest { AttemptId = row.AttemptId, Token = before.ReconcileToken! }, true, Stores, "editor");

        Assert.False(reconciled.Resolved);
        var after = Assert.Single(await manager.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        Assert.False(after.HasUnknownEffects);
        Assert.True(Assert.Single(after.Claims).CanRelease);
        Assert.NotEqual(Assert.Single(before.Claims).Id, Assert.Single(after.Claims).Id);
        recovery.Setup(x => x.ReleaseRecoveryClaimsAsync(f.OrderId, row.AttemptId,
                It.Is<IReadOnlyList<GiftcardRecoveryClaim>>(x => x.Count == 1 && x[0] == verified), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { verified with { Status = GiftcardRecoveryClaimStatus.Released } });

        var released = await manager.RecoverGiftcardAsync(f.OrderId, ReleaseRequest(after), false, Stores, "editor");

        Assert.True(released.Resolved);
        Assert.Equal(original, (await f.ReadAsync()).OrderInfo);
        using var finalDb = f.Database.Factory.GetDatabase();
        Assert.Equal(GiftcardReservationRecoveryState.Resolved, (await finalDb.GetTable<GiftcardReservationRecoveryData>().SingleAsync()).State);
        Assert.Null((await finalDb.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("account")]
    [InlineData("different-card")]
    [InlineData("amount")]
    [InlineData("raw-amount")]
    [InlineData("expiry")]
    [InlineData("currency")]
    [InlineData("request")]
    [InlineData("ambiguous-prior")]
    [InlineData("ambiguous-result")]
    [InlineData("settled")]
    public async Task DiagnosticPromotionRejectsChangedOrAmbiguousEvidence(string conflict)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var (service, adapter) = await FailedAsync(f, accounted: false);
        var diagnostic = Claim() with { Provider = "", Account = "", NewlyAcquired = false, Currency = "", ProviderRequestId = null, Status = GiftcardRecoveryClaimStatus.Unknown };
        var verified = diagnostic with { Provider = "verified-provider", Account = "verified-account", NewlyAcquired = true, Currency = "USD", ProviderRequestId = "original-request", Status = GiftcardRecoveryClaimStatus.Active };
        diagnostic = conflict switch
        {
            "provider" => diagnostic with { Provider = "established-provider" },
            "account" => diagnostic with { Account = "established-account" },
            "currency" => diagnostic with { Currency = "EUR" },
            "request" => diagnostic with { ProviderRequestId = "established-request" },
            "settled" => diagnostic with { Status = GiftcardRecoveryClaimStatus.Settled },
            _ => diagnostic,
        };
        verified = conflict switch
        {
            "different-card" => verified with { CardCode = "different-card" },
            "amount" => verified with { RequestedAmount = 6 },
            "raw-amount" => verified with { RawReturnedAmount = "changed" },
            "expiry" => verified with { RawExpiry = "changed" },
            _ => verified,
        };
        GiftcardReservationFailure prior;
        using (var db = f.Database.Factory.GetDatabase())
        {
            var row = await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync();
            prior = ReadEvidence(row) with
            {
                Claims = conflict == "ambiguous-prior" ? new[] { diagnostic, diagnostic with { CardCode = "another-card" } } : new[] { diagnostic },
            };
            await db.GetTable<GiftcardReservationRecoveryData>().Set(x => x.EvidenceJson, JsonConvert.SerializeObject(prior)).UpdateAsync();
        }
        var view = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        adapter.As<ICheckoutGiftcardReservationRecovery>()
            .Setup(x => x.ReconcileReservationFailureAsync(f.OrderId, view.AttemptId, It.IsAny<GiftcardReservationFailure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prior with
            {
                AllEffectsAccountedFor = true, Outcome = GiftcardReservationFailureOutcome.Unresolved,
                Claims = conflict == "ambiguous-result" ? new[] { verified, verified with { Provider = "another-provider" } } : new[] { verified },
            });

        var response = await service.RecoverGiftcardAsync(f.OrderId,
            new GiftcardRecoveryRequest { AttemptId = view.AttemptId, Token = view.ReconcileToken! }, true, Stores, "editor");

        Assert.False(response.Resolved);
        using var finalDb = f.Database.Factory.GetDatabase();
        var preserved = ReadEvidence(await finalDb.GetTable<GiftcardReservationRecoveryData>().SingleAsync());
        Assert.Equal(JsonConvert.SerializeObject(prior), JsonConvert.SerializeObject(preserved));
        await AssertBlockedAsync(f);
    }

    [Fact]
    public async Task DiagnosticPromotionPreservesReleasedStatusAndNonterminalInvocationUncertainty()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var (service, adapter) = await FailedAsync(f, accounted: false);
        var diagnostic = Claim() with
        {
            Provider = "", Account = "", NewlyAcquired = false, Currency = "", ProviderRequestId = null,
            Status = GiftcardRecoveryClaimStatus.Released,
        };
        GiftcardReservationFailure prior;
        using (var db = f.Database.Factory.GetDatabase())
        {
            var row = await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync();
            prior = ReadEvidence(row) with { Outcome = GiftcardReservationFailureOutcome.Unknown, Claims = new[] { diagnostic } };
            await db.GetTable<GiftcardReservationRecoveryData>().Set(x => x.EvidenceJson, JsonConvert.SerializeObject(prior)).UpdateAsync();
        }
        var verified = diagnostic with
        {
            Provider = "provider", Account = "account", NewlyAcquired = true,
            Currency = "USD", ProviderRequestId = "original-request", Status = GiftcardRecoveryClaimStatus.Active,
        };
        var view = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        adapter.As<ICheckoutGiftcardReservationRecovery>()
            .Setup(x => x.ReconcileReservationFailureAsync(f.OrderId, view.AttemptId, It.IsAny<GiftcardReservationFailure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prior with { Claims = new[] { verified } });

        var response = await service.RecoverGiftcardAsync(f.OrderId,
            new GiftcardRecoveryRequest { AttemptId = view.AttemptId, Token = view.ReconcileToken! }, true, Stores, "editor");

        Assert.False(response.Resolved);
        var after = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        Assert.True(after.HasUnknownEffects);
        Assert.Equal(nameof(GiftcardRecoveryClaimStatus.Released), Assert.Single(after.Claims).CleanupState);
        Assert.False(Assert.Single(after.Claims).CanRelease);
        using var finalDb = f.Database.Factory.GetDatabase();
        var evidence = ReadEvidence(await finalDb.GetTable<GiftcardReservationRecoveryData>().SingleAsync());
        Assert.Equal(verified with { Status = GiftcardRecoveryClaimStatus.Released }, Assert.Single(evidence.Claims));
        Assert.False(evidence.AllEffectsAccountedFor);
        await AssertBlockedAsync(f);
    }

    [Fact]
    public async Task RecoveryDiagnosticsPreserveMalformedScalarsButEscapeBoundAndRedactReferences()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var (service, _) = await FailedAsync(f);
        using (var db = f.Database.Factory.GetDatabase())
        {
            var row = await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync();
            var evidence = ReadEvidence(row);
            var diagnostic = "malformed <value> secret-code external-claim account reserve-request\n" + new string('x', 300);
            evidence = evidence with
            {
                Claims = evidence.Claims.Select(x => x with
                {
                    RawReturnedAmount = diagnostic,
                    RawExpiry = diagnostic,
                    ReturnedAmountType = diagnostic,
                }).ToArray(),
            };
            await db.GetTable<GiftcardReservationRecoveryData>().Set(x => x.EvidenceJson, JsonConvert.SerializeObject(evidence)).UpdateAsync();
        }

        var view = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        var claim = Assert.Single(view.Claims);
        Assert.Contains("malformed &lt;value&gt;", claim.RawReturnedAmount);
        Assert.Equal(claim.RawReturnedAmount, claim.RawExpiry);
        Assert.Equal(claim.RawReturnedAmount, claim.ReturnedAmountType);
        Assert.True(claim.RawReturnedAmount!.Length <= 768);
        Assert.DoesNotContain('\n', claim.RawReturnedAmount);
        var json = JsonConvert.SerializeObject(view);
        foreach (var reference in new[] { "secret-code", "external-claim", "account", "reserve-request" })
            Assert.DoesNotContain(reference, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReconciliationCannotMakePreviouslySettledClaimReleasable()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var (service, adapter) = await FailedAsync(f);
        using (var db = f.Database.Factory.GetDatabase())
        {
            var row = await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync();
            var evidence = ReadEvidence(row);
            evidence = evidence with { Claims = evidence.Claims.Select(x => x with { Status = GiftcardRecoveryClaimStatus.Settled }).ToArray() };
            await db.GetTable<GiftcardReservationRecoveryData>().Set(x => x.EvidenceJson, JsonConvert.SerializeObject(evidence)).UpdateAsync();
        }
        var view = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        adapter.As<ICheckoutGiftcardReservationRecovery>()
            .Setup(x => x.ReconcileReservationFailureAsync(f.OrderId, view.AttemptId, It.IsAny<GiftcardReservationFailure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Failure(f.OrderId, view.AttemptId, GiftcardRecoveryClaimStatus.Active, true));

        var response = await service.RecoverGiftcardAsync(f.OrderId,
            new GiftcardRecoveryRequest { AttemptId = view.AttemptId, Token = view.ReconcileToken! }, true, Stores, "editor");

        Assert.False(response.Resolved);
        var claim = Assert.Single(Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores)).Claims);
        Assert.Equal(nameof(GiftcardRecoveryClaimStatus.Settled), claim.CleanupState);
        Assert.False(claim.CanRelease);
        await AssertBlockedAsync(f);
    }

    [Fact]
    public async Task ReleaseUsesExactEvidenceAndOriginalAttemptWithoutChangingPurchaseAndIsSafeToRepeat()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var (service, adapter) = await FailedAsync(f);
        var original = (await f.ReadAsync()).OrderInfo;
        var view = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        var request = ReleaseRequest(view);
        adapter.As<ICheckoutGiftcardReservationRecovery>()
            .Setup(x => x.ReleaseRecoveryClaimsAsync(f.OrderId, view.AttemptId, It.IsAny<IReadOnlyList<GiftcardRecoveryClaim>>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid _, IReadOnlyList<GiftcardRecoveryClaim> claims, CancellationToken _) =>
            {
                var claim = Assert.Single(claims);
                Assert.Equal(Claim(), claim);
                return Task.FromResult<IReadOnlyList<GiftcardRecoveryClaim>>(new[] { claim with { Status = GiftcardRecoveryClaimStatus.Released } });
            });

        var response = await service.RecoverGiftcardAsync(f.OrderId, request, false, Stores, "editor");

        Assert.True(response.Resolved);
        Assert.Equal(original, (await f.ReadAsync()).OrderInfo);
        using var db = f.Database.Factory.GetDatabase();
        Assert.Null((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        Assert.Equal(CheckoutPaymentAttemptState.Released, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        // A stale confirmation may be rejected or treated as an idempotent no-op, but must never release twice.
        await Record.ExceptionAsync(() => service.RecoverGiftcardAsync(f.OrderId, request, false, Stores, "editor"));
        adapter.As<ICheckoutGiftcardReservationRecovery>().Verify(x => x.ReleaseRecoveryClaimsAsync(
            f.OrderId, view.AttemptId, It.IsAny<IReadOnlyList<GiftcardRecoveryClaim>>(), It.IsAny<CancellationToken>()), Times.Once);
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(await db.GetTable<OrderActivityLog>().ToListAsync(), x =>
            x.Log.Contains("secret-code", StringComparison.Ordinal) || x.Log.Contains("external-claim", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReleasingKnownClaimCannotClearGlobalUnknownEffects()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var (service, adapter) = await FailedAsync(f, accounted: false);
        var view = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        adapter.As<ICheckoutGiftcardReservationRecovery>()
            .Setup(x => x.ReleaseRecoveryClaimsAsync(f.OrderId, view.AttemptId, It.IsAny<IReadOnlyList<GiftcardRecoveryClaim>>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid _, IReadOnlyList<GiftcardRecoveryClaim> claims, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<GiftcardRecoveryClaim>>(claims.Select(x => x with { Status = GiftcardRecoveryClaimStatus.Released }).ToArray()));

        var response = await service.RecoverGiftcardAsync(f.OrderId, ReleaseRequest(view), false, Stores, "editor");

        Assert.False(response.Resolved);
        var refreshed = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        Assert.True(refreshed.HasUnknownEffects);
        await AssertBlockedAsync(f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownReconciliationRequiresExplicitTerminalWholeInvocationProof(bool terminal)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var (service, adapter) = await FailedAsync(f, accounted: false);
        var view = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        Assert.True(view.CanReconcile);
        adapter.As<ICheckoutGiftcardReservationRecovery>()
            .Setup(x => x.ReconcileReservationFailureAsync(f.OrderId, view.AttemptId, It.IsAny<GiftcardReservationFailure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Failure(f.OrderId, view.AttemptId, GiftcardRecoveryClaimStatus.Released, terminal));

        var response = await service.RecoverGiftcardAsync(f.OrderId, new GiftcardRecoveryRequest
        {
            AttemptId = view.AttemptId, Token = view.ReconcileToken!,
        }, true, Stores, "editor");

        Assert.Equal(terminal, response.Resolved);
        using var db = f.Database.Factory.GetDatabase();
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleAsync();
        Assert.Equal(terminal, operation.Owner == null);
        if (!terminal) await AssertBlockedAsync(f);
    }

    [Fact]
    public async Task StaleConfirmationCannotReleaseNewerReplacementEvidence()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var (service, adapter) = await FailedAsync(f);
        var view = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        using var db = f.Database.Factory.GetDatabase();
        var ledger = await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync();
        var replacement = ReadEvidence(ledger) with { Claims = new[] { Claim() with { ClaimId = "replacement-claim" } } };
        await db.GetTable<GiftcardReservationRecoveryData>().Set(x => x.Revision, Guid.NewGuid())
            .Set(x => x.EvidenceJson, JsonConvert.SerializeObject(replacement)).UpdateAsync();

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => service.RecoverGiftcardAsync(
            f.OrderId, ReleaseRequest(view), false, Stores, "editor"));

        adapter.As<ICheckoutGiftcardReservationRecovery>().Verify(x => x.ReleaseRecoveryClaimsAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<GiftcardRecoveryClaim>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("replacement-claim", Assert.Single(ReadEvidence(await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync()).Claims).ClaimId);
        await AssertBlockedAsync(f);
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("submitted")]
    [InlineData("settled")]
    public async Task PaidSubmittedOrSettledStateChangeInvalidatesPreviouslyAvailableRelease(string state)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var (service, adapter) = await FailedAsync(f);
        var view = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        using var db = f.Database.Factory.GetDatabase();
        if (state == "paid")
            await db.GetTable<OrderData>().Set(x => x.OrderStatusCol, "Closed").UpdateAsync();
        else if (state == "settled")
        {
            var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
            json["Giftcards"] = JArray.FromObject(new[] { new Giftcard { Code = "settled-selection", Amount = 5, Claimed = true } });
            await db.GetTable<OrderData>().Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
        }
        else
            await db.GetTable<CheckoutPaymentAttemptData>().Set(x => x.State, CheckoutPaymentAttemptState.Submitted)
                .Set(x => x.SubmittedUtc, (DateTime?)DateTime.UtcNow).UpdateAsync();

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => service.RecoverGiftcardAsync(
            f.OrderId, ReleaseRequest(view), false, Stores, "editor"));

        adapter.As<ICheckoutGiftcardReservationRecovery>().Verify(x => x.ReleaseRecoveryClaimsAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<GiftcardRecoveryClaim>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores)).CanReconcile);
        Assert.NotNull((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    [Fact]
    public async Task GenericReleaseLockCannotBypassUnresolvedGiftcardLedger()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await FailedAsync(f);
        using var db = f.Database.Factory.GetDatabase();
        var status = await CheckoutPaymentAttemptService.ReadManagerLockStatusAsync(db, f.OrderId, CancellationToken.None);

        Assert.False(status.CanRelease);
        Assert.Null(status.ReleaseToken);
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.ReleaseOrderLockAsync(f.OrderId, "", Stores, "editor"));

        await AssertBlockedAsync(f);
    }

    [Fact]
    public async Task ExternalReleaseWithFailedReceiptWriteRemainsLockedUntilFreshVerification()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var (service, adapter) = await FailedAsync(f);
        var view = Assert.Single(await service.GetGiftcardRecoveryAsync(f.OrderId, Stores));
        adapter.As<ICheckoutGiftcardReservationRecovery>()
            .Setup(x => x.ReleaseRecoveryClaimsAsync(f.OrderId, view.AttemptId, It.IsAny<IReadOnlyList<GiftcardRecoveryClaim>>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid _, IReadOnlyList<GiftcardRecoveryClaim> claims, CancellationToken _) =>
            {
                using var db = f.Database.Factory.GetDatabase();
                db.Execute("CREATE TRIGGER FailReleasedReceipt BEFORE UPDATE ON EkomGiftcardReservationRecovery BEGIN SELECT RAISE(ABORT, 'receipt unavailable'); END");
                return Task.FromResult<IReadOnlyList<GiftcardRecoveryClaim>>(claims.Select(x => x with { Status = GiftcardRecoveryClaimStatus.Released }).ToArray());
            });

        await Assert.ThrowsAnyAsync<Exception>(() => service.RecoverGiftcardAsync(f.OrderId, ReleaseRequest(view), false, Stores, "editor"));

        await AssertBlockedAsync(f);
        using var read = f.Database.Factory.GetDatabase();
        Assert.NotEqual(GiftcardReservationRecoveryState.Resolved,
            (await read.GetTable<GiftcardReservationRecoveryData>().SingleAsync()).State);
    }

    private static GiftcardRecoveryRequest ReleaseRequest(GiftcardRecoveryView view)
    {
        var claim = Assert.Single(view.Claims);
        return new GiftcardRecoveryRequest { AttemptId = view.AttemptId, ClaimId = claim.Id, Token = claim.ReleaseToken! };
    }

    private static async Task<(IOrderManagerGiftcardRecoveryService Service, Mock<ICheckoutGiftcardReservations> Adapter)> FailedAsync(
        CheckoutPaymentAttemptTests.Fixture f, bool accounted = true)
    {
        await SeedSelectionAsync(f);
        // Checkout normally has a preparation row even when no stock holds were acquired.
        using (var db = f.Database.Factory.GetDatabase())
            await db.InsertOrReplaceAsync(new CheckoutPreparationData { OrderId = f.OrderId });
        var adapter = FailureAdapter((order, attempt) => Failure(order, attempt, GiftcardRecoveryClaimStatus.Active, accounted));
        var service = f.NewService(adapter.Object);
        var order = new OrderInfo(await f.ReadAsync());
        await Assert.ThrowsAsync<GiftcardReservationFailureException>(() => service.BeginCheckoutAsync(order));
        return ((IOrderManagerGiftcardRecoveryService)service, adapter);
    }
}
