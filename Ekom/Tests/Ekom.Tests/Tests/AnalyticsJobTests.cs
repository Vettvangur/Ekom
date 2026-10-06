using Ekom.Analytics;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class AnalyticsJobTests
{
    [Fact]
    public async Task DisabledAndInvalidJobsDoNotInitializeOrScanStorage()
    {
        using var database = new JobDatabase(createSource: false);
        database.Options.Value.Enabled = false;
        var jobs = database.CreateJobs();
        Assert.False(await jobs.TickAsync());
        Assert.Null(await jobs.GetAsync(Guid.NewGuid()));
        Assert.Empty(await jobs.ListAsync("main"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => jobs.StartRebuildAsync("main"));
        database.Options.Value.Enabled = true;
        database.Options.Value.BatchSize = 0;
        Assert.False(await jobs.TickAsync());
        using var connection = database.Factory.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name LIKE 'EkomAnalytics%'";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task LeaseShorterThanCommandTimeoutDisablesJobs()
    {
        using var database = new JobDatabase(createSource: false);
        database.Options.Value.LeaseDuration = TimeSpan.FromSeconds(1);
        database.Options.Value.DatabaseCommandTimeoutSeconds = 30;
        Assert.False(await database.CreateJobs().TickAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.CreateJobs().StartRebuildAsync("main"));
    }

    [Fact]
    public async Task RebuildCapturesBoundAndBatchesOnlyItsStoreIncludingIncompleteOrders()
    {
        using var database = new JobDatabase();
        database.Options.Value.BatchSize = 2;
        var first = database.AddOrder("main");
        database.AddOrder("other");
        var second = database.AddOrder("main");
        var third = database.AddOrder("main");
        var jobs = database.CreateJobs();
        var job = await jobs.StartRebuildAsync("main");
        Assert.Equal("Pending", job.Status);
        Assert.Equal(third.ReferenceId, job.MaximumReferenceId);
        Assert.Equal(0, job.ProcessedCount);
        var afterStart = database.AddOrder("main");

        Assert.True(await jobs.TickAsync());
        var checkpoint = (await jobs.GetAsync(job.JobId))!;
        Assert.Equal("Running", checkpoint.Status);
        Assert.Equal(2, checkpoint.ProcessedCount);
        Assert.Equal(second.ReferenceId, checkpoint.LastReferenceId);
        Assert.True(await jobs.TickAsync());
        Assert.True(await jobs.TickAsync());
        var completed = (await jobs.GetAsync(job.JobId))!;
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(3, completed.ProcessedCount);
        Assert.Equal(3, completed.SucceededCount);
        Assert.Equal(3, completed.RemovedCount);
        Assert.Equal(0, completed.FailedCount);
        Assert.Equal(third.ReferenceId, completed.LastReferenceId);
        Assert.NotEqual(afterStart.ReferenceId, completed.LastReferenceId);
        Assert.NotNull(completed.CompletedAtUtc);
        using var verification = database.Factory.GetDatabase();
        Assert.Equal(2, verification.GetTable<AnalyticsOrderData>().Count());
        Assert.False(verification.GetTable<AnalyticsOrderData>().Any(x => x.OrderId == first.Id));
        Assert.True(verification.GetTable<AnalyticsOrderData>().Any(x => x.OrderId == afterStart.Id));
        Assert.Equal(5, verification.OrderData.Count());
        Assert.All(verification.OrderData, x => Assert.Equal("Incomplete", x.OrderStatusCol));
        Assert.Null(verification.GetTable<AnalyticsLeaseData>().Single().Owner);
    }

    [Fact]
    public async Task NeverProjectedIncompleteCartsAreSkippedWhilePreviousProjectionsAreRemoved()
    {
        using var database = new JobDatabase();
        database.AddOrder("main", projected: false);
        var projected = database.AddOrder("main");
        database.AddOrder("main", projected: false);
        var jobs = database.CreateJobs();
        var job = await jobs.StartRebuildAsync("main");

        Assert.True(await jobs.TickAsync());
        Assert.True(await jobs.TickAsync());

        var completed = (await jobs.GetAsync(job.JobId))!;
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(1, completed.ProcessedCount);
        Assert.Equal(1, completed.SucceededCount);
        Assert.Equal(1, completed.RemovedCount);
        Assert.Equal(projected.ReferenceId, completed.LastReferenceId);
        using var db = database.Factory.GetDatabase();
        Assert.Empty(db.GetTable<AnalyticsOrderData>());
        Assert.Empty(db.GetTable<OrderActivityLog>());
        Assert.Equal(3, db.OrderData.Count());
    }

    [Fact]
    public async Task ListReturnsLatestFiftyJobsForOnlyTheRequiredStore()
    {
        using var database = new JobDatabase();
        var jobs = database.CreateJobs();
        var oldest = await jobs.StartRebuildAsync("main");
        Assert.True(await jobs.ControlAsync(oldest.JobId, "cancel"));
        var start = DateTime.UtcNow.AddDays(-1);
        Guid newestId = Guid.Empty;
        using (var db = database.Factory.GetDatabase())
        {
            db.GetTable<AnalyticsJobData>().Where(x => x.JobId == oldest.JobId)
                .Set(x => x.StartedAtUtc, start.AddMinutes(-1)).Update();
            for (var i = 0; i < 55; i++)
            {
                var id = Guid.NewGuid();
                if (i == 54) newestId = id;
                db.Insert(new AnalyticsJobData
                {
                    JobId = id,
                    StoreAlias = "main",
                    Kind = "Scheduled",
                    Status = i == 54 ? "Interrupted" : "Completed",
                    StartedAtUtc = start.AddMinutes(i),
                    UpdatedAtUtc = start.AddMinutes(i),
                });
            }
            db.Insert(new AnalyticsJobData
            {
                JobId = Guid.NewGuid(),
                StoreAlias = "other",
                Status = "Pending",
                StartedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        }

        var list = await jobs.ListAsync("main");

        Assert.Equal(50, list.Count);
        Assert.Equal(newestId, list[0].JobId);
        Assert.Equal("Interrupted", list[0].Status);
        Assert.All(list, x => Assert.Equal("main", x.StoreAlias));
        Assert.DoesNotContain(list, x => x.JobId == oldest.JobId);
        Assert.Equal(list.OrderByDescending(x => x.StartedAtUtc).Select(x => x.JobId), list.Select(x => x.JobId));
        Assert.Empty(await jobs.ListAsync("missing"));
        await Assert.ThrowsAsync<ArgumentException>(() => jobs.ListAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => jobs.ListAsync(new string('a', 101)));
    }

    [Theory]
    [InlineData("RefreshInterval")]
    [InlineData("LookbackWindow")]
    [InlineData("DelayBetweenBatches")]
    [InlineData("LeaseDuration")]
    [InlineData("DatabaseCommandTimeoutSeconds")]
    [InlineData("BatchSize")]
    public async Task ExcessiveOptionsAreRejectedWithoutInitializingStorage(string name)
    {
        using var database = new JobDatabase(createSource: false);
        var options = database.Options.Value;
        switch (name)
        {
            case "RefreshInterval": options.RefreshInterval = TimeSpan.MaxValue; break;
            case "LookbackWindow": options.LookbackWindow = TimeSpan.FromDays(367); break;
            case "DelayBetweenBatches": options.DelayBetweenBatches = TimeSpan.MaxValue; break;
            case "LeaseDuration": options.LeaseDuration = TimeSpan.MaxValue; break;
            case "DatabaseCommandTimeoutSeconds": options.DatabaseCommandTimeoutSeconds = int.MaxValue; break;
            case "BatchSize": options.BatchSize = 10001; break;
        }
        Assert.False(options.IsValid(out var error));
        Assert.NotNull(error);
        var jobs = database.CreateJobs();
        Assert.False(await jobs.TickAsync());
        Assert.Empty(await jobs.ListAsync("main"));
    }

    [Fact]
    public void TimerAndLeaseValidationAcceptsBoundariesAndPreservesDefaults()
    {
        var options = new AnalyticsOptions();
        Assert.True(options.IsValid(out var error));
        Assert.Null(error);
        var maximum = TimeSpan.FromMilliseconds(uint.MaxValue - 1L);
        options.RefreshInterval = maximum;
        options.LookbackWindow = TimeSpan.FromDays(366);
        options.LeaseDuration = maximum;
        options.DelayBetweenBatches = maximum - TimeSpan.FromMilliseconds(1);
        options.DatabaseCommandTimeoutSeconds = int.MaxValue / 1000;
        Assert.True(options.IsValid(out _));
        options.DelayBetweenBatches = maximum;
        Assert.False(options.IsValid(out _));
        options.DelayBetweenBatches = TimeSpan.Zero;
        options.RefreshInterval = maximum + TimeSpan.FromTicks(1);
        Assert.False(options.IsValid(out _));
        options.RefreshInterval = TimeSpan.FromMinutes(30);
        options.DatabaseCommandTimeoutSeconds = 30;
        options.LeaseDuration = TimeSpan.FromSeconds(35);
        Assert.True(options.IsValid(out _));
        options.LeaseDuration -= TimeSpan.FromTicks(1);
        Assert.False(options.IsValid(out _));
    }

    [Fact]
    public void SqlServerLeaseExpiryExpressionGeneratesServerSideUtcDateAdditionWithoutConnecting()
    {
        using var db = new DataConnection(new DataOptions().UseSqlServer(
            "Server=not-used;Database=not-used;Integrated Security=true",
            SqlServerVersion.v2012, SqlServerProvider.MicrosoftDataSqlClient));
        var sql = db.GetTable<AnalyticsLeaseData>()
            .Where(x => x.ExpiresAtUtc > AnalyticsJobService.LeaseClockUtc().AddSeconds(300)).ToString();
        Assert.Contains("SYSUTCDATETIME()", sql!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DateAdd", sql!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LeaseAcquisitionAndRenewalUseDatabaseClockPlusDuration()
    {
        using var database = new JobDatabase();
        database.Options.Value.LeaseDuration = TimeSpan.FromSeconds(60);
        database.AddOrder("main");
        var jobs = database.CreateJobs();
        var job = await jobs.StartRebuildAsync("main");
        Assert.True(await jobs.TickAsync());
        using var db = database.Factory.GetDatabase();
        var lease = db.GetTable<AnalyticsLeaseData>()
            .Select(x => new { Now = AnalyticsJobService.LeaseClockUtc(), x.ExpiresAtUtc }).Single();
        Assert.NotNull(lease.ExpiresAtUtc);
        Assert.InRange((lease.ExpiresAtUtc.Value - lease.Now).TotalSeconds, 55, 61);
        var running = (await jobs.GetAsync(job.JobId))!;
        Assert.InRange((running.LeaseUntilUtc!.Value - lease.Now).TotalSeconds, 55, 61);
        var sql = db.GetTable<AnalyticsLeaseData>()
            .Where(x => x.ExpiresAtUtc > AnalyticsJobService.LeaseClockUtc().AddSeconds(60)).ToString();
        Assert.Contains("now", sql!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("strftime", sql!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedOrderIsCountedAndAdvancedWithoutRetry()
    {
        using var database = new JobDatabase();
        database.AddOrder("main", "ReadyForDispatch", "invalid-json");
        database.AddOrder("main");
        var jobs = database.CreateJobs();
        var job = await jobs.StartRebuildAsync("main");
        Assert.True(await jobs.TickAsync());
        Assert.True(await jobs.TickAsync());
        var completed = (await jobs.GetAsync(job.JobId))!;
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(2, completed.ProcessedCount);
        Assert.Equal(1, completed.FailedCount);
        Assert.Equal(1, completed.SucceededCount);
        using var db = database.Factory.GetDatabase();
        Assert.Single(db.GetTable<OrderActivityLog>());
    }

    [Fact]
    public async Task PauseBlocksNewJobAndResumeRetainsCheckpoint()
    {
        using var database = new JobDatabase();
        database.Options.Value.BatchSize = 1;
        var first = database.AddOrder("main");
        database.AddOrder("main");
        var jobs = database.CreateJobs();
        var job = await jobs.StartRebuildAsync("main");
        Assert.True(await jobs.TickAsync());
        Assert.True(await jobs.ControlAsync(job.JobId, "pause"));
        Assert.True(await jobs.TickAsync());
        var paused = (await jobs.GetAsync(job.JobId))!;
        Assert.Equal("Paused", paused.Status);
        Assert.Equal(first.ReferenceId, paused.LastReferenceId);
        Assert.Equal(1, paused.ProcessedCount);
        Assert.False(await jobs.TickAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => jobs.StartRebuildAsync("other"));
        Assert.True(await jobs.ControlAsync(job.JobId, "resume"));
        Assert.True(await jobs.TickAsync());
        Assert.True(await jobs.TickAsync());
        Assert.Equal(2, (await jobs.GetAsync(job.JobId))!.ProcessedCount);
        Assert.Equal("Completed", (await jobs.GetAsync(job.JobId))!.Status);
    }

    [Fact]
    public async Task PendingPauseAndCancelUseStateChecksAndReleaseReservation()
    {
        using var database = new JobDatabase();
        var jobs = database.CreateJobs();
        await Assert.ThrowsAsync<ArgumentException>(() => jobs.StartRebuildAsync(" "));
        var job = await jobs.StartRebuildAsync("main");
        Assert.False(await jobs.ControlAsync(job.JobId, "resume"));
        Assert.True(await jobs.ControlAsync(job.JobId, "pause"));
        Assert.False(await jobs.ControlAsync(job.JobId, "pause"));
        Assert.True(await jobs.ControlAsync(job.JobId, "cancel"));
        Assert.Equal("Cancelled", (await jobs.GetAsync(job.JobId))!.Status);
        Assert.False(await jobs.ControlAsync(job.JobId, "resume"));
        await Assert.ThrowsAsync<ArgumentException>(() => jobs.ControlAsync(job.JobId, "delete"));
        Assert.False(await jobs.ControlAsync(Guid.NewGuid(), "cancel"));
        Assert.Equal("Pending", (await jobs.StartRebuildAsync("other")).Status);
    }

    [Fact]
    public async Task CancelCannotBeDowngradedByPause()
    {
        using var database = new JobDatabase();
        database.AddOrder("main");
        var jobs = database.CreateJobs();
        var job = await jobs.StartRebuildAsync("main");
        await jobs.TickAsync();
        Assert.True(await jobs.ControlAsync(job.JobId, "cancel"));
        Assert.False(await jobs.ControlAsync(job.JobId, "pause"));
        Assert.True(await jobs.TickAsync());
        Assert.Equal("Cancelled", (await jobs.GetAsync(job.JobId))!.Status);
    }

    [Fact]
    public async Task SecondInstanceCannotProcessLiveLeaseAndExpiredJobRequiresManualResume()
    {
        using var database = new JobDatabase();
        database.Options.Value.BatchSize = 1;
        database.AddOrder("main");
        database.AddOrder("main");
        var first = database.CreateJobs();
        var second = database.CreateJobs();
        var job = await first.StartRebuildAsync("main");
        Assert.True(await first.TickAsync());
        Assert.False(await second.TickAsync());
        Assert.False(await second.ControlAsync(job.JobId, "resume"));
        using (var db = database.Factory.GetDatabase())
        {
            var expired = DateTime.UtcNow.AddMinutes(-1);
            db.GetTable<AnalyticsLeaseData>().Where(x => x.Id == 1).Set(x => x.ExpiresAtUtc, expired).Update();
            db.GetTable<AnalyticsJobData>().Where(x => x.JobId == job.JobId).Set(x => x.LeaseUntilUtc, expired).Update();
        }
        Assert.False(await second.TickAsync());
        var interrupted = (await second.GetAsync(job.JobId))!;
        Assert.Equal("Interrupted", interrupted.Status);
        Assert.Equal(1, interrupted.ProcessedCount);
        Assert.False(await first.TickAsync());
        Assert.True(await second.ControlAsync(job.JobId, "resume"));
        Assert.True(await second.TickAsync());
        Assert.True(await second.TickAsync());
        Assert.Equal("Completed", (await second.GetAsync(job.JobId))!.Status);
        Assert.Equal(2, (await second.GetAsync(job.JobId))!.ProcessedCount);
    }

    [Fact]
    public async Task ShutdownBetweenBatchesInterruptsWithoutLosingCheckpointAndAllowsManualResume()
    {
        using var database = new JobDatabase();
        database.Options.Value.BatchSize = 1;
        database.AddOrder("main");
        database.AddOrder("main");
        var jobs = database.CreateJobs();
        var job = await jobs.StartRebuildAsync("main");
        Assert.True(await jobs.TickAsync());

        await jobs.InterruptOwnedAsync(CancellationToken.None);

        var interrupted = (await jobs.GetAsync(job.JobId))!;
        Assert.Equal("Interrupted", interrupted.Status);
        Assert.Equal(1, interrupted.ProcessedCount);
        Assert.Null(interrupted.LeaseOwner);
        using (var db = database.Factory.GetDatabase())
            Assert.Null(db.GetTable<AnalyticsLeaseData>().Single().Owner);
        var restarted = database.CreateJobs();
        Assert.False(await restarted.TickAsync());
        Assert.True(await restarted.ControlAsync(job.JobId, "resume"));
        Assert.True(await restarted.TickAsync());
        Assert.True(await restarted.TickAsync());
        Assert.Equal("Completed", (await restarted.GetAsync(job.JobId))!.Status);
        Assert.Equal(2, (await restarted.GetAsync(job.JobId))!.ProcessedCount);
    }

    [Fact]
    public async Task UnexpectedBatchQueryFailureInterruptsAndDoesNotAutomaticallyRetry()
    {
        using var database = new JobDatabase();
        database.AddOrder("main");
        var jobs = database.CreateJobs();
        var job = await jobs.StartRebuildAsync("main");
        using (var db = database.Factory.GetDatabase()) db.DropTable<OrderData>();

        Assert.False(await jobs.TickAsync());

        var interrupted = (await jobs.GetAsync(job.JobId))!;
        Assert.Equal("Interrupted", interrupted.Status);
        Assert.Equal(0, interrupted.ProcessedCount);
        Assert.Equal(0, interrupted.LastReferenceId);
        Assert.False(await jobs.TickAsync());
        using var verification = database.Factory.GetDatabase();
        Assert.Null(verification.GetTable<AnalyticsLeaseData>().Single().Owner);
    }

    [Fact]
    public async Task CancelledTickDoesNotClaimOrAdvancePendingJob()
    {
        using var database = new JobDatabase();
        database.AddOrder("main");
        var jobs = database.CreateJobs();
        var job = await jobs.StartRebuildAsync("main");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => jobs.TickAsync(cancellation.Token));
        var pending = (await jobs.GetAsync(job.JobId))!;
        Assert.Equal("Pending", pending.Status);
        Assert.Equal(0, pending.ProcessedCount);
        Assert.Null(pending.LeaseOwner);
    }

    [Fact]
    public async Task ScheduledJobsUseLocalWindowForAnyChangedDateAndDoNotQueueOtherStores()
    {
        using var database = new JobDatabase();
        database.Options.Value.ScheduledRefreshEnabled = true;
        var old = DateTime.Now.AddDays(-30);
        database.AddOrder("main", created: old, updated: old);
        database.AddOrder("main", created: old, updated: DateTime.Now);
        database.AddOrder("main", created: old, updated: old, paid: DateTime.Now);
        database.AddOrder("main", created: DateTime.Now, updated: old);
        database.AddOrder("other");
        var jobs = database.CreateJobs();
        Assert.True(await jobs.TickAsync());
        Guid mainId;
        using (var db = database.Factory.GetDatabase())
        {
            var scheduled = db.GetTable<AnalyticsJobData>().Single();
            mainId = scheduled.JobId;
            Assert.Equal("main", scheduled.StoreAlias);
            Assert.Equal("Scheduled", scheduled.Kind);
            Assert.Equal(3, scheduled.ProcessedCount);
            Assert.NotNull(scheduled.WindowStart);
        }
        Assert.True(await jobs.TickAsync());
        Assert.Equal("Completed", (await jobs.GetAsync(mainId))!.Status);
        Assert.True(await jobs.TickAsync());
        using var verification = database.Factory.GetDatabase();
        Assert.Equal(2, verification.GetTable<AnalyticsJobData>().Count());
        Assert.Equal("other", verification.GetTable<AnalyticsJobData>().Single(x => x.Status == "Running").StoreAlias);
        Assert.True(await jobs.TickAsync());
        Assert.False(await jobs.TickAsync());
    }

    private sealed class JobDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;

        public JobDatabase(bool createSource = true)
        {
            var connectionString = $"Data Source=analytics-jobs-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = connectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            Factory = new DatabaseFactory(configuration, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
            Options = Microsoft.Extensions.Options.Options.Create(new AnalyticsOptions { Enabled = true, ScheduledRefreshEnabled = false });
            if (!createSource) return;
            using var db = Factory.GetDatabase();
            db.CreateTable<OrderData>();
            db.CreateTable<OrderActivityLog>();
            db.CreateTable<AnalyticsOrderData>();
        }

        public DatabaseFactory Factory { get; }
        public IOptions<AnalyticsOptions> Options { get; }

        public AnalyticsJobService CreateJobs()
        {
            var schema = new AnalyticsSchema(Factory, Options, NullLogger<AnalyticsSchema>.Instance);
            var projection = new AnalyticsProjectionService(Factory, schema,
                new AnalyticsSnapshotMapper(new EmailAnalyticsCustomerIdentityResolver(), Options), Options,
                new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, Factory), NullLogger<AnalyticsProjectionService>.Instance);
            return new AnalyticsJobService(Factory, schema, projection, Options, NullLogger<AnalyticsJobService>.Instance);
        }

        public (Guid Id, int ReferenceId) AddOrder(string store, string status = "Incomplete", string snapshot = "not-json",
            DateTime? created = null, DateTime? updated = null, DateTime? paid = null, bool projected = true)
        {
            var id = Guid.NewGuid();
            using var db = Factory.GetDatabase();
            var referenceId = db.InsertWithInt32Identity(new OrderData
            {
                UniqueId = id,
                StoreAlias = store,
                Currency = "en-US",
                OrderStatusCol = status,
                OrderInfo = snapshot,
                CreateDate = created ?? DateTime.Now,
                UpdateDate = updated ?? DateTime.Now,
                PaidDate = paid,
            });
            // Most worker-state tests intentionally use an already-projected incomplete
            // order so that the projection's removal path is an eligible candidate.
            if (projected && status == "Incomplete")
                db.Insert(new AnalyticsOrderData { OrderId = id, StoreAlias = store, CurrencyCode = "USD" });
            return (id, referenceId);
        }

        public void Dispose() => _connection.Dispose();
    }
}
