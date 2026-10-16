using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class GiftcardPreparationRecoveryTests
{
    [Fact]
    public async Task CompensatedReceiptClosesOnlyNewAttemptAndPreservesRawSelections()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var original = await SeedSelectionAsync(f);
        var adapter = FailureAdapter((order, attempt) => Failure(order, attempt,
            GiftcardRecoveryClaimStatus.Released, accounted: true));

        var exception = await Assert.ThrowsAsync<GiftcardReservationFailureException>(() => BeginAsync(f, adapter.Object));

        using var db = f.Database.Factory.GetDatabase();
        var attempt = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync();
        Assert.Equal(f.OrderId, exception.Failure.OrderId);
        Assert.Equal(attempt.AttemptId, exception.Failure.AttemptId);
        Assert.Equal(CheckoutPaymentAttemptState.Released, attempt.State);
        Assert.NotNull(attempt.ClosedUtc);
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleAsync();
        Assert.Null(operation.Owner);
        Assert.False(operation.ReconciliationRequired);
        Assert.Equal(GiftcardReservationRecoveryState.Resolved,
            (await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync()).State);
        Assert.Equal(original, (await f.ReadAsync()).OrderInfo);
        VerifyNoBroadRelease(adapter);
        await using var retry = await f.Service.BeginCheckoutAsync(new OrderInfo(await f.ReadAsync()));
        Assert.NotEqual(attempt.AttemptId, retry.AttemptId);
    }

    [Theory]
    [InlineData(GiftcardRecoveryClaimStatus.Active)]
    [InlineData(GiftcardRecoveryClaimStatus.ReleaseUnconfirmed)]
    [InlineData(GiftcardRecoveryClaimStatus.Unknown)]
    [InlineData(GiftcardRecoveryClaimStatus.Settled)]
    public async Task NonterminalClaimIsDurableWithoutBecomingPaymentContribution(GiftcardRecoveryClaimStatus status)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var original = await SeedSelectionAsync(f);
        var adapter = FailureAdapter((order, attempt) => Failure(order, attempt, status, accounted: true));
        var order = new OrderInfo(await f.ReadAsync());

        await Assert.ThrowsAsync<GiftcardReservationFailureException>(() => f.NewService(adapter.Object).BeginCheckoutAsync(order));

        using var db = f.Database.Factory.GetDatabase();
        var ledger = await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync();
        Assert.Equal(GiftcardReservationRecoveryState.Unresolved, ledger.State);
        var evidence = JsonConvert.DeserializeObject<GiftcardReservationFailure>(ledger.EvidenceJson)!;
        Assert.Equal(status, Assert.Single(evidence.Claims).Status);
        Assert.Equal("not-a-decimal", evidence.Claims[0].RawReturnedAmount);
        Assert.Equal("not-a-date", evidence.Claims[0].RawExpiry);
        Assert.Equal(original, (await f.ReadAsync()).OrderInfo);
        Assert.Null(Assert.Single(order.Giftcards).ClaimId);
        await AssertBlockedAsync(f);
        VerifyNoBroadRelease(adapter);
    }

    [Fact]
    public async Task ReleasedKnownClaimDoesNotProveUnknownEffectsAreTerminal()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await SeedSelectionAsync(f);
        var adapter = FailureAdapter((order, attempt) => Failure(order, attempt,
            GiftcardRecoveryClaimStatus.Released, accounted: false));

        await Assert.ThrowsAsync<GiftcardReservationFailureException>(() =>
            BeginAsync(f, adapter.Object));

        await AssertBlockedAsync(f);
        using var db = f.Database.Factory.GetDatabase();
        var evidence = ReadEvidence(await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync());
        Assert.False(evidence.AllEffectsAccountedFor);
        Assert.Equal(GiftcardRecoveryClaimStatus.Released, Assert.Single(evidence.Claims).Status);
    }

    [Theory]
    [InlineData("order")]
    [InlineData("attempt")]
    [InlineData("not-new")]
    [InlineData("missing-provider")]
    [InlineData("invalid-status")]
    public async Task MalformedOrForeignCompensationCannotUnlock(string invalid)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var original = await SeedSelectionAsync(f);
        var adapter = FailureAdapter((order, attempt) =>
        {
            var failure = Failure(order, attempt, GiftcardRecoveryClaimStatus.Released, accounted: true);
            return invalid switch
            {
                "order" => failure with { OrderId = Guid.NewGuid() },
                "attempt" => failure with { AttemptId = Guid.NewGuid() },
                "not-new" => failure with { Claims = new[] { Claim() with { NewlyAcquired = false, Status = GiftcardRecoveryClaimStatus.Released } } },
                "missing-provider" => failure with { Claims = new[] { Claim() with { Provider = "", Status = GiftcardRecoveryClaimStatus.Released } } },
                _ => failure with { Claims = new[] { Claim() with { Status = (GiftcardRecoveryClaimStatus)999 } } },
            };
        });

        await Assert.ThrowsAsync<GiftcardReservationFailureException>(() => BeginAsync(f, adapter.Object));

        await AssertBlockedAsync(f);
        Assert.Equal(original, (await f.ReadAsync()).OrderInfo);
    }

    [Fact]
    public async Task GenericAdapterFailureHasDurableUnknownIntentBeforeAnyRetry()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await SeedSelectionAsync(f);
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReserveAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Returns((Guid order, Guid attempt, IReadOnlyList<Giftcard> _, CancellationToken _) =>
            {
                using var db = f.Database.Factory.GetDatabase();
                var intent = db.GetTable<GiftcardReservationRecoveryData>().Single();
                Assert.Equal(GiftcardReservationRecoveryState.Pending, intent.State);
                Assert.Equal(attempt, intent.AttemptId);
                Assert.Equal(order, intent.OrderId);
                throw new TimeoutException("secret-code must not appear in audit");
            });

        await Assert.ThrowsAsync<TimeoutException>(() => BeginAsync(f, adapter.Object));

        using var read = f.Database.Factory.GetDatabase();
        var evidence = ReadEvidence(await read.GetTable<GiftcardReservationRecoveryData>().SingleAsync());
        Assert.Equal(GiftcardReservationFailureOutcome.Unknown, evidence.Outcome);
        Assert.False(evidence.AllEffectsAccountedFor);
        await AssertBlockedAsync(f);
        Assert.DoesNotContain(await read.GetTable<OrderActivityLog>().ToListAsync(), x => x.Log.Contains("secret-code", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("TransactionId")]
    [InlineData("UsedDate")]
    [InlineData("Claimed")]
    [InlineData("ClaimDate")]
    public async Task SettledSuccessfulReturnIsRecordedBeforeValidationRejectsIt(string settledField)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var original = await SeedSelectionAsync(f);
        var returned = new Giftcard
        {
            Code = "secret-code", Amount = 5, ClaimId = "returned-claim",
            TransactionId = settledField == "TransactionId" ? "settled" : null,
            UsedDate = settledField == "UsedDate" ? DateTime.UtcNow : null,
            Claimed = settledField == "Claimed",
            ClaimDate = settledField == "ClaimDate" ? DateTime.UtcNow : null,
        };
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReserveAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { returned });

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => BeginAsync(f, adapter.Object));

        using var db = f.Database.Factory.GetDatabase();
        var ledger = await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync();
        Assert.Equal(GiftcardReservationRecoveryState.Unresolved, ledger.State);
        var claim = Assert.Single(ReadEvidence(ledger).Claims);
        Assert.Equal("returned-claim", claim.ClaimId);
        Assert.Equal(GiftcardRecoveryClaimStatus.Settled, claim.Status);
        Assert.Equal(original, (await f.ReadAsync()).OrderInfo);
        await AssertBlockedAsync(f);
        VerifyNoBroadRelease(adapter);
    }

    [Fact]
    public async Task OrderSaveFailureDoesNotRollBackIndependentReturnedClaimEvidence()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var original = await SeedSelectionAsync(f);
        using var db = f.Database.Factory.GetDatabase();
        db.Execute("CREATE TRIGGER FailGiftcardSave BEFORE UPDATE OF OrderInfo ON EkomOrders BEGIN SELECT RAISE(ABORT, 'order save unavailable'); END");
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReserveAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Giftcard { Code = "secret-code", Amount = 5, ClaimId = "returned-before-save" } });

        await Assert.ThrowsAnyAsync<Exception>(() => BeginAsync(f, adapter.Object));

        var ledger = await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync();
        Assert.Equal(GiftcardReservationRecoveryState.Unresolved, ledger.State);
        Assert.Equal("returned-before-save", Assert.Single(ReadEvidence(ledger).Claims).ClaimId);
        Assert.Equal(original, (await f.ReadAsync()).OrderInfo);
        await AssertBlockedAsync(f);
    }

    [Fact]
    public async Task StructuredFailureAfterCancellationStillPersistsReceiptWithUncancelledWrites()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await SeedSelectionAsync(f);
        using var cancellation = new CancellationTokenSource();
        var adapter = FailureAdapter((order, attempt) =>
        {
            cancellation.Cancel();
            return Failure(order, attempt, GiftcardRecoveryClaimStatus.Active, accounted: true);
        });

        var order = new OrderInfo(await f.ReadAsync());
        await Assert.ThrowsAsync<GiftcardReservationFailureException>(() =>
            f.NewService(adapter.Object).BeginCheckoutAsync(order, cancellation.Token));

        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal("external-claim", Assert.Single(ReadEvidence(await db.GetTable<GiftcardReservationRecoveryData>().SingleAsync()).Claims).ClaimId);
        await AssertBlockedAsync(f);
    }

    [Fact]
    public async Task EvidenceWriteFailureAfterCompensationLeavesPendingIntentAndDoesNotUnlock()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await SeedSelectionAsync(f);
        var adapter = FailureAdapter((order, attempt) =>
        {
            using var db = f.Database.Factory.GetDatabase();
            db.Execute("CREATE TRIGGER FailReceiptSave BEFORE UPDATE ON EkomGiftcardReservationRecovery BEGIN SELECT RAISE(ABORT, 'receipt save unavailable'); END");
            return Failure(order, attempt, GiftcardRecoveryClaimStatus.Released, accounted: true);
        });

        await Assert.ThrowsAnyAsync<Exception>(() => BeginAsync(f, adapter.Object));

        using var read = f.Database.Factory.GetDatabase();
        Assert.Equal(GiftcardReservationRecoveryState.Pending,
            (await read.GetTable<GiftcardReservationRecoveryData>().SingleAsync()).State);
        await AssertBlockedAsync(f);
        VerifyNoBroadRelease(adapter);
    }

    internal static async Task<string> SeedSelectionAsync(CheckoutPaymentAttemptTests.Fixture f)
    {
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        json["Giftcards"] = new JArray(new JObject
        {
            ["Code"] = "secret-code", ["Amount"] = 5, ["Claimed"] = false,
            ["ProviderSelection"] = new JObject { ["opaque"] = "keep-me" },
        });
        var original = json.ToString(Formatting.None);
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId).Set(x => x.OrderInfo, original).UpdateAsync();
        return original;
    }

    internal static GiftcardRecoveryClaim Claim() => new()
    {
        Provider = "provider", Account = "account", CardCode = "secret-code", ClaimId = "external-claim",
        ProviderRequestId = "reserve-request", RequestedAmount = 5, Currency = "USD", NewlyAcquired = true,
        RawReturnedAmount = "not-a-decimal", ReturnedAmountType = "string", RawExpiry = "not-a-date",
        Status = GiftcardRecoveryClaimStatus.Active,
    };

    internal static GiftcardReservationFailure Failure(Guid order, Guid attempt, GiftcardRecoveryClaimStatus status, bool accounted) => new()
    {
        OrderId = order, AttemptId = attempt, AllEffectsAccountedFor = accounted,
        Outcome = status == GiftcardRecoveryClaimStatus.Released ? GiftcardReservationFailureOutcome.Compensated : GiftcardReservationFailureOutcome.Unresolved,
        Claims = new[] { Claim() with { Status = status } },
    };

    internal static Mock<ICheckoutGiftcardReservations> FailureAdapter(Func<Guid, Guid, GiftcardReservationFailure> receipt)
    {
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.As<ICheckoutGiftcardReservationRecovery>();
        adapter.Setup(x => x.ReserveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Returns((Guid order, Guid attempt, IReadOnlyList<Giftcard> _, CancellationToken _) =>
                Task.FromException<IReadOnlyList<Giftcard>>(new GiftcardReservationFailureException(receipt(order, attempt))));
        return adapter;
    }

    internal static GiftcardReservationFailure ReadEvidence(GiftcardReservationRecoveryData ledger)
        => JsonConvert.DeserializeObject<GiftcardReservationFailure>(ledger.EvidenceJson)!;

    internal static async Task AssertBlockedAsync(CheckoutPaymentAttemptTests.Fixture f)
    {
        using var db = f.Database.Factory.GetDatabase();
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleAsync();
        Assert.NotNull(operation.Owner);
        Assert.NotNull(operation.ActiveAttemptId);
        var order = new OrderInfo(await f.ReadAsync());
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.NewService().BeginCheckoutAsync(order));
    }

    private static async Task<CheckoutPaymentOperationScope> BeginAsync(CheckoutPaymentAttemptTests.Fixture f, ICheckoutGiftcardReservations adapter)
        => await f.NewService(adapter).BeginCheckoutAsync(new OrderInfo(await f.ReadAsync()));

    private static void VerifyNoBroadRelease(Mock<ICheckoutGiftcardReservations> adapter)
        => adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
}
