using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Data;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class CheckoutPaymentCancellationTests
{
    [Fact]
    public async Task CancellationBeforeAcquireDoesNotCreateOwner()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.BeginCheckoutAsync(order, cancellation.Token));
        using var db = f.Database.Factory.GetDatabase();
        Assert.False(await db.GetTable<CheckoutPaymentOperationData>().AnyAsync(x => x.Owner != null));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task CommittedAcquireFailureClearsOnlyItsGeneratedOwner(bool canceled, bool newerOwner)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        using var cancellation = new CancellationTokenSource();
        var fired = false;
        using var fault = new TraceFault(f, info =>
        {
            if (fired || info.TraceInfoStep != TraceInfoStep.AfterExecute || !IsOwnerUpdate(info)) return;
            fired = true;
            if (newerOwner)
            {
                using var db = f.Database.Factory.GetDatabase();
                db.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == f.OrderId)
                    .Set(x => x.Owner, "newer-owner").Update();
            }
            if (canceled)
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }
            throw new InvalidOperationException("Acquire committed before failure");
        });
        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.BeginCheckoutAsync(order, cancellation.Token));
        Assert.True(fired);
        using var read = f.Database.Factory.GetDatabase();
        Assert.Equal(newerOwner ? "newer-owner" : null, (await read.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        Assert.False(await read.GetTable<CheckoutPaymentAttemptData>().AnyAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AttemptCommitFailureResolvesDurableOutcomeBeforeUnlock(bool committed)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        using var cancellation = new CancellationTokenSource();
        var fired = false;
        using var fault = new TraceFault(f, info =>
        {
            if (fired || info.Operation != TraceOperation.CommitTransaction ||
                info.TraceInfoStep != (committed ? TraceInfoStep.AfterExecute : TraceInfoStep.BeforeExecute)) return;
            fired = true;
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.BeginCheckoutAsync(order, cancellation.Token));
        Assert.True(fired);
        using var read = f.Database.Factory.GetDatabase();
        Assert.Null((await read.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        var attempts = await read.GetTable<CheckoutPaymentAttemptData>().ToListAsync();
        if (committed) Assert.Equal(CheckoutPaymentAttemptState.Released, Assert.Single(attempts).State);
        else Assert.Empty(attempts);
    }

    [Fact]
    public async Task CancellationBeforeReserveDoesNotInvokeAdapterOrPreserveOwner()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await SelectGiftcardAsync(f);
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        var order = new OrderInfo(await f.ReadAsync());
        using var cancellation = new CancellationTokenSource();
        using var fault = new TraceFault(f, info =>
        {
            if (info.Operation == TraceOperation.CommitTransaction && info.TraceInfoStep == TraceInfoStep.AfterExecute)
                cancellation.Cancel();
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.NewService(adapter.Object).BeginCheckoutAsync(order, cancellation.Token));
        adapter.Verify(x => x.ReserveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
        using var db = f.Database.Factory.GetDatabase();
        Assert.Null((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        Assert.Equal(CheckoutPaymentAttemptState.Released, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
    }

    [Fact]
    public async Task LocalRecoveryDoesNotUnlockAMissingActiveAttempt()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        var fired = false;
        using var fault = new TraceFault(f, info =>
        {
            if (fired || info.Operation != TraceOperation.CommitTransaction || info.TraceInfoStep != TraceInfoStep.AfterExecute) return;
            fired = true;
            using var db = f.Database.Factory.GetDatabase();
            db.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == f.OrderId)
                .Set(x => x.ActiveAttemptId, (Guid?)Guid.NewGuid()).Update();
            throw new OperationCanceledException();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.BeginCheckoutAsync(order));

        Assert.True(fired);
        using var read = f.Database.Factory.GetDatabase();
        Assert.NotNull((await read.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KnownClaimsArePersistedDespiteCancellationBeforeCompensation(bool releaseFails)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await SelectGiftcardAsync(f);
        var order = new OrderInfo(await f.ReadAsync());
        using var cancellation = new CancellationTokenSource();
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReserveAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cancellation.Cancel();
                return Task.FromResult<IReadOnlyList<Giftcard>>([new Giftcard { Code = "selected", Amount = 5, ClaimId = "known-claim" }]);
            });
        var released = false;
        adapter.Setup(x => x.ReleaseAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, Guid _, IReadOnlyList<Giftcard> claims, CancellationToken token) =>
            {
                Assert.False(token.IsCancellationRequested);
                Assert.Equal("known-claim", Assert.Single(claims).ClaimId);
                Assert.Equal("known-claim", ReadClaim(await f.ReadAsync()));
                released = true;
                if (releaseFails) throw new InvalidOperationException("External release unavailable");
            });
        await Assert.ThrowsAnyAsync<Exception>(() => f.NewService(adapter.Object).BeginCheckoutAsync(order, cancellation.Token));
        Assert.True(released);
        using var db = f.Database.Factory.GetDatabase();
        var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleAsync();
        var attempt = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync();
        if (releaseFails)
        {
            Assert.NotNull(operation.Owner);
            Assert.Equal("known-claim", ReadClaim(await f.ReadAsync()));
            Assert.Equal(CheckoutPaymentAttemptState.ReleasePending, attempt.State);
        }
        else
        {
            Assert.Null(operation.Owner);
            Assert.Null(ReadClaim(await f.ReadAsync()));
            Assert.Equal(CheckoutPaymentAttemptState.Released, attempt.State);
        }
    }

    [Fact]
    public async Task UnknownAdapterCancellationRemainsFenced()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await SelectGiftcardAsync(f);
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReserveAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var order = new OrderInfo(await f.ReadAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.NewService(adapter.Object).BeginCheckoutAsync(order));
        using var db = f.Database.Factory.GetDatabase();
        Assert.NotNull((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailedAcquireCleanupDoesNotReportAnUnlockedOrder()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        var acquired = false;
        using var fault = new TraceFault(f, info =>
        {
            if (!IsOwnerUpdate(info)) return;
            if (!acquired && info.TraceInfoStep == TraceInfoStep.AfterExecute)
            {
                acquired = true;
                throw new OperationCanceledException();
            }
            if (acquired && info.TraceInfoStep == TraceInfoStep.BeforeExecute)
                throw new InvalidOperationException("Cleanup database unavailable");
        });
        var error = await Assert.ThrowsAsync<AggregateException>(() => f.Service.BeginCheckoutAsync(order));
        Assert.IsType<OperationCanceledException>(error.InnerExceptions[0]);
        Assert.IsType<InvalidOperationException>(error.InnerExceptions[1]);
        using var db = f.Database.Factory.GetDatabase();
        Assert.NotNull((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    [Fact]
    public async Task CheckoutDisposalDoesNotReleaseClaimsOrClearANewerOwner()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await SelectGiftcardAsync(f);
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReserveAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Giftcard { Code = "selected", Amount = 5, ClaimId = "known-claim" } });
        var checkout = await f.NewService(adapter.Object).BeginCheckoutAsync(new OrderInfo(await f.ReadAsync()));
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<CheckoutPaymentOperationData>().Where(x => x.OrderId == f.OrderId)
            .Set(x => x.Owner, "newer-owner").UpdateAsync();
        await checkout.DisposeAsync();
        Assert.Equal("newer-owner", (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        Assert.Equal("known-claim", ReadClaim(await f.ReadAsync()));
        adapter.Verify(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailedLocalRollbackRetainsOwner()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        using var fault = new TraceFault(f, info =>
        {
            if (info.TraceInfoStep == TraceInfoStep.BeforeExecute &&
                info.Operation is TraceOperation.CommitTransaction or TraceOperation.RollbackTransaction)
                throw new InvalidOperationException("Transaction unavailable");
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.BeginCheckoutAsync(order));
        using var db = f.Database.Factory.GetDatabase();
        Assert.NotNull((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SubmitCommitFailureResolvesLocalOnlyFence(bool committed)
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId)
            .Set(x => x.OrderInfo, JsonConvert.SerializeObject(order, EkomJsonDotNet.Settings)).UpdateAsync();
        var checkout = await f.Service.BeginCheckoutAsync(order);
        using var activation = checkout.Enter();
        var fired = false;
        using var fault = new TraceFault(f, info =>
        {
            if (fired || info.Operation != TraceOperation.CommitTransaction ||
                info.TraceInfoStep != (committed ? TraceInfoStep.AfterExecute : TraceInfoStep.BeforeExecute)) return;
            fired = true;
            throw new OperationCanceledException();
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.SubmitAsync(order));
        Assert.True(fired);
        Assert.False(checkout.PreserveOwnership);
        await checkout.DisposeAsync();
        Assert.Null((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        Assert.Equal(CheckoutPaymentAttemptState.Released,
            (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        using var suppression = CheckoutPaymentOperationScope.Suppress();
        await using var retry = await f.Service.BeginCheckoutAsync(new OrderInfo(await f.ReadAsync()));
        Assert.NotNull(retry.AttemptId);
    }

    [Fact]
    public async Task FailedDuplicateSubmitDoesNotRevokePreviouslyReturnedGatewayPermission()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        var order = new OrderInfo(await f.ReadAsync());
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<OrderData>().Set(x => x.OrderInfo, JsonConvert.SerializeObject(order, EkomJsonDotNet.Settings)).UpdateAsync();
        await using var checkout = await f.Service.BeginCheckoutAsync(order);
        using var activation = checkout.Enter();
        await f.Service.SubmitAsync(order);

        await Assert.ThrowsAnyAsync<EkomHttpException>(() => f.Service.SubmitAsync(order));

        Assert.False(checkout.SubmissionInProgress);
        await checkout.DisposeAsync();
        Assert.Equal(CheckoutPaymentAttemptState.Submitted, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        Assert.Null((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    [Fact]
    public async Task FailedSubmissionCompensationRetainsOwnerAndKnownClaim()
    {
        using var f = new CheckoutPaymentAttemptTests.Fixture();
        await SelectGiftcardAsync(f);
        var adapter = new Mock<ICheckoutGiftcardReservations>();
        adapter.Setup(x => x.ReserveAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Giftcard { Code = "selected", Amount = 5, ClaimId = "known-claim" } });
        adapter.Setup(x => x.ReleaseAsync(f.OrderId, It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Release unavailable"));
        var service = f.NewService(adapter.Object);
        var order = new OrderInfo(await f.ReadAsync());
        await using var checkout = await service.BeginCheckoutAsync(order);
        using var activation = checkout.Enter();
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<OrderData>().Set(x => x.OrderInfo, JsonConvert.SerializeObject(order, EkomJsonDotNet.Settings)).UpdateAsync();
        var fired = false;
        using (var fault = new TraceFault(f, info =>
        {
            if (fired || info.Operation != TraceOperation.CommitTransaction || info.TraceInfoStep != TraceInfoStep.AfterExecute) return;
            fired = true;
            throw new OperationCanceledException();
        }))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SubmitAsync(order));

        await Assert.ThrowsAsync<InvalidOperationException>(() => checkout.DisposeAsync().AsTask());

        Assert.True(checkout.PreserveOwnership);
        Assert.NotNull((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        Assert.Equal(CheckoutPaymentAttemptState.ReleasePending, (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        Assert.Equal("known-claim", ReadClaim(await f.ReadAsync()));
        adapter.Verify(x => x.ReleaseAsync(f.OrderId, checkout.AttemptId!.Value,
            It.Is<IReadOnlyList<Giftcard>>(cards => cards.Count == 1 && cards[0].ClaimId == "known-claim"), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static bool IsOwnerUpdate(TraceInfo info)
        => info.Command?.CommandText is string sql && sql.StartsWith("UPDATE", StringComparison.Ordinal) &&
            sql.Contains("Owner", StringComparison.Ordinal) && !sql.Contains("ActiveAttemptId", StringComparison.Ordinal);

    private static string? ReadClaim(OrderData data)
        => JObject.Parse(data.OrderInfo)["Giftcards"]!.Single()!["ClaimId"]!.Value<string>();

    private static async Task SelectGiftcardAsync(CheckoutPaymentAttemptTests.Fixture f)
    {
        var json = JObject.Parse((await f.ReadAsync()).OrderInfo);
        json["Giftcards"] = JArray.FromObject(new[] { new Giftcard { Code = "selected", Amount = 5 } });
        using var db = f.Database.Factory.GetDatabase();
        await db.GetTable<OrderData>().Where(x => x.UniqueId == f.OrderId).Set(x => x.OrderInfo, json.ToString(Formatting.None)).UpdateAsync();
    }

    // The factory constructs connections internally. LinqToDB's trace hook can
    // inject failures after the real SQLite effect, without mocking persistence.
    // Filter by connection string and restore globals to isolate other tests.
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
