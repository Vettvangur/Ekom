using Ekom.Algolia;
using Ekom.Algolia.Events;
using Ekom.Algolia.Indexing;
using Ekom.Algolia.Models.Events;
using Ekom.Algolia.Services;
using Ekom.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

/// <summary>
/// Insights events and stock availability updates are queued by the caller and sent to Algolia by a worker.
/// </summary>
public sealed class AlgoliaBackgroundDispatchTests
{
    [Fact]
    public async Task InsightsEventsAreQueuedWithoutCallingAlgoliaAndSentByTheWorker()
    {
        using var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://insights.algolia.io/1/") };
        var queue = new AlgoliaInsightsQueue(NullLogger<AlgoliaInsightsQueue>.Instance);
        using var worker = new AlgoliaInsightsWorker(
            queue, new AlgoliaInsightsClient(http, NullLogger<AlgoliaInsightsClient>.Instance), NullLogger<AlgoliaInsightsWorker>.Instance);
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var send = queue.SendEventsAsync([Event("product-1"), Event("product-2")]);

        Assert.True(send.IsCompletedSuccessfully);
        Assert.Empty(handler.Bodies);

        await worker.SendPendingAsync(default);

        // Both events travel in one request and keep the time they were queued.
        using var document = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var events = document.RootElement.GetProperty("events");
        Assert.Equal(2, events.GetArrayLength());
        Assert.Equal("product-2", events[1].GetProperty("objectIDs")[0].GetString());
        var queuedAt = events[0].GetProperty("timestamp").GetInt64();
        Assert.InRange(queuedAt, before, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Assert.Equal(queuedAt, events[1].GetProperty("timestamp").GetInt64());
    }

    [Fact]
    public async Task InsightsWorkerSplitsLargeBacklogsAndSurvivesFailedRequests()
    {
        using var handler = new RecordingHandler { Fail = true };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://insights.algolia.io/1/") };
        var queue = new AlgoliaInsightsQueue(NullLogger<AlgoliaInsightsQueue>.Instance);
        using var worker = new AlgoliaInsightsWorker(
            queue, new AlgoliaInsightsClient(http, NullLogger<AlgoliaInsightsClient>.Instance), NullLogger<AlgoliaInsightsWorker>.Instance);
        await queue.SendEventsAsync(Enumerable.Range(0, AlgoliaInsightsWorker.MaxEventsPerRequest + 1).Select(i => Event($"product-{i}")).ToList());

        await worker.SendPendingAsync(default);

        Assert.Equal(2, handler.Attempts);
        Assert.False(queue.Reader.TryRead(out _));

        handler.Fail = false;
        await queue.SendEventsAsync([Event("later")]);
        await worker.SendPendingAsync(default);

        Assert.Contains("later", Assert.Single(handler.Bodies), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InsightsQueueDropsEventsWhenFullInsteadOfWaiting()
    {
        var queue = new AlgoliaInsightsQueue(NullLogger<AlgoliaInsightsQueue>.Instance, capacity: 2);

        var send = queue.SendEventsAsync([Event("a"), Event("b"), Event("c")]);

        Assert.True(send.IsCompletedSuccessfully);
        await send;
        Assert.True(queue.Reader.TryRead(out var first));
        Assert.Equal("a", Assert.Single(first!.ObjectIds));
        Assert.True(queue.Reader.TryRead(out _));
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public void AddAlgoliaRoutesInsightsEventsThroughTheQueue()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ekom:Algolia:ApplicationId"] = "test",
                ["Ekom:Algolia:AdminApiKey"] = "test",
                ["Ekom:Algolia:SearchApiKey"] = "test",
            }).Build())
            .AddLogging()
            .AddAlgolia()
            .BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<AlgoliaInsightsQueue>(), provider.GetRequiredService<IAlgoliaInsightsClient>());
    }

    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(false, false, 0)]
    [InlineData(true, true, 0)]
    public async Task StockChangesAreQueuedInsteadOfUpdatingAlgoliaInline(bool availabilityUpdates, bool suppressed, int expected)
    {
        var options = Options.Create(AlgoliaOptions(availabilityUpdates));
        var queue = new AlgoliaAvailabilityQueue(options);
        using var provider = new ServiceCollection().AddLogging().AddSingleton(queue).BuildServiceProvider();
        var events = new AlgoliaEkomEvents(provider.GetRequiredService<IServiceScopeFactory>(), options);
        var change = new StockChangedEventArgs { Key = Guid.NewGuid(), StoreAlias = "Web", OldValue = 1, NewValue = 0 };
        events.Initialize();
        try
        {
            using (suppressed ? AlgoliaIndexingScope.Suppress() : null)
                await StockEvents.OnStockChangedAsync(this, change);
        }
        finally { events.Terminate(); }

        Assert.Equal(expected == 1, queue.Reader.TryRead(out var queued));
        if (expected == 1)
            Assert.Same(change, queued);
    }

    [Fact]
    public async Task AvailabilityWorkerAppliesChangesInOrderAndContinuesAfterAFailure()
    {
        var queue = new AlgoliaAvailabilityQueue(Options.Create(AlgoliaOptions(true)));
        var service = new RecordingAvailabilityService();
        using var worker = new AlgoliaAvailabilityWorker(queue, service, NullLogger<AlgoliaAvailabilityWorker>.Instance);
        var changes = Enumerable.Range(0, 3)
            .Select(i => new StockChangedEventArgs { Key = Guid.NewGuid(), OldValue = i, NewValue = i + 1 })
            .ToList();
        service.FailOn = changes[1].Key;
        foreach (var change in changes)
            Assert.True(queue.TryEnqueue(change));

        await worker.ProcessPendingAsync(default);

        Assert.Equal(changes.Select(x => x.Key), service.Applied);
        Assert.False(queue.Reader.TryRead(out _));
    }

    private static AlgoliaInsightsEvent Event(string objectId) => new()
    {
        EventType = "conversion",
        EventName = "Purchased",
        Index = "products",
        UserToken = "user-token",
        ObjectIds = [objectId],
    };

    private static AlgoliaOptions AlgoliaOptions(bool availabilityUpdates) => new()
    {
        ApplicationId = "test",
        AdminApiKey = "test",
        SearchApiKey = "test",
        Enabled = true,
        Stores = [new AlgoliaStoreOptions { Alias = "Web", EnableAvailabilityUpdates = availabilityUpdates }],
    };

    private sealed class RecordingAvailabilityService : IAlgoliaAvailabilityUpdateService
    {
        public List<Guid> Applied { get; } = new();

        public Guid FailOn { get; set; }

        public Task UpdateAsync(StockChangedEventArgs args, CancellationToken ct)
        {
            Applied.Add(args.Key);
            return args.Key == FailOn ? Task.FromException(new InvalidOperationException("Algolia is down")) : Task.CompletedTask;
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = new();

        public int Attempts { get; private set; }

        public bool Fail { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            if (Fail)
                throw new HttpRequestException("Algolia is down");

            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
