using Ekom.Printer.Agent.Client;
using Ekom.Printer.Protocol;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Ekom.Printer.Agent.Tests;

public sealed class AgentTests
{
    [Fact]
    public async Task LostAcknowledgementRetriesReportingWithoutPrintingAgain()
    {
        using var fixture = new Fixture();
        fixture.Handler.FailAcknowledgement = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Processor.ProcessNextAsync());
        Assert.Equal(1, fixture.Printer.Submissions);
        Assert.True(Assert.Single(await fixture.Journal.ReadAsync()).Result?.Submitted);
        fixture.Handler.FailAcknowledgement = false;
        Assert.False(await fixture.Processor.ProcessNextAsync());
        Assert.Equal(1, fixture.Printer.Submissions);
        Assert.Empty(await fixture.Journal.ReadAsync());
        Assert.True(fixture.Handler.LastAcknowledgement?.Submitted);
    }

    [Fact]
    public async Task UncertainSubmissionIsReportedForReviewAfterRestart()
    {
        using var fixture = new Fixture();
        fixture.Printer.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => fixture.Processor.ProcessNextAsync());
        Assert.Null(Assert.Single(await fixture.Journal.ReadAsync()).Result);
        var restarted = new AgentPrintProcessor(fixture.Client, fixture.Journal, fixture.Printer, TimeProvider.System);
        Assert.False(await restarted.ProcessNextAsync());
        Assert.Equal(1, fixture.Printer.Submissions);
        Assert.False(fixture.Handler.LastAcknowledgement?.Submitted);
        Assert.Empty(await fixture.Journal.ReadAsync());
    }

    [Fact]
    public async Task ExpiredClaimIsNeverPrinted()
    {
        using var fixture = new Fixture();
        fixture.Handler.Job = fixture.Handler.Job with { ExpiresUtc = DateTime.UtcNow.AddMinutes(-1) };
        Assert.True(await fixture.Processor.ProcessNextAsync());
        Assert.Equal(0, fixture.Printer.Submissions);
        Assert.False(fixture.Handler.LastAcknowledgement?.Submitted);
    }

    [Fact]
    public async Task IncompatibleServerStopsBeforePrinting()
    {
        using var fixture = new Fixture();
        fixture.Handler.Incompatible = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Processor.ProcessNextAsync());
        Assert.Equal(0, fixture.Printer.Submissions);
    }

    [Fact]
    public async Task CorruptJournalStopsBeforeRequestingNewJobs()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(fixture.DirectoryPath);
        await File.WriteAllTextAsync(Path.Combine(fixture.DirectoryPath, new string('A', 64) + ".json"), "not json");
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => fixture.Processor.ProcessNextAsync());
        Assert.Equal(0, fixture.Handler.Claims);
    }

    [Fact]
    public async Task SharedJournalPreventsConcurrentProcessors()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(fixture.DirectoryPath);
        using var heldLock = new FileStream(Path.Combine(fixture.DirectoryPath, "agent.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        await Assert.ThrowsAsync<IOException>(() => fixture.Processor.ProcessNextAsync());
        Assert.Equal(0, fixture.Handler.Claims);
        Assert.Equal(0, fixture.Printer.Submissions);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient _http;
        public Fixture()
        {
            _http = new HttpClient(Handler);
            Client = new PrinterAgentClient(_http, new Uri("https://warehouse.example/"), "main", new string('a', 64));
            Journal = new SubmissionJournal(DirectoryPath);
            Processor = new AgentPrintProcessor(Client, Journal, Printer, TimeProvider.System);
        }

        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "ekom-printer-test-" + Guid.NewGuid().ToString("N"));
        public ServerHandler Handler { get; } = new();
        public TestPrinter Printer { get; } = new();
        public PrinterAgentClient Client { get; }
        public SubmissionJournal Journal { get; }
        public AgentPrintProcessor Processor { get; }
        public void Dispose()
        {
            _http.Dispose();
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
        }
    }

    private sealed class TestPrinter : IHtmlPrinter
    {
        public int Submissions { get; private set; }
        public bool Fail { get; set; }
        public Task<string> SubmitAsync(string html, int copies, HtmlPrintPolicy policy, CancellationToken cancellationToken = default)
        {
            Assert.False(policy.JavaScriptEnabled);
            Assert.False(policy.ExternalNetworkRequestsEnabled);
            Assert.False(policy.LocalFileAccessEnabled);
            Submissions++;
            if (Fail) throw new IOException("Simulated uncertain spool submission.");
            return Task.FromResult("42");
        }
    }

    private sealed class ServerHandler : HttpMessageHandler
    {
        public ClaimedPrintJob Job { get; set; } = new(new string('A', 64), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5),
            "<html>Picking slip</html>", 1, new HtmlPrintPolicy());
        public int Claims { get; private set; }
        public bool FailAcknowledgement { get; set; }
        public bool Incompatible { get; set; }
        public PrintAcknowledgement? LastAcknowledgement { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("1", request.Headers.GetValues(PrinterProtocol.VersionHeader).Single());
            if (Incompatible) return new HttpResponseMessage(HttpStatusCode.UpgradeRequired);
            if (request.RequestUri!.AbsolutePath.EndsWith("/claim", StringComparison.Ordinal))
            {
                Claims++;
                return Claims == 1 ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Job) }
                    : new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            if (FailAcknowledgement) throw new HttpRequestException("Simulated dropped response.");
            LastAcknowledgement = await request.Content!.ReadFromJsonAsync<PrintAcknowledgement>(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
