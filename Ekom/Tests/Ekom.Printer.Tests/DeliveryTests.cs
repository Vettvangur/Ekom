using Ekom.Printer.Api;
using Ekom.Printer.Configuration;
using Ekom.Printer.Migrations;
using Ekom.Printer.Protocol;
using Ekom.Printer.Storage;
using LinqToDB;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Ekom.Printer.Tests;

public sealed class DeliveryTests
{
    [Fact]
    public async Task ConcurrentClaimsDeliverJobToOnlyOneAgent()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        var jobs = new PrinterJobStore(fixture.Factory);
        await ReadyAsync(jobs);
        var store = new PrinterDeliveryStore(fixture.Factory, Options.Create(new PrinterOptions()), TimeProvider.System);
        var claims = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() => store.ClaimAsync("warehouse"))));
        Assert.Single(claims.Where(claim => claim != null));
        await using var db = fixture.Factory.GetDatabase();
        Assert.Equal(1, await db.GetTable<PrinterDelivery>().CountAsync());
    }

    [Fact]
    public async Task AcknowledgementsAreStationScopedAndReplaySafe()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        await ReadyAsync(new PrinterJobStore(fixture.Factory));
        var store = new PrinterDeliveryStore(fixture.Factory, Options.Create(new PrinterOptions()), TimeProvider.System);
        Assert.Null(await store.ClaimAsync("other"));
        var job = Assert.IsType<ClaimedPrintJob>(await store.ClaimAsync("warehouse"));
        var acknowledgement = new PrintAcknowledgement(job.ClaimToken, true, "42");
        Assert.False(await store.AcknowledgeAsync("other", job.JobId, acknowledgement));
        Assert.False(await store.AcknowledgeAsync("warehouse", job.JobId, acknowledgement with { ClaimToken = Guid.NewGuid() }));
        Assert.True(await store.AcknowledgeAsync("warehouse", job.JobId, acknowledgement));
        Assert.True(await store.AcknowledgeAsync("warehouse", job.JobId, acknowledgement));
        Assert.False(await store.AcknowledgeAsync("warehouse", job.JobId, acknowledgement with { Submitted = false }));
        Assert.False(await store.AcknowledgeAsync("warehouse", job.JobId, acknowledgement with { SpoolJobId = "43" }));
        await using var db = fixture.Factory.GetDatabase();
        Assert.Equal(PrintJobState.Submitted, (await db.GetTable<PrintJob>().SingleAsync()).State);
    }

    [Fact]
    public async Task ExpiredClaimNeedsReviewAndCanReconcileLateAcknowledgement()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        await ReadyAsync(new PrinterJobStore(fixture.Factory));
        var clock = new TestClock();
        var store = new PrinterDeliveryStore(fixture.Factory, Options.Create(new PrinterOptions()), clock);
        var job = Assert.IsType<ClaimedPrintJob>(await store.ClaimAsync("warehouse"));
        clock.Advance(TimeSpan.FromHours(1));
        await store.ExpireClaimsAsync();
        Assert.Null(await store.ClaimAsync("warehouse"));
        await using var db = fixture.Factory.GetDatabase();
        Assert.Equal(PrintJobState.NeedsReview, (await db.GetTable<PrintJob>().SingleAsync()).State);
        Assert.True(await store.AcknowledgeAsync("warehouse", job.JobId, new PrintAcknowledgement(job.ClaimToken, true, "42")));
        Assert.Equal(PrintJobState.Submitted, (await db.GetTable<PrintJob>().SingleAsync()).State);
    }

    [Fact]
    public async Task HeartbeatTracksServerTimeAndBecomesOffline()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        var clock = new TestClock();
        var store = new PrinterDeliveryStore(fixture.Factory, Options.Create(new PrinterOptions()), clock);
        Assert.False((await store.GetHealthAsync("warehouse")).Online);
        await store.HeartbeatAsync("warehouse", new AgentHeartbeat(1, "0.1", "Office printer"));
        var health = await store.GetHealthAsync("warehouse");
        Assert.True(health.Online);
        Assert.True(health.Compatible);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, health.LastSeenUtc);
        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.False((await store.GetHealthAsync("warehouse")).Online);
    }

    [Fact]
    public async Task MigrationUpgradesVersionOneWithoutLosingJobs()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await using var db = fixture.Factory.GetDatabase();
        await db.CreateTableAsync<PrinterSchemaVersion>();
        await db.CreateTableAsync<PrintJob>();
        await db.InsertAsync(new PrinterSchemaVersion { Id = 1, Version = 1 });
        await new PrinterJobStore(fixture.Factory).EnqueueAsync(PrinterTests.Snapshot(), new PickingSlipOptions());
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        Assert.Equal(1, await db.GetTable<PrintJob>().CountAsync());
        Assert.Equal(4, (await db.GetTable<PrinterSchemaVersion>().SingleAsync()).Version);
        Assert.Equal(0, await db.GetTable<PrinterDelivery>().CountAsync());
    }

    [Fact]
    public void AuthenticationRequiresMatchingBearerToken()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        Assert.True(StationAccessFilter.Authenticate("Bearer " + token, hash));
        Assert.False(StationAccessFilter.Authenticate("Bearer " + new string('x', 64), hash));
        Assert.False(StationAccessFilter.Authenticate("Basic " + token, hash));
        Assert.False(StationAccessFilter.Authenticate("Bearer short", hash));
        Assert.False(StationAccessFilter.Authenticate("Bearer " + token, "invalid"));
    }

    [Fact]
    public async Task DeliveryRetentionPreservesUnresolvedClaims()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        var jobs = new PrinterJobStore(fixture.Factory);
        await ReadyAsync(jobs);
        await ReadyAsync(jobs);
        var clock = new TestClock();
        var store = new PrinterDeliveryStore(fixture.Factory, Options.Create(new PrinterOptions()), clock);
        var submitted = Assert.IsType<ClaimedPrintJob>(await store.ClaimAsync("warehouse"));
        var unresolved = Assert.IsType<ClaimedPrintJob>(await store.ClaimAsync("warehouse"));
        await store.AcknowledgeAsync("warehouse", submitted.JobId, new PrintAcknowledgement(submitted.ClaimToken, true, "42"));
        clock.Advance(TimeSpan.FromDays(100));
        await store.ExpireClaimsAsync();
        await store.CleanupAsync();
        await using var db = fixture.Factory.GetDatabase();
        Assert.Equal(unresolved.JobId, (await db.GetTable<PrinterDelivery>().SingleAsync()).JobId);
    }

    internal static async Task ReadyAsync(PrinterJobStore jobs)
    {
        var snapshot = PrinterTests.Snapshot();
        await jobs.EnqueueAsync(snapshot, new PickingSlipOptions());
        await jobs.SaveHtmlAsync(PrinterJobStore.GetJobId(snapshot, PrintPolicy.OncePerOrder), "<html><body>Picking slip</body></html>");
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
