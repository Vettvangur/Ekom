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
public sealed class OrderManagerGiftcardReleaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleasesOnlySelectedClaimAndPreservesOtherCardsStockAndAttempt(bool preparing)
    {
        using var f = new Fixture();
        var attempt = await SeedAsync(f, preparing);
        var before = await f.ReadAsync();
        var adapter = Adapter();
        var service = f.NewService(adapter.Object);
        var provider = Provider(service);
        var order = new OrderInfo(before);
        var actions = await provider.GetActionsAsync(order);
        var action = actions.Single(x => x.GiftcardCode == "secret-code");
        Assert.True(action.Enabled);
        Assert.DoesNotContain("secret-code", action.Key, StringComparison.Ordinal);
        Assert.DoesNotContain("claim", action.Key, StringComparison.Ordinal);
        Assert.Equal(2, actions.Count(x => x.GiftcardCode != null));
        Assert.IsType<OrderManagerActionSuccessResult>(await provider.ExecuteAsync(order, action.Key, "Operator"));
        Assert.IsType<OrderManagerActionSuccessResult>(await provider.ExecuteAsync(order, action.Key, "Operator"));

        var after = await f.ReadAsync();
        var previous = JObject.Parse(before.OrderInfo);
        var saved = JObject.Parse(after.OrderInfo);
        Assert.True(JToken.DeepEquals(previous["Giftcards"]![1], saved["Giftcards"]![1]));
        Assert.Equal(JTokenType.Null, saved["Giftcards"]![0]!["ClaimId"]!.Type);
        Assert.Equal(JTokenType.Null, saved["Giftcards"]![0]!["HoldValidUntil"]!.Type);
        Assert.Equal("secret-code", saved["Giftcards"]![0]!["Code"]!.Value<string>());
        Assert.True(JToken.DeepEquals(previous["ReservationIds"], saved["ReservationIds"]));
        Assert.True(JToken.DeepEquals(previous["HangfireJobs"], saved["HangfireJobs"]));
        Assert.Equal(before.OrderStatusCol, after.OrderStatusCol);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, preparing ? attempt.AttemptId : Guid.Empty,
            It.Is<IReadOnlyList<Giftcard>>(cards => cards.Count == 1 && cards[0].Code == "secret-code" && cards[0].ClaimId == "claim"),
            It.IsAny<CancellationToken>()), Times.Once);
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(preparing ? CheckoutPaymentAttemptState.Preparing : CheckoutPaymentAttemptState.Released,
            (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        Assert.DoesNotContain(await db.GetTable<OrderActivityLog>().ToListAsync(), x => x.Log.Contains("secret-code", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("ReleasePending")]
    [InlineData("CompletionPending")]
    [InlineData("Completed")]
    [InlineData("ReconciliationRequired")]
    [InlineData("OperationReconciliation")]
    [InlineData("Paid")]
    [InlineData("Settled")]
    [InlineData("MissingAdapter")]
    [InlineData("NoClaim")]
    [InlineData("PreparingWithSubmittedSnapshot")]
    public async Task SelectedCardAlwaysHasAnExplainedDisabledActionAndCannotReleaseUnsafeState(string unsafeState)
    {
        using var f = new Fixture();
        await SeedAsync(f, true);
        var adapter = Adapter();
        var service = f.NewService(unsafeState == "MissingAdapter" ? null : adapter.Object);
        var provider = Provider(service);
        using var db = f.Database.Factory.GetDatabase();
        if (Enum.TryParse<CheckoutPaymentAttemptState>(unsafeState, out var state))
            await db.GetTable<CheckoutPaymentAttemptData>().Set(x => x.State, state).UpdateAsync();
        else if (unsafeState == "OperationReconciliation")
            await db.GetTable<CheckoutPaymentOperationData>().Set(x => x.ReconciliationRequired, true).UpdateAsync();
        else if (unsafeState == "Paid")
            await db.GetTable<OrderData>().Set(x => x.PaidDate, (DateTime?)DateTime.UtcNow).UpdateAsync();
        else if (unsafeState == "PreparingWithSubmittedSnapshot")
            await db.GetTable<CheckoutPaymentAttemptData>()
                .Set(x => x.SubmittedOrderInfo, (await f.ReadAsync()).OrderInfo).UpdateAsync();
        else if (unsafeState is "Settled" or "NoClaim")
        {
            var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
            if (unsafeState == "Settled") json["Giftcards"]![0]!["TransactionId"] = "transaction";
            else json["Giftcards"]![0]!["ClaimId"] = null;
            await db.GetTable<OrderData>().Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
        }
        var before = await f.ReadAsync();
        var action = (await provider.GetActionsAsync(new OrderInfo(before))).Single(x => x.GiftcardCode == "secret-code");
        Assert.False(action.Enabled);
        Assert.False(string.IsNullOrWhiteSpace(action.DisabledReason));
        var result = await provider.ExecuteAsync(new OrderInfo(before), action.Key);
        if (unsafeState == "NoClaim") Assert.IsType<OrderManagerActionSuccessResult>(result);
        else Assert.IsType<OrderManagerActionBadRequestResult>(result);
        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Claim")]
    [InlineData("Amount")]
    [InlineData("Submitted")]
    [InlineData("OtherCard")]
    [InlineData("Store")]
    public async Task StalePerCardActionCannotReleaseChangedPersistedState(string change)
    {
        using var f = new Fixture();
        await SeedAsync(f, true);
        var adapter = Adapter();
        var provider = Provider(f.NewService(adapter.Object));
        var order = new OrderInfo(await f.ReadAsync());
        var action = (await provider.GetActionsAsync(order)).Single(x => x.GiftcardCode == "secret-code");
        using var db = f.Database.Factory.GetDatabase();
        if (change == "Submitted")
            await db.GetTable<CheckoutPaymentAttemptData>().Set(x => x.State, CheckoutPaymentAttemptState.Submitted).UpdateAsync();
        else if (change == "Store")
            await db.GetTable<OrderData>().Set(x => x.StoreAlias, "other").UpdateAsync();
        else
        {
            var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
            if (change == "Amount") json["Giftcards"]![0]!["Amount"] = 99;
            else json["Giftcards"]![change == "OtherCard" ? 1 : 0]!["ClaimId"] = "replacement";
            await db.GetTable<OrderData>().Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
        }
        var before = await f.ReadAsync();
        Assert.IsType<OrderManagerActionBadRequestResult>(await provider.ExecuteAsync(order, action.Key));
        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PerCardActionCannotBorrowRealOwnersCapability()
    {
        using var f = new Fixture();
        await SeedAsync(f, false);
        var adapter = Adapter();
        var service = f.NewService(adapter.Object);
        var provider = Provider(service);
        var order = new OrderInfo(await f.ReadAsync());
        var action = (await provider.GetActionsAsync(order)).Single(x => x.GiftcardCode == "secret-code");
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<CheckoutPaymentOperationData>()
            .Set(x => x.Owner, "real-owner").UpdateAsync();
        var before = await f.ReadAsync();
        await using var manager = CheckoutPaymentOperationScope.BeginManagerOverride(f.OrderId);
        using var activation = manager.Enter();
        Assert.IsType<OrderManagerActionBadRequestResult>(await provider.ExecuteAsync(order, action.Key));
        Assert.Same(manager, CheckoutPaymentOperationScope.Current);
        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailedPerCardProviderReleasePreservesBothClaimsAndCanRetryOnlyThatCard()
    {
        using var f = new Fixture();
        var attempt = await SeedAsync(f, true);
        var adapter = Adapter();
        adapter.SetupSequence(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unavailable"))
            .Returns(Task.CompletedTask);
        var provider = Provider(f.NewService(adapter.Object));
        var before = await f.ReadAsync();
        var order = new OrderInfo(before);
        var action = (await provider.GetActionsAsync(order)).Single(x => x.GiftcardCode == "secret-code");
        Assert.IsType<OrderManagerActionBadRequestResult>(await provider.ExecuteAsync(order, action.Key));
        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.IsType<OrderManagerActionSuccessResult>(await provider.ExecuteAsync(order, action.Key));
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, attempt.AttemptId,
            It.Is<IReadOnlyList<Giftcard>>(cards => cards.Count == 1 && cards[0].Code == "secret-code"), It.IsAny<CancellationToken>()), Times.Exactly(2));
        var saved = JObject.Parse((await f.ReadAsync()).OrderInfo);
        Assert.True(JToken.DeepEquals(JObject.Parse(before.OrderInfo)["Giftcards"]![1], saved["Giftcards"]![1]));
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
    }

    [Fact]
    public async Task SettledOtherCardIsNotReleasedOrChangedByPerCardRelease()
    {
        using var f = new Fixture();
        await SeedAsync(f, false);
        using var db = f.Database.Factory.GetDatabase();
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        json["Giftcards"]![1]!["TransactionId"] = "settled-transaction";
        await db.GetTable<OrderData>().Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
        var provider = Provider(f.NewService(Adapter().Object));
        var order = new OrderInfo(await f.ReadAsync());
        var actions = await provider.GetActionsAsync(order);
        Assert.False(actions.Single(x => x.GiftcardCode == "second-secret").Enabled);
        var action = actions.Single(x => x.GiftcardCode == "secret-code");
        Assert.True(action.Enabled);
        Assert.IsType<OrderManagerActionSuccessResult>(await provider.ExecuteAsync(order, action.Key));
        Assert.True(JToken.DeepEquals(json["Giftcards"]![1], JObject.Parse((await f.ReadAsync()).OrderInfo)["Giftcards"]![1]));
    }

    [Fact]
    public async Task PerCardSaveCannotOverwritePaymentRecordedDuringExternalRelease()
    {
        using var f = new Fixture();
        await SeedAsync(f, true);
        var adapter = Adapter();
        adapter.Setup(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                using var db = f.Database.Factory.GetDatabase();
                await db.GetTable<OrderData>().Set(x => x.PaidDate, (DateTime?)DateTime.UtcNow).UpdateAsync();
            });
        var provider = Provider(f.NewService(adapter.Object));
        var before = await f.ReadAsync();
        var order = new OrderInfo(before);
        var action = (await provider.GetActionsAsync(order)).Single(x => x.GiftcardCode == "secret-code");
        Assert.IsType<OrderManagerActionBadRequestResult>(await provider.ExecuteAsync(order, action.Key));
        var saved = await f.ReadAsync();
        Assert.NotNull(saved.PaidDate);
        Assert.Equal(before.OrderInfo, saved.OrderInfo);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
    }

    [Fact]
    public async Task OldAggregateGiftcardActionIsRejectedRatherThanEscalated()
    {
        using var f = new Fixture();
        await f.SubmittedAsync(claimed: true);
        var service = f.NewService(Adapter().Object);
        var state = (await service.GetManagerReleaseStateAsync(f.OrderId, default))!;
        var before = await f.ReadAsync();
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => service.ReleaseForManagerAsync(f.OrderId, "main", state.Token, true, null, default));
        Assert.IsType<OrderManagerActionBadRequestResult>(await Provider(service).ExecuteAsync(new OrderInfo(before), "ekom.release-giftcards:" + state.Token));
        Assert.Equal(before.OrderInfo, (await f.ReadAsync()).OrderInfo);
    }

    private static async Task<CheckoutPaymentAttemptData> SeedAsync(Fixture f, bool preparing)
    {
        var attempt = await f.SubmittedAsync(claimed: true);
        using var db = f.Database.Factory.GetDatabase();
        // A genuinely unsubmitted Preparing attempt has no frozen payment snapshot.
        await db.GetTable<CheckoutPaymentAttemptData>()
            .Set(x => x.State, preparing ? CheckoutPaymentAttemptState.Preparing : CheckoutPaymentAttemptState.Released)
            .Set(x => x.SubmittedOrderInfo, preparing ? null : attempt.SubmittedOrderInfo)
            .Set(x => x.SubmittedOrderData, preparing ? null : attempt.SubmittedOrderData)
            .Set(x => x.SubmittedUtc, (DateTime?)null).UpdateAsync();
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        json["Giftcards"]![0]!["HoldValidUntil"] = DateTime.UtcNow.AddMinutes(10);
        ((JArray)json["Giftcards"]!).Add(JObject.FromObject(new Giftcard { Code = "second-secret", ClaimId = "second-claim", Amount = 3 }));
        await db.GetTable<OrderData>().Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
        return attempt;
    }

    private static Mock<ICheckoutGiftcardReservations> Adapter()
    {
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return adapter;
    }

    private static ReservationReleaseManagerActionProvider Provider(CheckoutPaymentAttemptService service)
        => new(service, NullLogger<ReservationReleaseManagerActionProvider>.Instance);
}
