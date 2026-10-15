using Ekom.Exceptions;
using Ekom.Models;
using LinqToDB;
using LinqToDB.Data;
using System.Diagnostics;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class CheckoutPreparationCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterOwnerWriteFinishesCommitThenClearsOnlyOwner(bool existingRow)
    {
        using var database = new StockReservationTests.ReservationDatabase();
        var orderId = Guid.NewGuid();
        const string protectedIds = "[\"previous-hold\"]";
        using (var db = database.Factory.GetDatabase())
        {
            if (existingRow) await db.InsertAsync(new CheckoutPreparationData { OrderId = orderId, ProtectedIds = protectedIds });
        }
        using var cancellation = new CancellationTokenSource();
        var wroteOwner = false;
        var committed = false;
        using var fault = new TraceFault(database, info =>
        {
            if (!wroteOwner && info.TraceInfoStep == TraceInfoStep.AfterExecute && IsPreparationWrite(info))
            {
                wroteOwner = true;
                cancellation.Cancel();
            }
            if (info.Operation == TraceOperation.CommitTransaction && info.TraceInfoStep == TraceInfoStep.AfterExecute)
                committed = true;
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.NewCheckout().AcquirePreparationAsync(orderId, cancellation.Token));

        Assert.True(wroteOwner);
        Assert.True(committed);
        using var read = database.Factory.GetDatabase();
        var row = await read.GetTable<CheckoutPreparationData>().SingleAsync();
        Assert.Null(row.Owner);
        Assert.Equal(existingRow ? protectedIds : "[]", row.ProtectedIds);
        var next = await database.NewCheckout().AcquirePreparationAsync(orderId, default);
        Assert.NotNull(next.Owner);
        await database.NewCheckout().ReleasePreparationAsync(next, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationExceptionAfterWriteRollsBackBeforeCleanup(bool existingRow)
    {
        using var database = new StockReservationTests.ReservationDatabase();
        var orderId = Guid.NewGuid();
        using (var db = database.Factory.GetDatabase())
        {
            if (existingRow) await db.InsertAsync(new CheckoutPreparationData { OrderId = orderId, ProtectedIds = "[\"hold\"]" });
        }
        using var cancellation = new CancellationTokenSource();
        var fired = false;
        using var fault = new TraceFault(database, info =>
        {
            if (fired || info.TraceInfoStep != TraceInfoStep.AfterExecute || !IsPreparationWrite(info)) return;
            fired = true;
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.NewCheckout().AcquirePreparationAsync(orderId, cancellation.Token));

        Assert.True(fired);
        using var read = database.Factory.GetDatabase();
        var row = await read.GetTable<CheckoutPreparationData>().SingleOrDefaultAsync();
        if (existingRow)
        {
            Assert.NotNull(row);
            Assert.Null(row.Owner);
            Assert.Equal("[\"hold\"]", row.ProtectedIds);
        }
        else Assert.Null(row);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CommitFailureResolvesOnlyGeneratedOwnerAfterTransactionDisposal(bool committed, bool newerOwner)
    {
        using var database = new StockReservationTests.ReservationDatabase();
        var orderId = Guid.NewGuid();
        const string protectedIds = "[\"protected-hold\"]";
        using (var db = database.Factory.GetDatabase())
            await db.InsertAsync(new CheckoutPreparationData { OrderId = orderId, ProtectedIds = protectedIds });
        using var cancellation = new CancellationTokenSource();
        var fired = false;
        using var fault = new TraceFault(database, info =>
        {
            if (fired || info.Operation != TraceOperation.CommitTransaction ||
                info.TraceInfoStep != (committed ? TraceInfoStep.AfterExecute : TraceInfoStep.BeforeExecute)) return;
            fired = true;
            if (newerOwner)
            {
                using var db = database.Factory.GetDatabase();
                db.GetTable<CheckoutPreparationData>().Where(x => x.OrderId == orderId)
                    .Set(x => x.Owner, "another-owner").Update();
            }
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.NewCheckout().AcquirePreparationAsync(orderId, cancellation.Token));

        Assert.True(fired);
        using var read = database.Factory.GetDatabase();
        var row = await read.GetTable<CheckoutPreparationData>().SingleAsync();
        Assert.Equal(newerOwner ? "another-owner" : null, row.Owner);
        Assert.Equal(protectedIds, row.ProtectedIds);
    }

    [Fact]
    public async Task UnknownCommittedAcquisitionFailureClearsItsOwnCapability()
    {
        using var database = new StockReservationTests.ReservationDatabase();
        var orderId = Guid.NewGuid();
        var failure = new InvalidOperationException("Commit acknowledgement unavailable");
        using var fault = new TraceFault(database, info =>
        {
            if (info.Operation == TraceOperation.CommitTransaction && info.TraceInfoStep == TraceInfoStep.AfterExecute)
                throw failure;
        });

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => database.NewCheckout().AcquirePreparationAsync(orderId, default)));

        using var read = database.Factory.GetDatabase();
        Assert.Null((await read.GetTable<CheckoutPreparationData>().SingleAsync()).Owner);
    }

    [Fact]
    public async Task CleanupFailurePreservesBothErrorsAndLeavesOwnerFenced()
    {
        using var database = new StockReservationTests.ReservationDatabase();
        var orderId = Guid.NewGuid();
        var acquisitionError = new OperationCanceledException("Commit acknowledgement canceled");
        var cleanupError = new InvalidOperationException("Cleanup unavailable");
        var committed = false;
        using var fault = new TraceFault(database, info =>
        {
            if (info.Operation == TraceOperation.CommitTransaction && info.TraceInfoStep == TraceInfoStep.AfterExecute)
            {
                committed = true;
                throw acquisitionError;
            }
            if (committed && info.TraceInfoStep == TraceInfoStep.BeforeExecute && IsPreparationWrite(info))
                throw cleanupError;
        });

        var error = await Assert.ThrowsAsync<AggregateException>(() => database.NewCheckout().AcquirePreparationAsync(orderId, default));

        Assert.Contains(acquisitionError, error.InnerExceptions);
        Assert.Contains(cleanupError, error.InnerExceptions);
        using var read = database.Factory.GetDatabase();
        Assert.NotNull((await read.GetTable<CheckoutPreparationData>().SingleAsync()).Owner);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlreadyCanceledTokenDoesNotWriteOrClearExistingOwner(bool existingOwner)
    {
        using var database = new StockReservationTests.ReservationDatabase();
        var orderId = Guid.NewGuid();
        using (var db = database.Factory.GetDatabase())
        {
            if (existingOwner) await db.InsertAsync(new CheckoutPreparationData { OrderId = orderId, Owner = "active-owner" });
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var writes = 0;
        using var fault = new TraceFault(database, info =>
        {
            if (info.TraceInfoStep == TraceInfoStep.BeforeExecute && IsPreparationWrite(info)) writes++;
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.NewCheckout().AcquirePreparationAsync(orderId, cancellation.Token));

        Assert.Equal(0, writes);
        using var read = database.Factory.GetDatabase();
        var row = await read.GetTable<CheckoutPreparationData>().SingleOrDefaultAsync();
        if (existingOwner) Assert.Equal("active-owner", row!.Owner);
        else Assert.Null(row);
    }

    [Fact]
    public async Task BusyOwnerDoesNotTriggerCleanup()
    {
        using var database = new StockReservationTests.ReservationDatabase();
        var orderId = Guid.NewGuid();
        using (var db = database.Factory.GetDatabase())
            await db.InsertAsync(new CheckoutPreparationData { OrderId = orderId, Owner = "active-owner", ProtectedIds = "[\"hold\"]" });
        var writes = 0;
        using var fault = new TraceFault(database, info =>
        {
            if (info.TraceInfoStep == TraceInfoStep.BeforeExecute && IsPreparationWrite(info)) writes++;
        });

        await Assert.ThrowsAsync<StockException>(() => database.NewCheckout().AcquirePreparationAsync(orderId, default));

        Assert.Equal(0, writes);
        using var read = database.Factory.GetDatabase();
        var row = await read.GetTable<CheckoutPreparationData>().SingleAsync();
        Assert.Equal("active-owner", row.Owner);
        Assert.Equal("[\"hold\"]", row.ProtectedIds);
    }

    private static bool IsPreparationWrite(TraceInfo info)
        => info.Command?.CommandText is string sql && sql.Contains("EkomCheckoutPreparation", StringComparison.Ordinal) &&
            (sql.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ||
             sql.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase));

    // Exercise the real SQLite boundary without adding production injection hooks.
    private sealed class TraceFault : IDisposable
    {
        private readonly Action<TraceInfo> _previous = DataConnection.DefaultOnTraceConnection;
        private readonly TraceLevel _level = DataConnection.TraceSwitch.Level;

        public TraceFault(StockReservationTests.ReservationDatabase database, Action<TraceInfo> action)
        {
            using var db = database.Factory.GetDatabase();
            var connectionString = db.ConnectionString;
            DataConnection.DefaultOnTraceConnection = info =>
            {
                if (info.DataConnection.ConnectionString == connectionString) action(info);
            };
            DataConnection.TraceSwitch.Level = TraceLevel.Info;
        }

        public void Dispose()
        {
            DataConnection.DefaultOnTraceConnection = _previous;
            DataConnection.TraceSwitch.Level = _level;
        }
    }
}
