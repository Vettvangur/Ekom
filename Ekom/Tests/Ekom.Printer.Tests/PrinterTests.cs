using Ekom.Printer.Configuration;
using Ekom.Printer.Migrations;
using Ekom.Printer.Protocol;
using Ekom.Printer.Rendering;
using Ekom.Printer.Storage;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ekom.Printer.Tests;

public sealed class PrinterTests
{
    [Fact]
    public async Task DefaultTemplateEncodesUntrustedValues()
    {
        var context = Snapshot() with
        {
            OrderNumber = "<script>alert(1)</script>",
            Lines = [new PickingSlipLine("<sku>", "<img src=x onerror=alert(1)>", "<variant>", 2)],
        };
        var html = await new DefaultPickingSlipHtmlRenderer().RenderAsync(context);
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.Contains("&lt;sku&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&lt;variant&gt;", html, StringComparison.Ordinal);
        Assert.Contains("@page", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateIdentityRespectsPolicyAndStation()
    {
        var first = Snapshot();
        var second = first with { TriggerStatus = OrderStatus.ReadyForDispatch };
        Assert.Equal(PrinterJobStore.GetJobId(first, PrintPolicy.OncePerOrder),
            PrinterJobStore.GetJobId(second, PrintPolicy.OncePerOrder));
        Assert.NotEqual(PrinterJobStore.GetJobId(first, PrintPolicy.OncePerStatus),
            PrinterJobStore.GetJobId(second, PrintPolicy.OncePerStatus));
        Assert.NotEqual(PrinterJobStore.GetJobId(first, PrintPolicy.OncePerOrder),
            PrinterJobStore.GetJobId(first with { StationAlias = "other" }, PrintPolicy.OncePerOrder));
    }

    [Fact]
    public void DisabledPrintingDoesNotRequireStations()
    {
        Assert.True(new PrinterOptionsValidator().Validate(null, new PrinterOptions()).Succeeded);
    }

    [Fact]
    public void EnabledPrintingRejectsInvalidRetentionAndDuplicateStations()
    {
        var station = new PrinterStationOptions { Alias = "warehouse", TokenSha256 = new string('A', 64), StoreAliases = ["main"] };
        station.PickingSlip.TriggerStatuses = [OrderStatus.ReadyForDispatch];
        var options = new PrinterOptions
        {
            Enabled = true,
            HtmlRetentionDays = 100,
            HistoryRetentionDays = 10,
            Stations = [station, station],
        };
        var result = new PrinterOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        Assert.Equal(2, result.Failures.Count());
    }

    [Fact]
    public async Task MigrationIsRepeatableAndQueueRejectsDuplicateOrders()
    {
        using var fixture = new DatabaseFixture();
        var migration = new PrinterMigration(fixture.Factory);
        await migration.ApplyAsync();
        await migration.ApplyAsync();
        var store = new PrinterJobStore(fixture.Factory);
        var snapshot = Snapshot();
        Assert.True(await store.EnqueueAsync(snapshot, new PickingSlipOptions()));
        Assert.False(await store.EnqueueAsync(snapshot with { TriggerStatus = OrderStatus.ReadyForDispatch }, new PickingSlipOptions()));
        var job = Assert.Single(await store.GetPendingAsync());
        await store.SaveHtmlAsync(job.Id, "<html>first</html>");
        await store.SaveHtmlAsync(job.Id, "<html>second</html>");
        await using var db = fixture.Factory.GetDatabase();
        var saved = await db.GetTable<PrintJob>().SingleAsync();
        Assert.Equal(PrintJobState.Ready, saved.State);
        Assert.Equal("<html>first</html>", saved.Html);
    }

    [Fact]
    public async Task RetentionPreservesUnfinishedDocumentsAndDuplicateTombstones()
    {
        using var fixture = new DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        var store = new PrinterJobStore(fixture.Factory);
        var snapshot = Snapshot() with { CreatedUtc = DateTime.UtcNow.AddDays(-100) };
        var unfinished = snapshot with { OrderId = Guid.NewGuid() };
        await store.EnqueueAsync(snapshot, new PickingSlipOptions());
        await store.EnqueueAsync(unfinished, new PickingSlipOptions());
        var id = PrinterJobStore.GetJobId(snapshot, PrintPolicy.OncePerOrder);
        await store.SaveHtmlAsync(id, "<html>completed</html>");
        await using var db = fixture.Factory.GetDatabase();
        await db.GetTable<PrintJob>().Where(x => x.Id == id)
            .Set(x => x.State, PrintJobState.Submitted)
            .Set(x => x.CompletedUtc, DateTime.UtcNow.AddDays(-100)).UpdateAsync();
        await store.CleanupAsync(new PrinterOptions(), DateTime.UtcNow);
        var completed = await db.GetTable<PrintJob>().SingleAsync(x => x.Id == id);
        Assert.Null(completed.Html);
        Assert.Null(completed.SnapshotJson);
        Assert.Equal(Guid.Empty, completed.OrderId);
        Assert.NotNull(Assert.Single(await store.GetPendingAsync()).SnapshotJson);
        Assert.False(await store.EnqueueAsync(snapshot, new PickingSlipOptions()));
    }

    internal static PickingSlipContext Snapshot() => new(Guid.NewGuid(), "ORDER-1", "main", "warehouse",
        OrderStatus.OfflinePayment, DateTime.UtcNow, [new PickingSlipLine("SKU", "Product", null, 1)]);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void IncompatibleAgentVersionsAreRejected(int version, bool compatible)
    {
        var result = AgentCompatibility.Check(version);
        Assert.Equal(compatible, result.Compatible);
        Assert.Equal(PrinterProtocol.CurrentVersion, result.RequiredProtocolVersion);
        if (!compatible) Assert.NotNull(result.Message);
    }

    [Fact]
    public void ConfigurationBindsMultipleStatusesAndAllowsCustomRenderer()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ekom:Printing:Enabled"] = "true",
            ["Ekom:Printing:Stations:0:Alias"] = "warehouse",
            ["Ekom:Printing:Stations:0:TokenSha256"] = new string('A', 64),
            ["Ekom:Printing:Stations:0:StoreAliases:0"] = "main",
            ["Ekom:Printing:Stations:0:PickingSlip:TriggerStatuses:0"] = "OfflinePayment",
            ["Ekom:Printing:Stations:0:PickingSlip:TriggerStatuses:1"] = "ReadyForDispatch",
        }).Build();
        var services = new ServiceCollection();
        services.AddEkomPrinter(configuration);
        var renderer = Mock.Of<IPickingSlipHtmlRenderer>();
        services.AddSingleton(renderer);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PrinterOptions>>().Value;
        Assert.Equal(2, options.Stations[0].PickingSlip.TriggerStatuses.Count);
        Assert.Same(renderer, provider.GetRequiredService<IPickingSlipHtmlRenderer>());
    }

    [Fact]
    public async Task NewerDatabaseSchemaRejectsPackageDowngrade()
    {
        using var fixture = new DatabaseFixture();
        var migration = new PrinterMigration(fixture.Factory);
        await migration.ApplyAsync();
        await using var db = fixture.Factory.GetDatabase();
        await db.GetTable<PrinterSchemaVersion>().Set(x => x.Version, 5).UpdateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => migration.ApplyAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ConcurrentStartupAndEnqueueKeepOneJob(int iteration)
    {
        Assert.InRange(iteration, 0, 4);
        using var fixture = new DatabaseFixture();
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            new PrinterMigration(fixture.Factory).ApplyAsync())));
        var snapshot = Snapshot();
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            new PrinterJobStore(fixture.Factory).EnqueueAsync(snapshot, new PickingSlipOptions()))));
        Assert.Single(results.Where(inserted => inserted));
    }

    internal sealed class DatabaseFixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        public DatabaseFixture()
        {
            var connectionString = $"Data Source=printer-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = connectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            Factory = new DatabaseFactory(configuration, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
        }

        public DatabaseFactory Factory { get; }
        public void Dispose() => _connection.Dispose();
    }
}
