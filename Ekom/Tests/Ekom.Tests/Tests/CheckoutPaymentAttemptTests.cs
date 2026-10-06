using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class CheckoutPaymentAttemptTests
{
    [Fact]
    public async Task SubmissionAlignsAllHoldsToOneDeadlineWithoutExtendingAnyHold()
    {
        using var f = new Fixture();
        var firstKey = await f.Database.SeedAsync(10);
        var secondKey = await f.Database.SeedAsync(10);
        var order = new OrderInfo(await f.ReadAsync());
        await using var scope = await f.Service.BeginCheckoutAsync(order);
        using var activation = scope.Enter();
        var first = await f.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = firstKey, Quantity = 1, OrderId = f.OrderId.ToString(), Duration = TimeSpan.FromMinutes(5),
        });
        var second = await f.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = secondKey, Quantity = 1, OrderId = f.OrderId.ToString(), Duration = TimeSpan.FromMinutes(10),
        });
        order.ReservationIds = new[] { first.ReservationId!, second.ReservationId! };
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId)
            .Set(x => x.OrderInfo, JsonConvert.SerializeObject(order, EkomJsonDotNet.Settings)).UpdateAsync();

        var attemptId = await f.Service.SubmitAsync(order);

        var attempt = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == attemptId);
        var holds = await db.GetTable<StockReservationData>().Where(x => x.OrderId == f.OrderId.ToString()).ToListAsync();
        Assert.Equal(2, holds.Count);
        Assert.All(holds, hold => Assert.Equal(first.ExpiresUtc, hold.ExpiresUtc));
        Assert.Equal(first.ExpiresUtc, attempt.ExpiresUtc);
        Assert.True(attempt.ExpiresUtc < second.ExpiresUtc);
    }

    [Fact]
    public async Task LegacyCompletionReceiptPreventsRetryEditAndReleaseWithoutPaidDate()
    {
        using var f = new Fixture();
        using var db = f.Database.Factory.GetDatabase();
        await db.InsertAsync(new CheckoutStockCompletionData
        {
            OrderId = f.OrderId,
            CompletedUtc = DateTime.UtcNow,
        });
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId)
            .Set(x => x.OrderStatusCol, "Pending").UpdateAsync();
        var order = new OrderInfo(await f.ReadAsync());

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.BeginCheckoutAsync(order));
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.BeginEditAsync(order, "cart-edit"));
        Assert.Null(await f.Service.ReleaseAsync(f.OrderId, null, "legacy cancel"));
        Assert.Equal("Pending", (await f.ReadAsync()).OrderStatusCol);
        Assert.False(await db.GetTable<CheckoutPaymentAttemptData>().AnyAsync());
        var completed = false;
        Assert.True(await f.Service.CompleteAsync(f.OrderId, null, () =>
        {
            completed = true;
            return Task.CompletedTask;
        }));
        Assert.True(completed);
    }

    [Fact]
    public async Task CancelReleasesExactlyOnceResetsSameOrderAndPersistsDetailedAudit()
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync();
        var snapshot = attempt.SubmittedOrderInfo;
        var result = await f.Service.ReleaseAsync(f.OrderId, attempt.AttemptId, "Customer cancel");
        Assert.NotNull(result);
        Assert.Equal(f.OrderId, result.UniqueId);
        Assert.Equal("Incomplete", result.OrderStatusCol);
        Assert.Empty(JObject.Parse(result.OrderInfo)["ReservationIds"]!);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        Assert.Null(await f.Service.ReleaseAsync(f.OrderId, attempt.AttemptId, "Repeated cancel"));
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        using var db = f.Database.Factory.GetDatabase();
        var stored = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync();
        Assert.Equal(CheckoutPaymentAttemptState.Released, stored.State);
        Assert.Equal(snapshot, stored.SubmittedOrderInfo);
        var logs = await db.GetTable<OrderActivityLog>().ToListAsync();
        Assert.Contains(logs, x => x.Log.Contains("release attempted", StringComparison.Ordinal) && x.Log.Contains("Customer cancel", StringComparison.Ordinal));
        Assert.Contains(logs, x => x.Log.Contains("quantity=3", StringComparison.Ordinal) && x.Log.Contains("restored=3", StringComparison.Ordinal) && x.Log.Contains("outcome=Released", StringComparison.Ordinal));
        Assert.Contains(logs, x => x.Log.Contains("outcome=AlreadyReleased", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, x => x.Log.Contains("secret-code", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SubmittedWaitingForPaymentCancelRestoresStockAndIncompleteStatus()
    {
        using var f = new Fixture();
        var key = await f.Database.SeedAsync(10);
        var order = new OrderInfo(await f.ReadAsync());
        await using var scope = await f.Service.BeginCheckoutAsync(order);
        using var activation = scope.Enter();
        var hold = await f.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = key, Quantity = 3, OrderId = f.OrderId.ToString(), PaymentAttemptId = scope.AttemptId.ToString(),
        });
        order.ReservationIds = new[] { Assert.IsType<string>(hold.ReservationId) };
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId)
            .Set(x => x.OrderInfo, JsonConvert.SerializeObject(order, EkomJsonDotNet.Settings)).UpdateAsync();
        var attemptId = await f.Service.SubmitAsync(order);
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId).Set(x => x.OrderStatusCol, "WaitingForPayment").UpdateAsync();
        var reset = await f.Service.ReleaseAsync(f.OrderId, attemptId, "Online payment cancel");
        Assert.Equal("Incomplete", reset!.OrderStatusCol);
        Assert.Equal(10, await f.Database.StockAsync(key));
    }

    [Fact]
    public async Task SameCheckoutCapabilityPermitsInternalTokenlessCompletion()
    {
        using var f = new Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        await using var scope = await f.Service.BeginCheckoutAsync(order);
        using var activation = scope.Enter();
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId)
            .Set(x => x.OrderInfo, JsonConvert.SerializeObject(order, EkomJsonDotNet.Settings)).UpdateAsync();
        await f.Service.SubmitAsync(order);
        var invoked = false;
        Assert.True(await f.Service.CompleteAsync(f.OrderId, null, () => { invoked = true; return Task.CompletedTask; }));
        Assert.True(invoked);
    }

    [Fact]
    public async Task RetryUsesFreshAttemptAndStaleCancelCannotReleaseItsHolds()
    {
        using var f = new Fixture();
        var old = await f.SubmittedAsync();
        await f.Service.ReleaseAsync(f.OrderId, old.AttemptId, "Cancel");
        var next = await f.SubmittedAsync();
        Assert.NotEqual(old.AttemptId, next.AttemptId);
        Assert.Null(await f.Service.ReleaseAsync(f.OrderId, old.AttemptId, "Stale cancel"));
        Assert.Null(await f.Service.ReleaseAsync(f.OrderId, null, "Missing token"));
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        Assert.False(await f.Service.CompleteAsync(f.OrderId, old.AttemptId, () => throw new InvalidOperationException("Must not complete")));
    }

    [Fact]
    public async Task LateHistoricalSuccessPersistsReconciliationEvenWhenActivityLoggingFails()
    {
        using var f = new Fixture();
        var old = await f.SubmittedAsync();
        await f.Service.ReleaseAsync(f.OrderId, old.AttemptId, "First cancel");
        var current = await f.SubmittedAsync();
        using var db = f.Database.Factory.GetDatabase();
        db.Execute("CREATE TRIGGER FailCallbackAudit BEFORE INSERT ON EkomOrdersActivityLog BEGIN SELECT RAISE(ABORT, 'audit unavailable'); END");
        Assert.False(await f.Service.CompleteAsync(f.OrderId, old.AttemptId, () => throw new InvalidOperationException("Must not complete edited purchase")));
        Assert.Equal(CheckoutPaymentAttemptState.ReconciliationRequired,
            (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == old.AttemptId)).State);
        Assert.Equal(CheckoutPaymentAttemptState.Submitted,
            (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == current.AttemptId)).State);
        Assert.Equal(current.AttemptId, (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).ActiveAttemptId);
        Assert.False((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).ReconciliationRequired);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
    }

    [Fact]
    public async Task SubmittedHistoricalSnapshotNeverReturnsCurrentEditedPurchase()
    {
        using var f = new Fixture();
        var old = await f.SubmittedAsync();
        await f.Service.ReleaseAsync(f.OrderId, old.AttemptId, "Cancel");
        await f.SubmittedAsync();
        var snapshot = await f.Service.GetSubmittedOrderAsync(f.OrderId, old.AttemptId);
        Assert.Equal(old.SubmittedOrderInfo, snapshot!.OrderInfo);
        Assert.NotEqual((await f.ReadAsync()).OrderInfo, snapshot.OrderInfo);
        Assert.Null(await f.Service.GetSubmittedOrderAsync(Guid.NewGuid(), old.AttemptId));
        Assert.Null(await f.Service.GetSubmittedOrderAsync(f.OrderId, Guid.NewGuid()));
    }

    [Fact]
    public async Task EditReleasesAndRefreshesPassedInstanceBeforeMutation()
    {
        using var f = new Fixture();
        await f.SubmittedAsync();
        var stale = new OrderInfo(await f.ReadAsync());
        Assert.Single(stale.ReservationIds);
        await using var scope = await f.Service.BeginEditAsync(stale, "Quantity changed");
        using var activation = scope.Enter();
        Assert.Empty(stale.ReservationIds);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        using var db = f.Database.Factory.GetDatabase();
        await f.Service.EnsureWriteAllowedAsync(db, f.OrderId);
        Assert.Equal(scope.Owner, (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    [Fact]
    public async Task ReleasedAndChangedCallbacksOnlyLogReconciliation()
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync();
        using (var db = f.Database.Factory.GetDatabase())
            await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId)
                .Set(x => x.OrderInfo, JObject.Parse(attempt.SubmittedOrderInfo!).AlsoChanged()).UpdateAsync();
        var invoked = false;
        Assert.False(await f.Service.CompleteAsync(f.OrderId, attempt.AttemptId, () => { invoked = true; return Task.CompletedTask; }));
        Assert.False(invoked);
        await f.Service.ReleaseAsync(f.OrderId, attempt.AttemptId, "Cancel changed purchase");
        Assert.False(await f.Service.CompleteAsync(f.OrderId, attempt.AttemptId, () => { invoked = true; return Task.CompletedTask; }));
        Assert.False(invoked);
        using var read = f.Database.Factory.GetDatabase();
        Assert.Contains(await read.GetTable<OrderActivityLog>().ToListAsync(), x => x.Log.Contains("NEEDS RECONCILIATION", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TimerExpiryNeverCompletesAndReleaseDoesNotRestoreTwice()
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync();
        var id = JsonConvert.DeserializeObject<List<string>>(attempt.ReservationIds)!.Single();
        await f.Database.MakeDueAsync(id);
        await f.Database.Service.ExpireAsync(id);
        Assert.False(await f.Service.CompleteAsync(f.OrderId, attempt.AttemptId, () => throw new InvalidOperationException("Expired")));
        await f.Service.ReleaseAsync(f.OrderId, attempt.AttemptId, "Expired timer cleanup");
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
    }

    [Fact]
    public async Task FailedReleaseLeavesPendingAndCanRetry()
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync();
        var fail = new Mock<IStockReservationService>();
        fail.Setup(x => x.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.NewService(stock: fail.Object).ReleaseAsync(f.OrderId, attempt.AttemptId, "Error return"));
        using (var db = f.Database.Factory.GetDatabase())
            Assert.Equal(CheckoutPaymentAttemptState.ReleasePending, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        await f.Service.ReleaseAsync(f.OrderId, attempt.AttemptId, "Retry error return");
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
    }

    [Fact]
    public async Task MissingGiftcardAdapterFailsClosedThenAdapterConfirmsRelease()
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync(claimed: true);
        var exception = await Assert.ThrowsAsync<EkomHttpException>(() => f.Service.ReleaseAsync(f.OrderId, attempt.AttemptId, "Giftcard cancel"));
        Assert.Contains(nameof(ICheckoutGiftcardReservations), exception.Message, StringComparison.Ordinal);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReleaseAsync(f.OrderId, attempt.AttemptId, It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var data = await f.NewService(adapter.Object).ReleaseAsync(f.OrderId, attempt.AttemptId, "Giftcard retry");
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, attempt.AttemptId, It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Once);
        var giftcard = JObject.Parse(data!.OrderInfo)["Giftcards"]!.Single()!.ToObject<Giftcard>()!;
        Assert.Equal("secret-code", giftcard.Code);
        Assert.Equal(5, giftcard.Amount);
        Assert.False(giftcard.Claimed);
        Assert.Null(giftcard.ClaimId);
        Assert.Null(giftcard.ClaimDate);
        Assert.Null(giftcard.TransactionId);
        Assert.Null(giftcard.UsedDate);
        Assert.NotNull(giftcard.ValidUntil);
    }

    [Theory]
    [InlineData("Claimed")]
    [InlineData("TransactionId")]
    [InlineData("ClaimDate")]
    public async Task SettledGiftcardNeverCallsReleaseAdapterOrClearsMetadata(string settledField)
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync(claimed: true);
        var json = JObject.Parse(attempt.SubmittedOrderInfo!);
        json["Giftcards"]!.Single()![settledField] = settledField switch
        {
            "Claimed" => JToken.FromObject(true),
            "TransactionId" => JToken.FromObject("settled-transaction"),
            _ => JToken.FromObject(DateTime.UtcNow),
        };
        var frozen = json.ToString(Formatting.None);
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId).Set(x => x.OrderInfo, frozen).UpdateAsync();
        await db.GetTable<CheckoutPaymentAttemptData>().Where(x => x.AttemptId == attempt.AttemptId).Set(x => x.SubmittedOrderInfo, frozen).UpdateAsync();
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.NewService(adapter.Object).ReleaseAsync(f.OrderId, attempt.AttemptId, "Settled selection"));
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(frozen, (await f.ReadAsync()).OrderInfo);
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        Assert.Equal(CheckoutPaymentAttemptState.ReconciliationRequired, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
    }

    [Fact]
    public async Task NoHoldCheckoutAndEditRefreshStalePurchaseBeforeCallerMutates()
    {
        using var f = new Fixture();
        var staleCheckout = new OrderInfo(await f.ReadAsync()) { Coupon = "old-selection" };
        var staleEdit = new OrderInfo(await f.ReadAsync()) { Coupon = "old-selection" };
        using var db = f.Database.Factory.GetDatabase();
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        json["Coupon"] = "authoritative-selection";
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId).Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
        await using (var checkout = await f.Service.BeginCheckoutAsync(staleCheckout))
            Assert.Equal("authoritative-selection", staleCheckout.Coupon);
        await using var edit = await f.Service.BeginEditAsync(staleEdit, "Refresh before editing");
        Assert.Equal("authoritative-selection", staleEdit.Coupon);
    }

    [Fact]
    public async Task DurableOwnerFencesOtherNodesWithoutTimedTakeover()
    {
        using var f = new Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        await using var first = await f.Service.BeginEditAsync(order, "First edit");
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.NewService().BeginEditAsync(order, "Other node"));
        using var db = f.Database.Factory.GetDatabase();
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.NewService().EnsureWriteAllowedAsync(db, f.OrderId));
        using (first.Enter())
        {
            await using var nested = await f.Service.BeginEditAsync(order, "Nested save");
            Assert.Equal(first.Owner, nested.Owner);
        }
        first.PreserveOwnership = true;
        await first.DisposeAsync();
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.NewService().BeginEditAsync(order, "No timeout takeover"));
    }

    [Fact]
    public async Task PreparingOwnerBlocksReturnAndUnactivatedCheckoutRejectsSubmit()
    {
        using var f = new Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        await using var checkout = await f.Service.BeginCheckoutAsync(order);
        Assert.NotNull(checkout.AttemptId);
        await Assert.ThrowsAsync<EkomHttpException>(() => f.Service.SubmitAsync(order));
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.NewService().ReleaseAsync(f.OrderId, checkout.AttemptId, "Concurrent return"));
    }

    [Fact]
    public async Task ValidationReturnBeforeSubmissionAllowsFreshCheckoutAfterOwnerDisposes()
    {
        using var f = new Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        var first = await f.Service.BeginCheckoutAsync(order);
        var oldId = first.AttemptId;
        await first.DisposeAsync();
        await using var next = await f.NewService().BeginCheckoutAsync(order);
        Assert.NotEqual(oldId, next.AttemptId);
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(CheckoutPaymentAttemptState.Released,
            (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == oldId!.Value)).State);
    }

    [Fact]
    public async Task SubmitFreezesActualPersistedPurchaseOnceAndRejectsResubmission()
    {
        using var f = new Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        var actual = JsonConvert.SerializeObject(order, EkomJsonDotNet.Settings);
        using (var db = f.Database.Factory.GetDatabase())
            await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId).Set(x => x.OrderInfo, actual).UpdateAsync();
        await using var checkout = await f.Service.BeginCheckoutAsync(order);
        using var activation = checkout.Enter();
        var id = await f.Service.SubmitAsync(order);
        using var read = f.Database.Factory.GetDatabase();
        var submitted = await read.GetTable<CheckoutPaymentAttemptData>().SingleAsync();
        Assert.Equal(id, submitted.AttemptId);
        Assert.Equal(actual, submitted.SubmittedOrderInfo);
        Assert.NotNull(submitted.SubmittedOrderData);
        await Assert.ThrowsAsync<EkomHttpException>(() => f.Service.SubmitAsync(order));
        Assert.Equal(actual, (await read.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).SubmittedOrderInfo);
    }

    [Fact]
    public async Task ExpiredAttemptIsReleasedBeforeFreshCheckoutIsAllocated()
    {
        using var f = new Fixture();
        var old = await f.SubmittedAsync();
        await f.Database.MakeDueAsync(JsonConvert.DeserializeObject<List<string>>(old.ReservationIds)!.Single());
        var order = new OrderInfo(await f.ReadAsync());
        await using var checkout = await f.Service.BeginCheckoutAsync(order);
        Assert.NotEqual(old.AttemptId, checkout.AttemptId);
        Assert.Empty(order.ReservationIds);
        Assert.Equal(10, await f.Database.StockAsync(f.StockKey));
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(CheckoutPaymentAttemptState.Released,
            (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == old.AttemptId)).State);
    }

    [Fact]
    public async Task GiftcardAdapterRenewsSelectionBeforeCheckoutProviderWork()
    {
        using var f = new Fixture();
        using (var db = f.Database.Factory.GetDatabase())
        {
            var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
            json["Giftcards"] = JArray.FromObject(new[] { new Giftcard { Code = "secret-code", Amount = 5 } });
            await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId).Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
        }
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReserveAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Giftcard { Code = "secret-code", Amount = 5, Claimed = false, ClaimId = "fresh-claim" } });
        var order = new OrderInfo(await f.ReadAsync());
        await using var checkout = await f.NewService(adapter.Object).BeginCheckoutAsync(order);
        Assert.Equal("fresh-claim", order.Giftcards.Single().ClaimId);
        Assert.Equal("fresh-claim", JObject.Parse((await f.ReadAsync()).OrderInfo)["Giftcards"]!.Single()!["ClaimId"]!.Value<string>());
        adapter.Verify(x => x.ReserveAsync(f.OrderId, checkout.AttemptId!.Value, It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LegacyOwnedStockMissingFromJsonStillReleasesBeforeEdit()
    {
        using var f = new Fixture();
        var key = await f.Database.SeedAsync(10);
        await f.Database.Service.ReserveAsync(new StockReservationRequest { Key = key, Quantity = 3, OrderId = f.OrderId.ToString() });
        var order = new OrderInfo(await f.ReadAsync());
        await using var edit = await f.Service.BeginEditAsync(order, "Legacy orphaned hold before edit");
        Assert.Equal(10, await f.Database.StockAsync(key));
        Assert.Empty(order.ReservationIds);
    }

    [Fact]
    public async Task ReservationMetadataLeaseRefreshesOrderWithoutReleasingIncomingOwnedHold()
    {
        using var f = new Fixture();
        var order = new OrderInfo(await f.ReadAsync()) { Coupon = "stale" };
        var key = await f.Database.SeedAsync(10);
        var incoming = await f.Database.Service.ReserveAsync(new StockReservationRequest { Key = key, Quantity = 3, OrderId = f.OrderId.ToString() });
        await using var scope = await f.Service.BeginReservationUpdateAsync(order);
        using var activation = scope.Enter();
        Assert.Null(order.Coupon);
        Assert.Equal(7, await f.Database.StockAsync(key));
        order.ReservationIds = new[] { Assert.IsType<string>(incoming.ReservationId) };
        await using var nested = await f.Service.BeginReservationUpdateAsync(order);
        Assert.Equal(scope.Owner, nested.Owner);
        Assert.Single(order.ReservationIds);
        using var db = f.Database.Factory.GetDatabase();
        await f.Service.EnsureWriteAllowedAsync(db, f.OrderId);
    }

    [Fact]
    public async Task ReservationMetadataLeaseRejectsActiveAttemptAndPaidOrder()
    {
        using var f = new Fixture();
        await f.SubmittedAsync();
        var order = new OrderInfo(await f.ReadAsync());
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.BeginReservationUpdateAsync(order));
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<CheckoutPaymentAttemptData>().Set(x => x.State, CheckoutPaymentAttemptState.Released).UpdateAsync();
        await db.GetTable<OrderData>().Set(x => x.OrderStatusCol, "Closed").UpdateAsync();
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.BeginReservationUpdateAsync(order));
    }

    [Fact]
    public async Task MissingReleaseAuditFailsClosedBeforeAnyStockRestoration()
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync();
        using var db = f.Database.Factory.GetDatabase();
        db.Execute("CREATE TRIGGER FailReleaseAudit BEFORE INSERT ON EkomOrdersActivityLog BEGIN SELECT RAISE(ABORT, 'audit unavailable'); END");
        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.ReleaseAsync(f.OrderId, attempt.AttemptId, "Audit required"));
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
        Assert.Equal(CheckoutPaymentAttemptState.Submitted, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
    }

    [Fact]
    public async Task PreparationOwnerBlocksReleaseAndEditEvenWithoutOperationOwner()
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync();
        using var db = f.Database.Factory.GetDatabase();
        await db.InsertAsync(new CheckoutPreparationData { OrderId = f.OrderId, Owner = Guid.NewGuid().ToString() });
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.ReleaseAsync(f.OrderId, attempt.AttemptId, "Active preparation"));
        var order = new OrderInfo(await f.ReadAsync());
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.BeginEditAsync(order, "Active preparation"));
        Assert.Equal(7, await f.Database.StockAsync(f.StockKey));
    }

    [Fact]
    public async Task CompletionIsIdempotentAndFailedCompletionBlocksEditsAndRelease()
    {
        using var f = new Fixture();
        var attempt = await f.SubmittedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CompleteAsync(f.OrderId, attempt.AttemptId, () => throw new InvalidOperationException("Paid completion failed")));
        Assert.Null(await f.Service.ReleaseAsync(f.OrderId, attempt.AttemptId, "Cannot cancel paid completion"));
        var order = new OrderInfo(await f.ReadAsync());
        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.BeginEditAsync(order, "Cannot edit paid completion"));
        var count = 0;
        Assert.True(await f.Service.CompleteAsync(f.OrderId, attempt.AttemptId, () => { count++; return Task.CompletedTask; }));
        Assert.True(await f.Service.CompleteAsync(f.OrderId, attempt.AttemptId, () => { count++; return Task.CompletedTask; }));
        Assert.Equal(1, count);
    }

    [Fact]
    public void PurchaseComparisonIgnoresOnlyRootLifecycleFields()
    {
        Assert.True(CheckoutPaymentAttemptService.SamePurchase("{\"Amount\":4,\"OrderStatus\":\"Pending\",\"UpdateDate\":1}", "{\"Amount\":4,\"OrderStatus\":\"Closed\",\"UpdateDate\":2}"));
        Assert.False(CheckoutPaymentAttemptService.SamePurchase("{\"Amount\":4}", "{\"Amount\":5}"));
        Assert.False(CheckoutPaymentAttemptService.SamePurchase("{\"Line\":{\"UpdateDate\":1}}", "{\"Line\":{\"UpdateDate\":2}}"));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ConfigurationScope _configuration = new(overrides: new Dictionary<string, string?>());
        public StockReservationTests.ReservationDatabase Database { get; } = new();
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        public Guid OrderId { get; } = Guid.NewGuid();
        public Guid StockKey { get; private set; }
        public CheckoutPaymentAttemptService Service { get; }

        public Fixture()
        {
            using var db = Database.Factory.GetDatabase();
            db.CreateTable<OrderData>();
            db.CreateTable<OrderActivityLog>();
            db.CreateTable<CheckoutPaymentOperationData>(tableOptions: TableOptions.CreateIfNotExists);
            db.CreateTable<CheckoutPaymentAttemptData>(tableOptions: TableOptions.CreateIfNotExists);
            // ReservationDatabase already creates preparation infrastructure.
            var storeInfo = new StoreInfo(Guid.NewGuid(), new CurrencyModel { CurrencyValue = "en-US" }, [], "en-US", "main", false, 0, false);
            db.Insert(new OrderData
            {
                UniqueId = OrderId, OrderStatusCol = "Incomplete",
                OrderInfo = JsonConvert.SerializeObject(new { StoreInfo = storeInfo, OrderLines = Array.Empty<object>(), Giftcards = Array.Empty<Giftcard>(), ReservationIds = Array.Empty<string>() }),
                OrderNumber = "test", CustomerEmail = "", CustomerUsername = "", ShippingCountry = "", Currency = "USD", StoreAlias = "main",
            });
            Service = NewService();
        }

        public CheckoutPaymentAttemptService NewService(ICheckoutGiftcardReservations? adapter = null, IStockReservationService? stock = null)
            => new(Database.Factory, stock ?? Database.Service,
                new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, Database.Factory),
                NullLogger<CheckoutPaymentAttemptService>.Instance, _cache, adapter);

        public async Task<CheckoutPaymentAttemptData> SubmittedAsync(bool claimed = false)
        {
            if (StockKey == Guid.Empty) StockKey = await Database.SeedAsync(10);
            var attemptId = Guid.NewGuid();
            var reservation = await Database.Service.ReserveAsync(new StockReservationRequest
            {
                Key = StockKey, Quantity = 3, OrderId = OrderId.ToString(), PaymentAttemptId = attemptId.ToString(),
            });
            var reservationId = Assert.IsType<string>(reservation.ReservationId);
            var json = new JObject
            {
                ["StoreInfo"] = JObject.Parse((await ReadAsync()).OrderInfo)["StoreInfo"]?.DeepClone(),
                ["OrderLines"] = new JArray(), ["ReservationIds"] = new JArray(reservationId),
                ["HangfireJobs"] = new JArray(reservationId),
                ["Giftcards"] = claimed ? JArray.FromObject(new[] { new Giftcard
                {
                    Code = "secret-code", Amount = 5, Claimed = false, ClaimId = "claim", ValidUntil = DateTime.UtcNow.AddYears(1),
                } }) : new JArray(),
            }.ToString(Formatting.None);
            using var db = Database.Factory.GetDatabase();
            await db.GetTable<OrderData>().Where(x => x.UniqueId == OrderId).Set(x => x.OrderInfo, json).Set(x => x.OrderStatusCol, "Pending").UpdateAsync();
            var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleOrDefaultAsync(x => x.OrderId == OrderId);
            if (operation == null) await db.InsertAsync(new CheckoutPaymentOperationData { OrderId = OrderId, ActiveAttemptId = attemptId });
            else await db.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == OrderId).Set(x => x.ActiveAttemptId, (Guid?)attemptId).UpdateAsync();
            var attempt = new CheckoutPaymentAttemptData
            {
                AttemptId = attemptId, OrderId = OrderId, State = CheckoutPaymentAttemptState.Submitted,
                SubmittedOrderInfo = json, SubmittedOrderData = JsonConvert.SerializeObject(await ReadAsync()),
                ReservationIds = JsonConvert.SerializeObject(new[] { reservationId }), CreatedUtc = DateTime.UtcNow, SubmittedUtc = DateTime.UtcNow,
            };
            await db.InsertAsync(attempt);
            return attempt;
        }

        public async Task<OrderData> ReadAsync()
        {
            using var db = Database.Factory.GetDatabase();
            return await db.GetTable<OrderData>().SingleAsync(x => x.UniqueId == OrderId);
        }

        public void Dispose()
        {
            _cache.Dispose();
            Database.Dispose();
            _configuration.Dispose();
        }
    }
}

internal static class CheckoutAttemptTestJson
{
    internal static string AlsoChanged(this JObject json)
    {
        json["TotalAmount"] = 999;
        return json.ToString(Formatting.None);
    }
}
