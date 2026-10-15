using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Fixture = Ekom.Tests.Tests.CheckoutPaymentAttemptTests.Fixture;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class OrderManagerReservationReleaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagerReleaseOfSubmittedAttemptReleasesAllHoldsAndAuditsUserExactlyOnce(bool giftcardsOnly)
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync(claimed: true);
        var before = JObject.Parse((await f.ReadAsync()).OrderInfo);
        var adapter = Adapter();
        var service = f.NewService(adapter.Object);
        var state = Assert.IsType<CheckoutPaymentAttemptService.ManagerReleaseState>(
            await service.GetManagerReleaseStateAsync(f.OrderId, default));
        Assert.True(state.HasActiveAttempt);
        Assert.True(state.HasGiftcardClaims);
        Assert.True(state.HasCheckoutReservations);
        Assert.True(state.CanRelease);

        Assert.True(await service.ReleaseForManagerAsync(f.OrderId, "main", state.Token, giftcardsOnly, "manager@example.test", default));
        Assert.False(await service.ReleaseForManagerAsync(f.OrderId, "main", state.Token, giftcardsOnly, "manager@example.test", default));

        var saved = await f.ReadAsync();
        Assert.Equal("Incomplete", saved.OrderStatusCol);
        var after = JObject.Parse(saved.OrderInfo);
        Assert.Empty(after["ReservationIds"]!);
        Assert.Empty(after["HangfireJobs"]!);
        AssertReleasedSelection(before, after);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, attempt.AttemptId, It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Once);
        using var db = f.Database.Factory.GetDatabase();
        var stored = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync();
        Assert.Equal(CheckoutPaymentAttemptState.Released, stored.State);
        Assert.Equal(attempt.SubmittedOrderInfo, stored.SubmittedOrderInfo);
        var logs = await db.GetTable<OrderActivityLog>().ToListAsync();
        Assert.Contains(logs, x => x.UserName == "manager@example.test" && x.Log.Contains("Manual release requested", StringComparison.Ordinal));
        Assert.Contains(logs, x => x.UserName == "manager@example.test" && x.Log.Contains("outcome=Released", StringComparison.Ordinal));
        Assert.Contains(logs, x => x.UserName == "manager@example.test" && x.Log.Contains("outcome=AlreadyReleased", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, x => x.Log.Contains("secret-code", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ManagerLegacyReleasePreservesStockOnlyForStandaloneGiftcardAction(bool giftcardsOnly)
    {
        using var f = new Fixture();
        var key = await SeedLegacyAsync(f);
        var before = await f.ReadAsync();
        var adapter = Adapter();
        var service = f.NewService(adapter.Object);
        var state = (await service.GetManagerReleaseStateAsync(f.OrderId, default))!;
        Assert.False(state.HasActiveAttempt);
        Assert.True(await service.ReleaseForManagerAsync(f.OrderId, "main", state.Token, giftcardsOnly, "Operator", default));

        var after = await f.ReadAsync();
        var json = JObject.Parse(after.OrderInfo);
        AssertReleasedSelection(JObject.Parse(before.OrderInfo), json);
        Assert.Equal(giftcardsOnly ? 7 : 10, await f.Database.StockAsync(key));
        Assert.Equal(giftcardsOnly ? before.OrderStatusCol : "Incomplete", after.OrderStatusCol);
        if (giftcardsOnly)
        {
            Assert.True(JToken.DeepEquals(JObject.Parse(before.OrderInfo)["ReservationIds"], json["ReservationIds"]));
            Assert.True(JToken.DeepEquals(JObject.Parse(before.OrderInfo)["HangfireJobs"], json["HangfireJobs"]));
        }
        else
        {
            Assert.Empty(json["ReservationIds"]!);
            Assert.Empty(json["HangfireJobs"]!);
        }
        Assert.False(await service.ReleaseForManagerAsync(f.OrderId, "main", state.Token, giftcardsOnly, "Operator", default));
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, Guid.Empty, It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ManagerStaleConfirmationCannotReleaseNewPaymentAttempt()
    {
        using var f = new Fixture();
        var old = await f.SubmittedAsync();
        var stale = (await f.Service.GetManagerReleaseStateAsync(f.OrderId, default))!;
        await f.Service.ReleaseAsync(f.OrderId, old.AttemptId, "Cancel first attempt");
        var current = await f.SubmittedAsync();
        var before = await f.ReadAsync();

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.ReleaseForManagerAsync(f.OrderId, "main", stale.Token, false, "Operator", default));

        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(CheckoutPaymentAttemptState.Submitted,
            (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == current.AttemptId)).State);
    }

    [Theory]
    [InlineData("PurchaseChanged")]
    [InlineData("StoreChanged")]
    public async Task ManagerConfirmationRefusesChangedPurchaseOrStoreWithoutEffects(string change)
    {
        using var f = new Fixture();
        await f.SubmittedAsync(claimed: true);
        var adapter = Adapter();
        var service = f.NewService(adapter.Object);
        var token = (await service.GetManagerReleaseStateAsync(f.OrderId, default))!.Token;
        using var db = f.Database.Factory.GetDatabase();
        if (change == "StoreChanged")
            await db.GetTable<OrderData>().Set(x => x.StoreAlias, "other-store").UpdateAsync();
        else
        {
            var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
            json["Giftcards"]![0]!["ClaimId"] = "replacement-claim";
            await db.GetTable<OrderData>().Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
        }
        var before = await f.ReadAsync();

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => service.ReleaseForManagerAsync(f.OrderId, "main", token, false, "Operator", default));

        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        Assert.Equal(CheckoutPaymentAttemptState.Submitted, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("PaidDate")]
    [InlineData("Closed")]
    [InlineData("CompletionReceipt")]
    [InlineData("CompletionPending")]
    [InlineData("Completed")]
    [InlineData("ReconciliationRequired")]
    [InlineData("OperationReconciliation")]
    [InlineData("Claimed")]
    [InlineData("ClaimDate")]
    [InlineData("TransactionId")]
    [InlineData("UsedDate")]
    [InlineData("SubmittedTransactionId")]
    public async Task ManagerUnsafeOrdersHaveNoActionsAndRefuseReleaseWithoutEffects(string unsafeState)
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync(claimed: true);
        var adapter = Adapter();
        var service = f.NewService(adapter.Object);
        var token = (await service.GetManagerReleaseStateAsync(f.OrderId, default))!.Token;
        using var db = f.Database.Factory.GetDatabase();
        switch (unsafeState)
        {
            case "PaidDate":
                await db.GetTable<OrderData>().Set(x => x.PaidDate, (DateTime?)DateTime.UtcNow).UpdateAsync();
                break;
            case "Closed":
                await db.GetTable<OrderData>().Set(x => x.OrderStatusCol, "Closed").UpdateAsync();
                break;
            case "CompletionReceipt":
                await db.InsertAsync(new CheckoutStockCompletionData { OrderId = f.OrderId, CompletedUtc = DateTime.UtcNow });
                break;
            case "CompletionPending":
            case "Completed":
            case "ReconciliationRequired":
                await db.GetTable<CheckoutPaymentAttemptData>().Set(x => x.State, Enum.Parse<CheckoutPaymentAttemptState>(unsafeState)).UpdateAsync();
                break;
            case "OperationReconciliation":
                await db.GetTable<CheckoutPaymentOperationData>().Set(x => x.ReconciliationRequired, true).UpdateAsync();
                break;
            case "SubmittedTransactionId":
                var submitted = JObject.Parse(attempt.SubmittedOrderInfo!);
                submitted["Giftcards"]![0]!["TransactionId"] = "settled-in-frozen-attempt";
                await db.GetTable<CheckoutPaymentAttemptData>().Set(x => x.SubmittedOrderInfo, submitted.ToString(Formatting.None)).UpdateAsync();
                break;
            default:
                var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
                json["Giftcards"]![0]![unsafeState] = unsafeState switch
                {
                    "Claimed" => JToken.FromObject(true),
                    "TransactionId" => JToken.FromObject("settled-transaction"),
                    _ => JToken.FromObject(DateTime.UtcNow),
                };
                await db.GetTable<OrderData>().Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
                break;
        }
        var before = await f.ReadAsync();
        var attemptBefore = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync();
        Assert.Null(await service.GetManagerReleaseStateAsync(f.OrderId, default));
        Assert.Empty(await Provider(service).GetActionsAsync(new OrderInfo(before)));

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => service.ReleaseForManagerAsync(f.OrderId, "main", token, false, "Operator", default));

        var after = await f.ReadAsync();
        Assert.Equal(before.OrderInfo, after.OrderInfo);
        Assert.Equal(before.OrderStatusCol, after.OrderStatusCol);
        Assert.Equal(before.PaidDate, after.PaidDate);
        Assert.Equal(attemptBefore.State, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ManagerMissingAdapterDisablesBothActionsAndRefusesDirectExecution()
    {
        using var f = new Fixture();
        await f.SubmittedAsync(claimed: true);
        var before = await f.ReadAsync();
        var state = (await f.Service.GetManagerReleaseStateAsync(f.OrderId, default))!;
        Assert.False(state.CanRelease);
        var provider = Provider(f.Service);
        var actions = await provider.GetActionsAsync(new OrderInfo(before));
        Assert.Equal(2, actions.Count);
        Assert.All(actions, action => Assert.False(action.Enabled));
        foreach (var action in actions)
            Assert.IsType<OrderManagerActionBadRequestResult>(await provider.ExecuteAsync(new OrderInfo(before), action.Key, "Operator"));
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.ReleaseForManagerAsync(f.OrderId, "main", state.Token, false, "Operator", default));
        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagerAdapterFailureKeepsClaimMetadataReportsFailureAndCanRetry(bool legacy)
    {
        using var f = new Fixture();
        var key = legacy ? await SeedLegacyAsync(f) : Guid.Empty;
        if (!legacy) await f.SubmittedAsync(claimed: true);
        var before = await f.ReadAsync();
        var adapter = Adapter();
        adapter.SetupSequence(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Provider unavailable"))
            .Returns(Task.CompletedTask);
        var service = f.NewService(adapter.Object);
        var provider = Provider(service);
        var action = (await provider.GetActionsAsync(new OrderInfo(before))).Single(x => x.Key.StartsWith("ekom.release-giftcards:", StringComparison.Ordinal));

        Assert.IsType<OrderManagerActionBadRequestResult>(await provider.ExecuteAsync(new OrderInfo(before), action.Key, "Operator"));
        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.Equal(before.OrderStatusCol, (await f.ReadAsync()).OrderStatusCol);
        using (var db = f.Database.Factory.GetDatabase())
        {
            var logs = await db.GetTable<OrderActivityLog>().ToListAsync();
            Assert.Contains(logs, x => x.UserName == "Operator" && x.Log.Contains("Manual release failed or blocked", StringComparison.Ordinal));
            Assert.DoesNotContain(logs, x => x.Log.Contains("Manual release completed", StringComparison.Ordinal));
        }
        Assert.IsType<OrderManagerActionSuccessResult>(await provider.ExecuteAsync(new OrderInfo(await f.ReadAsync()), action.Key, "Operator"));
        AssertReleasedSelection(JObject.Parse(before.OrderInfo), JObject.Parse((await f.ReadAsync()).OrderInfo));
        Assert.Equal(legacy ? 7 : 10, await f.Database.StockAsync(legacy ? key : f.StockKey));
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ManagerOverrideCannotBorrowBusyCheckoutOwnershipToReleaseReservations()
    {
        using var f = new Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        await using var checkout = await f.Service.BeginCheckoutAsync(order);
        checkout.PreserveOwnership = true;
        using var checkoutActivation = checkout.Enter();
        var key = await SeedLegacyAsync(f);
        var adapter = Adapter();
        var service = f.NewService(adapter.Object);
        order = new OrderInfo(await f.ReadAsync());
        var provider = Provider(service);
        var action = (await provider.GetActionsAsync(order)).Single(x => x.Key.StartsWith("ekom.release-checkout:", StringComparison.Ordinal));
        // A separate node owns the durable operation; the Manager override is not that capability.
        var before = await f.ReadAsync();
        await using var manager = CheckoutPaymentOperationScope.BeginManagerOverride(f.OrderId);
        using var activation = manager.Enter();

        Assert.IsType<OrderManagerActionBadRequestResult>(await provider.ExecuteAsync(order, action.Key, "Operator"));

        Assert.Same(manager, CheckoutPaymentOperationScope.Current);
        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.Equal(7, await f.Database.StockAsync(key));
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(checkout.Owner, (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManagerDiscoveryRequiresConfirmationAndDescribesWholeAttemptVersusLegacyGiftcardRelease(bool activeAttempt)
    {
        using var f = new Fixture();
        if (activeAttempt) await f.SubmittedAsync(claimed: true);
        else await SeedLegacyAsync(f);
        var provider = Provider(f.NewService(Adapter().Object));
        var actions = await provider.GetActionsAsync(new OrderInfo(await f.ReadAsync()));
        Assert.Equal(2, actions.Count);
        Assert.All(actions, x =>
        {
            Assert.True(x.Enabled);
            Assert.False(string.IsNullOrWhiteSpace(x.ConfirmMessage));
            Assert.Equal(64, x.Key[(x.Key.IndexOf(':') + 1)..].Length);
        });
        var giftcard = actions.Single(x => x.Key.StartsWith("ekom.release-giftcards:", StringComparison.Ordinal));
        Assert.Contains(activeAttempt ? "entire unpaid payment attempt" : "Stock reservations", giftcard.ConfirmMessage, StringComparison.Ordinal);
        var checkout = actions.Single(x => x.Key.StartsWith("ekom.release-checkout:", StringComparison.Ordinal));
        Assert.Contains("does not cancel a payment", checkout.ConfirmMessage, StringComparison.Ordinal);
        Assert.Null(await provider.ExecuteAsync(new OrderInfo(await f.ReadAsync()), "another-provider:action"));
        Assert.IsType<OrderManagerActionBadRequestResult>(await provider.ExecuteAsync(new OrderInfo(await f.ReadAsync()), "ekom.release-checkout:invalid"));
    }

    [Fact]
    public async Task ManagerDiscoveryOffersOnlyCheckoutForStockAndNothingForEmptyOrder()
    {
        using var f = new Fixture();
        var provider = Provider(f.Service);
        Assert.Empty(await provider.GetActionsAsync(new OrderInfo(await f.ReadAsync())));
        await f.SubmittedAsync();
        var action = Assert.Single(await provider.GetActionsAsync(new OrderInfo(await f.ReadAsync())));
        Assert.StartsWith("ekom.release-checkout:", action.Key);
        Assert.True(action.Enabled);
    }

    [Fact]
    public async Task ManagerFreshConfirmationRefusesClaimsDifferentFromFrozenAttemptBeforeAnyReleaseEffects()
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync(claimed: true);
        using var db = f.Database.Factory.GetDatabase();
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        json["Giftcards"]![0]!["ClaimId"] = "replacement-claim";
        await db.GetTable<OrderData>().Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
        var before = await f.ReadAsync();
        var adapter = Adapter();
        var service = f.NewService(adapter.Object);
        // Fetch after the edit: a valid fresh confirmation must not legitimize a mismatched claim.
        var state = (await service.GetManagerReleaseStateAsync(f.OrderId, default))!;

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => service.ReleaseForManagerAsync(f.OrderId, "main", state.Token, false, "Operator", default));

        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        var stored = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync();
        Assert.Equal(CheckoutPaymentAttemptState.Submitted, stored.State);
        Assert.Equal(attempt.SubmittedOrderInfo, stored.SubmittedOrderInfo);
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(await db.GetTable<OrderActivityLog>().ToListAsync(),
            x => x.Log.Contains("Manual release completed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ManagerFreshConfirmationRefusesUnattachedOwnedStockOutsideSubmittedAttempt()
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync(claimed: true);
        var extraKey = await f.Database.SeedAsync(10);
        var extra = await f.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = extraKey, Quantity = 2, OrderId = f.OrderId.ToString(),
        });
        var extraId = Assert.IsType<string>(extra.ReservationId);
        var before = await f.ReadAsync();
        Assert.DoesNotContain(extraId, before.OrderInfo, StringComparison.Ordinal);
        Assert.DoesNotContain(extraId, attempt.ReservationIds, StringComparison.Ordinal);
        var adapter = Adapter();
        var service = f.NewService(adapter.Object);
        var state = (await service.GetManagerReleaseStateAsync(f.OrderId, default))!;

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => service.ReleaseForManagerAsync(f.OrderId, "main", state.Token, false, "Operator", default));

        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        Assert.Equal(8, await f.Database.StockAsync(extraKey));
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(CheckoutPaymentAttemptState.Submitted, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        var holds = await db.GetTable<StockReservationData>().Where(x => x.OrderId == f.OrderId.ToString()).ToListAsync();
        Assert.Equal(2, holds.Count);
        Assert.All(holds, hold => Assert.Equal(StockReservationState.Active, hold.State));
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(await db.GetTable<OrderActivityLog>().ToListAsync(),
            x => x.Log.Contains("Manual release completed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ManagerWriteOverrideCannotBypassRealOwnerDuringExternalGiftcardRelease()
    {
        using var f = new Fixture();
        await f.SubmittedAsync(claimed: true);
        var adapter = Adapter();
        var service = f.NewService(adapter.Object);
        var callbackInvoked = false;
        adapter.Setup(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                callbackInvoked = true;
                using var db = f.Database.Factory.GetDatabase();
                var owner = (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner;
                Assert.False(string.IsNullOrWhiteSpace(owner));
                await using var manager = CheckoutPaymentOperationScope.BeginManagerOverride(f.OrderId);
                using var activation = manager.Enter();

                await Assert.ThrowsAnyAsync<EkomHttpException>(() => service.EnsureWriteAllowedAsync(db, f.OrderId));

                Assert.Equal(owner, (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
                var order = await f.ReadAsync();
                Assert.Null(order.PaidDate);
                Assert.Equal("Pending", order.OrderStatusCol);
            });
        var state = (await service.GetManagerReleaseStateAsync(f.OrderId, default))!;

        Assert.True(await service.ReleaseForManagerAsync(f.OrderId, "main", state.Token, false, "Operator", default));

        Assert.True(callbackInvoked);
        Assert.Equal("Incomplete", (await f.ReadAsync()).OrderStatusCol);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
    }

    [Theory]
    [InlineData("PaidDate")]
    [InlineData("Closed")]
    [InlineData("CompletionReceipt")]
    public async Task ManagerReleaseSaveCannotOverwritePaymentRecordedDuringExternalRelease(string paidState)
    {
        using var f = new Fixture();
        await f.SubmittedAsync(claimed: true);
        var before = await f.ReadAsync();
        var adapter = Adapter();
        var service = f.NewService(adapter.Object);
        var callbackInvoked = false;
        adapter.Setup(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                callbackInvoked = true;
                // Simulate a legacy/out-of-band writer which bypasses normal ownership checks.
                using var db = f.Database.Factory.GetDatabase();
                if (paidState == "PaidDate")
                    await db.GetTable<OrderData>().Set(x => x.PaidDate, (DateTime?)DateTime.UtcNow).UpdateAsync();
                else if (paidState == "Closed")
                    await db.GetTable<OrderData>().Set(x => x.OrderStatusCol, "Closed").UpdateAsync();
                else
                    await db.InsertAsync(new CheckoutStockCompletionData { OrderId = f.OrderId, CompletedUtc = DateTime.UtcNow });
            });
        var provider = Provider(service);
        var action = (await provider.GetActionsAsync(new OrderInfo(before)))
            .Single(x => x.Key.StartsWith("ekom.release-checkout:", StringComparison.Ordinal));

        Assert.IsType<OrderManagerActionBadRequestResult>(await provider.ExecuteAsync(new OrderInfo(before), action.Key, "Operator"));

        Assert.True(callbackInvoked);
        var after = await f.ReadAsync();
        Assert.Equal(before.OrderInfo, after.OrderInfo);
        Assert.Equal(paidState == "Closed" ? "Closed" : before.OrderStatusCol, after.OrderStatusCol);
        if (paidState == "PaidDate") Assert.NotNull(after.PaidDate);
        using var read = f.Database.Factory.GetDatabase();
        if (paidState == "CompletionReceipt")
            Assert.True(await read.GetTable<CheckoutStockCompletionData>().AnyAsync(x => x.OrderId == f.OrderId));
        Assert.Equal(CheckoutPaymentAttemptState.ReleasePending, (await read.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        Assert.DoesNotContain(await read.GetTable<OrderActivityLog>().ToListAsync(),
            x => x.Log.Contains("Manual release completed", StringComparison.Ordinal));
    }

    private static ReservationReleaseManagerActionProvider Provider(CheckoutPaymentAttemptService service)
        => new(service, NullLogger<ReservationReleaseManagerActionProvider>.Instance);

    private static Mock<ICheckoutGiftcardReservations> Adapter()
    {
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return adapter;
    }

    private static async Task<Guid> SeedLegacyAsync(Fixture f)
    {
        var key = await f.Database.SeedAsync(10);
        var reservation = await f.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = key, Quantity = 3, OrderId = f.OrderId.ToString(),
        });
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        json["ReservationIds"] = new JArray(reservation.ReservationId);
        json["HangfireJobs"] = new JArray(reservation.ReservationId);
        json["Giftcards"] = JArray.FromObject(new[] { new Giftcard
        {
            Code = "secret-code", Amount = 5, ClaimId = "legacy-claim", ValidUntil = DateTime.UtcNow.AddYears(1),
        } });
        json["Giftcards"]![0]!["HoldValidUntil"] = DateTime.UtcNow.AddMinutes(10);
        json["Giftcards"]![0]!["holdValidUntil"] = DateTime.UtcNow.AddMinutes(10);
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId)
            .Set(x => x.OrderInfo, json.ToString(Formatting.None)).Set(x => x.OrderStatusCol, "Pending").UpdateAsync();
        return key;
    }

    private static void AssertReleasedSelection(JObject before, JObject after)
    {
        var selected = before["Giftcards"]!.Single()!;
        var released = after["Giftcards"]!.Single()!;
        foreach (var field in new[] { "Code", "Amount", "ValidUntil" })
            Assert.True(JToken.DeepEquals(selected[field], released[field]), field + " must be preserved");
        Assert.False(released["Claimed"]!.Value<bool>());
        foreach (var field in new[] { "ClaimId", "ClaimDate", "TransactionId", "UsedDate", "HoldValidUntil", "holdValidUntil" })
            Assert.True(released[field] == null || released[field]!.Type == JTokenType.Null, field + " must be cleared");
    }
}
