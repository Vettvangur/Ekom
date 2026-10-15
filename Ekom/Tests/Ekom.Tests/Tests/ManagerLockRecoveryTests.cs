using Ekom.Controllers;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class ManagerLockRecoveryTests
{
    private const string Editor = "recovery-editor";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResetOverridesBothOwnersPreservesPurchaseAndFreezesRecoveryHistory(bool preparing)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync(claimed: true);
        var state = preparing ? CheckoutPaymentAttemptState.Preparing : CheckoutPaymentAttemptState.ReconciliationRequired;
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<CheckoutPaymentAttemptData>().Set(x => x.State, state).UpdateAsync();
        if (preparing)
            await db.GetTable<CheckoutPaymentAttemptData>().Set(x => x.SubmittedOrderInfo, (string?)null)
                .Set(x => x.SubmittedOrderData, (string?)null).UpdateAsync();
        await db.GetTable<CheckoutPaymentOperationData>().Set(x => x.Owner, "payment-secret")
            .Set(x => x.ReconciliationRequired, true).UpdateAsync();
        await db.InsertOrReplaceAsync(new CheckoutPreparationData
        {
            OrderId = f.OrderId, Owner = "preparation-secret", ProtectedIds = interrupted.ReservationIds,
        });
        var before = JObject.Parse((await f.ReadAsync()).OrderInfo);
        before["OrderLines"] = JArray.Parse("[{\"Key\":\"retained-line\",\"Quantity\":3}]");
        before["Customer"] = JObject.Parse("{\"Name\":\"Retained customer\"}");
        before["Giftcards"]![0]!["HoldValidUntil"] = DateTime.UtcNow.AddMinutes(20);
        await SaveJsonAsync(f, before);
        await db.GetTable<OrderData>().Set(x => x.CustomerName, "Retained customer")
            .Set(x => x.CustomerEmail, "customer@example.test").UpdateAsync();
        var snapshot = (await f.ReadAsync()).OrderInfo;
        var adapter = ReleasingAdapter();

        var result = await ResetAsync(f, f.NewService(adapter.Object));

        Assert.Null(result.Warning);
        await AssertUnlockedAsync(f);
        var current = await f.ReadAsync();
        Assert.Equal("Incomplete", current.OrderStatusCol);
        Assert.Equal("Retained customer", current.CustomerName);
        Assert.Equal("customer@example.test", current.CustomerEmail);
        var after = JObject.Parse(current.OrderInfo);
        Assert.True(JToken.DeepEquals(before["OrderLines"], after["OrderLines"]));
        Assert.True(JToken.DeepEquals(before["Customer"], after["Customer"]));
        Assert.Empty(after["ReservationIds"]!);
        Assert.Empty(after["HangfireJobs"]!);
        var card = after["Giftcards"]![0]!;
        foreach (var field in new[] { "Code", "Amount", "ValidUntil" })
            Assert.True(JToken.DeepEquals(before["Giftcards"]![0]![field], card[field]));
        Assert.False(card["Claimed"]!.Value<bool>());
        Assert.Equal(JTokenType.Null, card["ClaimId"]!.Type);
        Assert.Equal(JTokenType.Null, card["HoldValidUntil"]!.Type);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        var attempts = await db.GetTable<CheckoutPaymentAttemptData>().ToListAsync();
        Assert.Equal(2, attempts.Count);
        Assert.All(attempts, x => Assert.Equal(CheckoutPaymentAttemptState.Released, x.State));
        var recovery = Assert.Single(attempts, x => x.AttemptId != interrupted.AttemptId);
        Assert.Equal(snapshot, recovery.SubmittedOrderInfo);
        Assert.Equal(preparing ? null : interrupted.SubmittedOrderInfo, attempts.Single(x => x.AttemptId == interrupted.AttemptId).SubmittedOrderInfo);
        var metadata = JObject.Parse(recovery.SubmittedOrderData!)["ManagerRecovery"]!;
        Assert.Equal(interrupted.AttemptId, metadata["InterruptedAttemptId"]!.ToObject<Guid>());
        Assert.Equal(Editor, metadata["Editor"]!.Value<string>());
        Assert.Equal(interrupted.ReservationIds, metadata["ProtectedIds"]!.Value<string>());
        Assert.Equal(recovery.AttemptId, (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).ActiveAttemptId);
        var logs = await db.GetTable<OrderActivityLog>().ToListAsync();
        Assert.Contains(logs, x => x.UserName == Editor && x.Log.Contains("completed", StringComparison.Ordinal)
            && x.Log.Contains(recovery.AttemptId.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(logs, x => x.Log.Contains("secret-code", StringComparison.Ordinal)
            || x.Log.Contains("payment-secret", StringComparison.Ordinal) || x.Log.Contains("preparation-secret", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrFailingProviderStillResetsLocallyAndPreservesWarningAndClaimHistory(bool throws)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync(claimed: true);
        var adapter = ReleasingAdapter();
        if (throws)
            adapter.Setup(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("secret-code provider error"));

        var result = await ResetAsync(f, f.NewService(throws ? adapter.Object : null));

        Assert.False(string.IsNullOrWhiteSpace(result.Warning));
        Assert.DoesNotContain("secret-code", result.Warning!, StringComparison.Ordinal);
        Assert.Equal("Incomplete", (await f.ReadAsync()).OrderStatusCol);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        await AssertUnlockedAsync(f);
        using var db = f.Database.Factory.GetDatabase();
        var recovery = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId != interrupted.AttemptId);
        Assert.Equal(interrupted.SubmittedOrderInfo, recovery.SubmittedOrderInfo);
        var metadata = JObject.Parse(recovery.SubmittedOrderData!)["ManagerRecovery"]!;
        Assert.Equal(result.Warning, metadata["Warning"]!.Value<string>());
        Assert.Equal("LocalResetGiftcardsRequireReview", metadata["Outcome"]!.Value<string>());
        Assert.Equal("claim", JObject.Parse(recovery.SubmittedOrderInfo!)["Giftcards"]![0]!["ClaimId"]!.Value<string>());
        Assert.Equal(JTokenType.Null, JObject.Parse((await f.ReadAsync()).OrderInfo)["Giftcards"]![0]!["ClaimId"]!.Type);
        Assert.DoesNotContain(await db.GetTable<OrderActivityLog>().ToListAsync(), x => x.Log.Contains("secret-code", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReleasesFrozenAndCurrentClaimsExactlyOnceWithTheirOriginalAttemptIdentity()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync(claimed: true);
        var current = JObject.Parse((await f.ReadAsync()).OrderInfo);
        ((JArray)current["Giftcards"]!).Add(JObject.FromObject(new Giftcard { Code = "new-selection", Amount = 2, ClaimId = "new-claim" }));
        // Duplicates across both snapshots and within the current selection must not call the provider twice.
        ((JArray)current["Giftcards"]!).Add(current["Giftcards"]![0]!.DeepClone());
        await SaveJsonAsync(f, current);
        var frozen = JObject.Parse(interrupted.SubmittedOrderInfo!);
        ((JArray)frozen["Giftcards"]!).Add(JObject.FromObject(new Giftcard { Code = "frozen-only", Amount = 1, ClaimId = "frozen-claim" }));
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<CheckoutPaymentAttemptData>().Where(x => x.AttemptId == interrupted.AttemptId)
            .Set(x => x.SubmittedOrderInfo, frozen.ToString(Formatting.None)).UpdateAsync();
        var calls = new List<(Guid Order, Guid Attempt, string Code, string? Claim)>();
        var adapter = ReleasingAdapter();
        adapter.Setup(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, IReadOnlyList<Giftcard>, CancellationToken>((order, attempt, cards, _) =>
            {
                var card = Assert.Single(cards);
                calls.Add((order, attempt, card.Code, card.ClaimId));
            }).Returns(Task.CompletedTask);

        await ResetAsync(f, f.NewService(adapter.Object));

        Assert.Equal(3, calls.Count);
        Assert.Contains((f.OrderId, interrupted.AttemptId, "secret-code", "claim"), calls);
        Assert.Contains((f.OrderId, interrupted.AttemptId, "frozen-only", "frozen-claim"), calls);
        Assert.Contains((f.OrderId, Guid.Empty, "new-selection", "new-claim"), calls);
    }

    [Fact]
    public async Task RedeemedCardsAreNotRefundedAndSettlementMetadataRemainsInRecoveryHistory()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync(claimed: true);
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        json["Giftcards"]![0]!["Claimed"] = true;
        json["Giftcards"]![0]!["TransactionId"] = "settled-transaction";
        json["Giftcards"]![0]!["UsedDate"] = DateTime.UtcNow;
        await SaveJsonAsync(f, json);
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<CheckoutPaymentAttemptData>().Set(x => x.SubmittedOrderInfo, json.ToString(Formatting.None)).UpdateAsync();
        var adapter = ReleasingAdapter();

        var result = await ResetAsync(f, f.NewService(adapter.Object));

        Assert.False(string.IsNullOrWhiteSpace(result.Warning));
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
        var recovery = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId != interrupted.AttemptId);
        Assert.True(JToken.DeepEquals(json, JObject.Parse(recovery.SubmittedOrderInfo!)));
        Assert.Equal("settled-transaction", JObject.Parse(recovery.SubmittedOrderInfo!)["Giftcards"]![0]!["TransactionId"]!.Value<string>());
        Assert.Equal(JTokenType.Null, JObject.Parse((await f.ReadAsync()).OrderInfo)["Giftcards"]![0]!["TransactionId"]!.Type);
        await AssertUnlockedAsync(f);
    }

    [Theory]
    [InlineData("order")]
    [InlineData("owner")]
    [InlineData("attempt")]
    [InlineData("active-attempt")]
    [InlineData("preparation")]
    [InlineData("protected-ids")]
    public async Task StaleConfirmationRefusesChangedPersistedStateWithoutReleasingAnything(string changed)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var attempt = await f.SubmittedAsync(claimed: true);
        using var db = f.Database.Factory.GetDatabase();
        await db.InsertOrReplaceAsync(new CheckoutPreparationData { OrderId = f.OrderId, Owner = "original-owner" });
        var token = await TokenAsync(f);
        switch (changed)
        {
            case "order":
                await db.GetTable<OrderData>().Set(x => x.CustomerEmail, "changed@example.test").UpdateAsync();
                break;
            case "owner":
                await db.GetTable<CheckoutPaymentOperationData>().Set(x => x.Owner, "new-owner").UpdateAsync();
                break;
            case "attempt":
                await db.GetTable<CheckoutPaymentAttemptData>().Set(x => x.State, CheckoutPaymentAttemptState.Preparing).UpdateAsync();
                break;
            case "active-attempt":
                await db.GetTable<CheckoutPaymentOperationData>().Set(x => x.ActiveAttemptId, (Guid?)Guid.NewGuid()).UpdateAsync();
                break;
            case "preparation":
                await db.GetTable<CheckoutPreparationData>().Set(x => x.Owner, "new-preparation").UpdateAsync();
                break;
            case "protected-ids":
                await db.GetTable<CheckoutPreparationData>().Set(x => x.ProtectedIds, "[\"changed-protected-id\"]").UpdateAsync();
                break;
        }
        var before = JsonConvert.SerializeObject(await f.ReadAsync());
        var adapter = ReleasingAdapter();

        var error = await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.NewService(adapter.Object)
            .ReleaseOrderLockAsync(f.OrderId, token, ["main"], Editor));

        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
        Assert.Equal(before, JsonConvert.SerializeObject(await f.ReadAsync()));
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        Assert.Equal(attempt.AttemptId, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).AttemptId);
        Assert.Equal(changed == "preparation" ? "new-preparation" : "original-owner", (await db.GetTable<CheckoutPreparationData>().SingleAsync()).Owner);
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("closed")]
    [InlineData("completed")]
    [InlineData("receipt")]
    [InlineData("consumed")]
    public async Task PaidOrConsumedCheckoutIsRefusedWithoutFalseStockRestoration(string blocker)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var attempt = await f.SubmittedAsync();
        var token = await TokenAsync(f);
        using var db = f.Database.Factory.GetDatabase();
        switch (blocker)
        {
            case "paid":
                await db.GetTable<OrderData>().Set(x => x.PaidDate, (DateTime?)DateTime.UtcNow).UpdateAsync();
                break;
            case "closed":
                await db.GetTable<OrderData>().Set(x => x.OrderStatusCol, "Closed").UpdateAsync();
                break;
            case "completed":
                await db.GetTable<CheckoutPaymentAttemptData>().Set(x => x.State, CheckoutPaymentAttemptState.Completed).UpdateAsync();
                break;
            case "receipt":
                await db.InsertAsync(new CheckoutStockCompletionData { OrderId = f.OrderId, CompletedUtc = DateTime.UtcNow });
                break;
            case "consumed":
                await f.Database.Service.ConsumeAsync(JsonConvert.DeserializeObject<List<string>>(attempt.ReservationIds)!.Single());
                break;
        }
        var status = await StatusAsync(f);
        Assert.False(status.CanRelease);
        Assert.Null(status.ReleaseToken);

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.ReleaseOrderLockAsync(f.OrderId, token, ["main"], Editor));

        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        Assert.Single(await db.GetTable<CheckoutPaymentAttemptData>().ToListAsync());
        Assert.DoesNotContain(await db.GetTable<OrderActivityLog>().ToListAsync(), x => x.Log.Contains("recovery completed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReferencedOtherOrdersReservationIsRefusedBeforeAcquiringEitherFence()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync();
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<CheckoutPaymentOperationData>().Set(x => x.Owner, "existing-payment-owner").UpdateAsync();
        await db.InsertOrReplaceAsync(new CheckoutPreparationData { OrderId = f.OrderId, Owner = "existing-preparation-owner" });
        var oldToken = await TokenAsync(f);
        var otherKey = await f.Database.SeedAsync(10);
        var other = await f.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = otherKey, Quantity = 2, OrderId = Guid.NewGuid().ToString(),
        });
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        ((JArray)json["ReservationIds"]!).Add(other.ReservationId);
        await SaveJsonAsync(f, json);
        var before = (await f.ReadAsync()).OrderInfo;
        var stock = new Mock<IStockReservationService>(MockBehavior.Strict);
        var status = await StatusAsync(f);
        Assert.False(status.CanRelease);
        Assert.Null(status.ReleaseToken);

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.NewService(stock: stock.Object)
            .ReleaseOrderLockAsync(f.OrderId, oldToken, ["main"], Editor));

        Assert.Equal(8, await f.Database.StockAsync(otherKey));
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        Assert.Equal(StockReservationState.Active, (await db.GetTable<StockReservationData>().SingleAsync(x => x.Id == other.ReservationId)).State);
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleAsync();
        Assert.Equal("existing-payment-owner", operation.Owner);
        Assert.Equal(interrupted.AttemptId, operation.ActiveAttemptId);
        Assert.Equal("existing-preparation-owner", (await db.GetTable<CheckoutPreparationData>().SingleAsync()).Owner);
        Assert.Equal(CheckoutPaymentAttemptState.Submitted, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        Assert.Equal(before, (await f.ReadAsync()).OrderInfo);
        Assert.Equal("Pending", (await f.ReadAsync()).OrderStatusCol);
        stock.VerifyNoOtherCalls();
        Assert.DoesNotContain(await db.GetTable<OrderActivityLog>().ToListAsync(), x => x.Log.Contains("recovery completed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StockReleaseDeduplicatesCurrentFrozenAndPreparationReferences()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync();
        var frozenId = JsonConvert.DeserializeObject<List<string>>(interrupted.ReservationIds)!.Single();
        var currentKey = await f.Database.SeedAsync(10);
        var currentHold = await f.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = currentKey, Quantity = 2, OrderId = f.OrderId.ToString(),
        });
        var currentId = Assert.IsType<string>(currentHold.ReservationId);
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        json["ReservationIds"] = new JArray(currentId, currentId);
        json["HangfireJobs"] = new JArray(currentId);
        await SaveJsonAsync(f, json);
        using var db = f.Database.Factory.GetDatabase();
        await db.InsertOrReplaceAsync(new CheckoutPreparationData
        {
            OrderId = f.OrderId, Owner = "preparation", ProtectedIds = JsonConvert.SerializeObject(new[] { frozenId, currentId, frozenId }),
        });
        var stock = new Mock<IStockReservationService>();
        stock.Setup(x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken ct) => f.Database.Service.ReleaseAsync(id, ct));

        await ResetAsync(f, f.NewService(stock: stock.Object));

        stock.Verify(x => x.ReleaseAsync(frozenId, It.IsAny<CancellationToken>()), Times.Once);
        stock.Verify(x => x.ReleaseAsync(currentId, It.IsAny<CancellationToken>()), Times.Once);
        stock.VerifyNoOtherCalls();
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        Assert.Equal(10, await f.Database.StockAsync(currentKey));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreparationOnlyAndReconciliationOnlyLocksCanBeRecovered(bool preparationOnly)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        using var db = f.Database.Factory.GetDatabase();
        if (preparationOnly)
            await db.InsertOrReplaceAsync(new CheckoutPreparationData { OrderId = f.OrderId, Owner = "orphan-preparation" });
        else
            await db.InsertAsync(new CheckoutPaymentOperationData { OrderId = f.OrderId, ReconciliationRequired = true });

        await ResetAsync(f);

        await AssertUnlockedAsync(f);
        Assert.Equal("Incomplete", (await f.ReadAsync()).OrderStatusCol);
        Assert.Equal(CheckoutPaymentAttemptState.Released, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
    }

    [Fact]
    public async Task LateInterruptedCallbackCannotCompleteRecoveredOrder()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync();
        await ResetAsync(f);
        var invoked = false;

        Assert.False(await f.Service.CompleteAsync(f.OrderId, interrupted.AttemptId, () =>
        {
            invoked = true;
            return Task.CompletedTask;
        }));

        Assert.False(invoked);
        Assert.Equal("Incomplete", (await f.ReadAsync()).OrderStatusCol);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryBeginsFreshAttemptWithRenewedClaimsOrWithoutCards(bool keepCards)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync(claimed: true);
        var adapter = ReleasingAdapter();
        adapter.Setup(x => x.ReserveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, IReadOnlyList<Giftcard> cards, CancellationToken _) =>
                (IReadOnlyList<Giftcard>)cards.Select(x => new Giftcard { Code = x.Code, Amount = x.Amount, ClaimId = "fresh-claim", ValidUntil = x.ValidUntil }).ToArray());
        var service = f.NewService(adapter.Object);
        await ResetAsync(f, service);
        if (!keepCards)
        {
            var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
            json["Giftcards"] = new JArray();
            await SaveJsonAsync(f, json);
        }
        var order = new OrderInfo(await f.ReadAsync());

        await using var retry = await (keepCards ? service : f.Service).BeginCheckoutAsync(order);

        Assert.NotEqual(interrupted.AttemptId, retry.AttemptId);
        if (keepCards)
        {
            Assert.Equal("fresh-claim", Assert.Single(order.Giftcards).ClaimId);
            adapter.Verify(x => x.ReserveAsync(f.OrderId, retry.AttemptId!.Value,
                It.Is<IReadOnlyList<Giftcard>>(cards => cards.Count == 1 && cards[0].ClaimId == null && cards[0].Code == "secret-code"), It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            Assert.Empty(order.Giftcards);
            adapter.Verify(x => x.ReserveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task DuplicateConfirmationHasNoRepeatedEffectsAndCannotResetNewCheckoutOwner()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await f.SubmittedAsync(claimed: true);
        var adapter = ReleasingAdapter();
        var service = f.NewService(adapter.Object);
        var token = await TokenAsync(f);
        await service.ReleaseOrderLockAsync(f.OrderId, token, ["main"], Editor);

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => service.ReleaseOrderLockAsync(f.OrderId, token, ["main"], Editor));
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        json["Giftcards"] = new JArray();
        await SaveJsonAsync(f, json);
        await using var retry = await f.Service.BeginCheckoutAsync(new OrderInfo(await f.ReadAsync()));

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => service.ReleaseOrderLockAsync(f.OrderId, token, ["main"], Editor));

        using var db = f.Database.Factory.GetDatabase();
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleAsync();
        Assert.Equal(retry.Owner, operation.Owner);
        Assert.Equal(retry.AttemptId, operation.ActiveAttemptId);
        Assert.Equal(3, await db.GetTable<CheckoutPaymentAttemptData>().CountAsync());
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuditPersistenceFailurePreventsRecoveryAcquisitionAndAllExternalEffects()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync(claimed: true);
        var token = await TokenAsync(f);
        using var db = f.Database.Factory.GetDatabase();
        db.Execute("CREATE TRIGGER FailManagerRecoveryAudit BEFORE INSERT ON EkomOrdersActivityLog BEGIN SELECT RAISE(ABORT, 'audit unavailable'); END");
        var adapter = ReleasingAdapter();

        await Assert.ThrowsAnyAsync<Exception>(() => f.NewService(adapter.Object).ReleaseOrderLockAsync(f.OrderId, token, ["main"], Editor));

        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        Assert.Equal(interrupted.SubmittedOrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.Equal(CheckoutPaymentAttemptState.Submitted, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StockReleaseFailurePreservesRecoveryFenceAndOriginalClaimMetadata()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync(claimed: true);
        var stock = new Mock<IStockReservationService>();
        stock.Setup(x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("stock unavailable"));

        await Assert.ThrowsAnyAsync<Exception>(() => ResetAsync(f, f.NewService(stock: stock.Object)));

        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        await AssertFailedFenceAsync(f, interrupted);
    }

    [Fact]
    public async Task FreshConfirmationAfterTransientStockFailureReleasesOriginalFrozenOnlyClaimWithOriginalAttemptId()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync(claimed: true);
        var current = JObject.Parse((await f.ReadAsync()).OrderInfo);
        current["Giftcards"] = new JArray();
        current["ReservationIds"] = new JArray();
        current["HangfireJobs"] = new JArray();
        await SaveJsonAsync(f, current);
        var initialToken = await TokenAsync(f);
        var stock = new Mock<IStockReservationService>();
        stock.Setup(x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transient stock failure"));
        var adapter = ReleasingAdapter();

        await Assert.ThrowsAnyAsync<Exception>(() => f.NewService(adapter.Object, stock.Object)
            .ReleaseOrderLockAsync(f.OrderId, initialToken, ["main"], Editor));

        using var db = f.Database.Factory.GetDatabase();
        var firstRecovery = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId != interrupted.AttemptId);
        var fenced = await db.GetTable<CheckoutPaymentOperationData>().SingleAsync();
        Assert.NotNull(fenced.Owner);
        Assert.Equal(firstRecovery.AttemptId, fenced.ActiveAttemptId);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        Assert.Equal("Pending", (await f.ReadAsync()).OrderStatusCol);
        Assert.Empty(JObject.Parse(firstRecovery.SubmittedOrderInfo!)["Giftcards"]!);
        var original = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == interrupted.AttemptId);
        Assert.Equal(interrupted.SubmittedOrderInfo, original.SubmittedOrderInfo);
        Assert.Equal(interrupted.SubmittedOrderData, original.SubmittedOrderData);
        Assert.Equal("claim", JObject.Parse(original.SubmittedOrderInfo!)["Giftcards"]![0]!["ClaimId"]!.Value<string>());
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
        var freshToken = await TokenAsync(f);
        Assert.NotEqual(initialToken, freshToken);
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.NewService(adapter.Object)
            .ReleaseOrderLockAsync(f.OrderId, initialToken, ["main"], Editor));
        Assert.Equal(fenced.Owner, (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);

        var result = await ResetAsync(f, f.NewService(adapter.Object));

        Assert.Null(result.Warning);
        await AssertUnlockedAsync(f);
        Assert.Equal("Incomplete", (await f.ReadAsync()).OrderStatusCol);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, interrupted.AttemptId,
            It.Is<IReadOnlyList<Giftcard>>(cards => cards.Count == 1 && cards[0].Code == "secret-code" && cards[0].ClaimId == "claim"),
            It.IsAny<CancellationToken>()), Times.Once);
        adapter.VerifyNoOtherCalls();
        var attempts = await db.GetTable<CheckoutPaymentAttemptData>().ToListAsync();
        Assert.Equal(3, attempts.Count);
        var secondRecovery = Assert.Single(attempts, x => x.AttemptId != interrupted.AttemptId && x.AttemptId != firstRecovery.AttemptId);
        Assert.Equal(firstRecovery.AttemptId, JObject.Parse(secondRecovery.SubmittedOrderData!)["ManagerRecovery"]!["InterruptedAttemptId"]!.ToObject<Guid>());
        Assert.Equal(interrupted.AttemptId, JObject.Parse(firstRecovery.SubmittedOrderData!)["ManagerRecovery"]!["InterruptedAttemptId"]!.ToObject<Guid>());
        Assert.Equal(interrupted.SubmittedOrderInfo, attempts.Single(x => x.AttemptId == interrupted.AttemptId).SubmittedOrderInfo);
        // Replaying the previous recovery confirmation cannot restore the stock twice.
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.ReleaseOrderLockAsync(f.OrderId, freshToken, ["main"], Editor));
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UncertainAcquisitionCommitNeverReportsSuccessOrReleasesStock(bool committed)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync(claimed: true);
        var token = await TokenAsync(f);
        var fired = false;
        var recoveryInserted = false;
        using var fault = new TraceFault(f, info =>
        {
            if (info.TraceInfoStep == TraceInfoStep.AfterExecute &&
                info.SqlText?.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase) == true &&
                info.SqlText.Contains("EkomCheckoutPaymentAttempt", StringComparison.Ordinal))
                recoveryInserted = true;
            if (fired || !recoveryInserted || info.Operation != TraceOperation.CommitTransaction ||
                info.TraceInfoStep != (committed ? TraceInfoStep.AfterExecute : TraceInfoStep.BeforeExecute)) return;
            fired = true;
            throw new InvalidOperationException("uncertain acquisition commit");
        });

        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.ReleaseOrderLockAsync(f.OrderId, token, ["main"], Editor));

        Assert.True(fired);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        Assert.Equal(interrupted.SubmittedOrderInfo, (await f.ReadAsync()).OrderInfo);
        if (committed) await AssertFailedFenceAsync(f, interrupted);
        else
        {
            using var db = f.Database.Factory.GetDatabase();
            Assert.Single(await db.GetTable<CheckoutPaymentAttemptData>().ToListAsync());
            Assert.Null((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        }
    }

    [Fact]
    public async Task LocalResetCommitFailureRetainsFenceAndFreshConfirmationCanFinishRecovery()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync(claimed: true);
        var originalToken = await TokenAsync(f);
        var adapter = ReleasingAdapter();
        var releaseFinished = false;
        adapter.Setup(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Callback(() => releaseFinished = true).Returns(Task.CompletedTask);
        var fired = false;
        using var fault = new TraceFault(f, info =>
        {
            if (fired || !releaseFinished || info.Operation != TraceOperation.CommitTransaction || info.TraceInfoStep != TraceInfoStep.BeforeExecute) return;
            fired = true;
            throw new InvalidOperationException("local reset commit unavailable");
        });

        await Assert.ThrowsAnyAsync<Exception>(() => ResetAsync(f, f.NewService(adapter.Object)));

        Assert.True(fired);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        await AssertFailedFenceAsync(f, interrupted);
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, interrupted.AttemptId, It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotEqual(originalToken, await TokenAsync(f));

        var result = await ResetAsync(f, f.NewService(adapter.Object));

        Assert.Null(result.Warning);
        await AssertUnlockedAsync(f);
        Assert.Equal("Incomplete", (await f.ReadAsync()).OrderStatusCol);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, interrupted.AttemptId, It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        adapter.VerifyNoOtherCalls();
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(3, await db.GetTable<CheckoutPaymentAttemptData>().CountAsync());
        Assert.Single(await db.GetTable<OrderActivityLog>().Where(x => x.Log.Contains("recovery completed")).ToListAsync());
    }

    [Fact]
    public async Task FinalAuditFailureRollsBackResetRetainsClaimsAndAllowsFreshConfirmedRetry()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var interrupted = await f.SubmittedAsync(claimed: true);
        using var db = f.Database.Factory.GetDatabase();
        db.Execute("CREATE TRIGGER FailManagerRecoveryCompletion BEFORE INSERT ON EkomOrdersActivityLog WHEN NEW.Log LIKE 'Manager lock recovery completed%' BEGIN SELECT RAISE(ABORT, 'completion audit unavailable'); END");
        var adapter = ReleasingAdapter();

        await Assert.ThrowsAnyAsync<Exception>(() => ResetAsync(f, f.NewService(adapter.Object)));

        await AssertFailedFenceAsync(f, interrupted);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        var firstRecovery = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId != interrupted.AttemptId);
        Assert.Equal("Acquired", JObject.Parse(firstRecovery.SubmittedOrderData!)["ManagerRecovery"]!["Outcome"]!.Value<string>());
        Assert.Equal("Pending", (await f.ReadAsync()).OrderStatusCol);
        Assert.Equal("claim", JObject.Parse((await f.ReadAsync()).OrderInfo)["Giftcards"]![0]!["ClaimId"]!.Value<string>());
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, interrupted.AttemptId, It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Once);
        db.Execute("DROP TRIGGER FailManagerRecoveryCompletion");

        var result = await ResetAsync(f, f.NewService(adapter.Object));

        Assert.Null(result.Warning);
        await AssertUnlockedAsync(f);
        Assert.Equal("Incomplete", (await f.ReadAsync()).OrderStatusCol);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        Assert.Equal(JTokenType.Null, JObject.Parse((await f.ReadAsync()).OrderInfo)["Giftcards"]![0]!["ClaimId"]!.Type);
        Assert.Equal(interrupted.SubmittedOrderInfo, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == interrupted.AttemptId)).SubmittedOrderInfo);
        Assert.Equal(interrupted.SubmittedOrderInfo, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == firstRecovery.AttemptId)).SubmittedOrderInfo);
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, interrupted.AttemptId, It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        adapter.VerifyNoOtherCalls();
        Assert.Single(await db.GetTable<OrderActivityLog>().Where(x => x.Log.Contains("recovery completed")).ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ServiceAuthorizesPersistedStoreBeforeAnyRecoveryEffects(bool allowed)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await f.SubmittedAsync();
        var token = await TokenAsync(f);
        if (allowed)
            await f.Service.ReleaseOrderLockAsync(f.OrderId, token, ["MAIN"], Editor);
        else
        {
            var error = await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.ReleaseOrderLockAsync(f.OrderId, token, ["other-store"], Editor));
            Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
            Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
            using var db = f.Database.Factory.GetDatabase();
            Assert.Empty(await db.GetTable<OrderActivityLog>().ToListAsync());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControllerAuthorizesStoreAndPassesReleaseCapabilityToService(bool allowed)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessStore("main")).Returns(allowed);
        access.Setup(x => x.GetAllowedStoreAliases()).Returns(["main"]);
        var locks = new Mock<IOrderManagerLockService>();
        locks.Setup(x => x.ReleaseOrderLockAsync(f.OrderId, "confirmed-token", It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderLockReleaseResponse("released", null));
        var controller = Controller(f, access.Object);

        var result = await controller.ReleaseOrderLockAsync(f.OrderId, new OrderLockReleaseRequest { ReleaseToken = "confirmed-token" }, locks.Object);

        if (allowed)
        {
            Assert.IsType<OrderLockReleaseResponse>(Assert.IsType<OkObjectResult>(result).Value);
            locks.Verify(x => x.ReleaseOrderLockAsync(f.OrderId, "confirmed-token", It.Is<IReadOnlyCollection<string>>(stores => stores.Contains("main")), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            Assert.IsType<ForbidResult>(result);
            locks.VerifyNoOtherCalls();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ControllerRejectsMissingBodyOrBlankTokenWithoutCallingService(string? token)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var locks = new Mock<IOrderManagerLockService>();
        var controller = Controller(f, Mock.Of<IManagerAccessService>());

        var result = await controller.ReleaseOrderLockAsync(f.OrderId,
            token == null ? null : new OrderLockReleaseRequest { ReleaseToken = token }, locks.Object);

        Assert.IsType<BadRequestObjectResult>(result);
        locks.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ControllerReturnsConflictForRejectedConfirmationToken()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessStore("main")).Returns(true);
        access.Setup(x => x.GetAllowedStoreAliases()).Returns(["main"]);
        var locks = new Mock<IOrderManagerLockService>();
        locks.Setup(x => x.ReleaseOrderLockAsync(f.OrderId, "stale-token", It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EkomHttpException(HttpStatusCode.Conflict, "Refresh and confirm again."));

        var result = await Controller(f, access.Object).ReleaseOrderLockAsync(f.OrderId,
            new OrderLockReleaseRequest { ReleaseToken = "stale-token" }, locks.Object);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    private static Mock<ICheckoutGiftcardReservations> ReleasingAdapter()
    {
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return adapter;
    }

    private static async Task<OrderLockReleaseResponse> ResetAsync(CheckoutPaymentAttemptTests.Fixture f, IOrderManagerLockService? service = null)
        => await (service ?? f.Service).ReleaseOrderLockAsync(f.OrderId, await TokenAsync(f), ["main"], Editor);

    private static async Task<string> TokenAsync(CheckoutPaymentAttemptTests.Fixture f)
    {
        var status = await StatusAsync(f);
        Assert.True(status.CanRelease, status.ReleaseUnavailableReason);
        return Assert.IsType<string>(status.ReleaseToken);
    }

    private static async Task<OrderLockStatus> StatusAsync(CheckoutPaymentAttemptTests.Fixture f)
    {
        using var db = f.Database.Factory.GetDatabase();
        return await CheckoutPaymentAttemptService.ReadManagerLockStatusAsync(db, f.OrderId, CancellationToken.None);
    }

    private static async Task SaveJsonAsync(CheckoutPaymentAttemptTests.Fixture f, JObject json)
    {
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId)
            .Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
    }

    private static async Task AssertUnlockedAsync(CheckoutPaymentAttemptTests.Fixture f)
    {
        using var db = f.Database.Factory.GetDatabase();
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleAsync();
        Assert.Null(operation.Owner);
        Assert.False(operation.ReconciliationRequired);
        var preparation = await db.GetTable<CheckoutPreparationData>().SingleAsync();
        Assert.Null(preparation.Owner);
        Assert.Empty(JsonConvert.DeserializeObject<List<string>>(preparation.ProtectedIds)!);
    }

    private static async Task AssertFailedFenceAsync(CheckoutPaymentAttemptTests.Fixture f, CheckoutPaymentAttemptData interrupted)
    {
        using var db = f.Database.Factory.GetDatabase();
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleAsync();
        var preparation = await db.GetTable<CheckoutPreparationData>().SingleAsync();
        Assert.NotNull(operation.Owner);
        Assert.Equal(operation.Owner, preparation.Owner);
        var recovery = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId != interrupted.AttemptId);
        Assert.Equal(recovery.AttemptId, operation.ActiveAttemptId);
        Assert.Equal(CheckoutPaymentAttemptState.Released, recovery.State);
        Assert.Equal(interrupted.SubmittedOrderInfo, recovery.SubmittedOrderInfo);
        Assert.Equal(interrupted.SubmittedOrderInfo, (await f.ReadAsync()).OrderInfo);
        Assert.Contains("claim", recovery.SubmittedOrderInfo!, StringComparison.Ordinal);
        var status = await StatusAsync(f);
        Assert.True(status.CanRelease, status.ReleaseUnavailableReason);
        Assert.False(string.IsNullOrWhiteSpace(status.ReleaseToken));
        var logs = await db.GetTable<OrderActivityLog>().ToListAsync();
        Assert.DoesNotContain(logs, x => x.Log.Contains("recovery completed", StringComparison.Ordinal));
        Assert.Contains(logs, x => x.Log.Contains(recovery.AttemptId.ToString(), StringComparison.Ordinal));
    }

    private static ManagerRepository Repository(CheckoutPaymentAttemptTests.Fixture f)
        => new(NullLogger<ManagerRepository>.Instance,
            new Configuration(new ConfigurationBuilder().Build()), f.Database.Factory, Mock.Of<IStoreService>());

    private static EkomManagerController Controller(CheckoutPaymentAttemptTests.Fixture f, IManagerAccessService access)
        => new(Repository(f), access, Mock.Of<INodeService>(), Mock.Of<IOrderActivityLogService>(),
            Mock.Of<IOrderManagerActionService>(), null!, NullLogger<EkomManagerController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    // Connections are created inside the real SQLite fixture's factory. Restore
    // both global hooks and filter by connection string, as cancellation tests do.
    private sealed class TraceFault : IDisposable
    {
        private readonly Action<TraceInfo> _previous = DataConnection.DefaultOnTraceConnection;
        private readonly TraceLevel _level = DataConnection.TraceSwitch.Level;

        public TraceFault(CheckoutPaymentAttemptTests.Fixture f, Action<TraceInfo> action)
        {
            using var db = f.Database.Factory.GetDatabase();
            var connectionString = db.ConnectionString;
            DataConnection.TraceSwitch.Level = TraceLevel.Info;
            DataConnection.DefaultOnTraceConnection = info =>
            {
                if (info.DataConnection.ConnectionString == connectionString) action(info);
            };
        }

        public void Dispose()
        {
            DataConnection.DefaultOnTraceConnection = _previous;
            DataConnection.TraceSwitch.Level = _level;
        }
    }
}
