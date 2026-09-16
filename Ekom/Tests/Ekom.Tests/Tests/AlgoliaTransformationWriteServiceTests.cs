using Algolia.Search.Clients;
using Algolia.Search.Exceptions;
using Algolia.Search.Utils;
using Ekom.Algolia;
using Ekom.Algolia.Indexing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaTransformationWriteServiceTests
{
    [Theory]
    [InlineData(1000, 250)]
    [InlineData(250, 250)]
    [InlineData(100, 100)]
    [InlineData(0, 250)]
    public void Caps_Transformation_Batch_Size(int configured, int expected)
    {
        var service = CreateService(Mock.Of<ISearchClient>());

        var result = service.GetEffectiveBatchSize(configured);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Retries_Transient_Failures()
    {
        var client = new Mock<ISearchClient>();
        var calls = 0;
        var records = new[] { new TestRecord() };
        client
            .Setup(x => x.SaveObjectsWithTransformationAsync(
                "products",
                It.IsAny<IEnumerable<object>>(),
                true,
                250,
                null,
                It.IsAny<CancellationToken>(),
                null))
            .Callback(() =>
            {
                calls++;
                if (calls < 3)
                    throw new AlgoliaUnreachableHostException("temporary failure");
            });
        var service = CreateService(client.Object, maxAttempts: 3);

        await service.SaveAsync("products", records, 1000, null, CancellationToken.None);

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Saves_Records_In_Capped_Batches()
    {
        var client = new Mock<ISearchClient>();
        var records = Enumerable.Range(0, 501).Select(x => new TestRecord { ObjectID = x.ToString() }).ToArray();
        var service = CreateService(client.Object);

        await service.SaveAsync("products", records, 1000, null, CancellationToken.None);

        client.Verify(
            x => x.SaveObjectsWithTransformationAsync(
                "products",
                It.Is<IEnumerable<object>>(objects => objects.Count() == 250),
                true,
                250,
                null,
                CancellationToken.None,
                null),
            Times.Exactly(2));
        client.Verify(
            x => x.SaveObjectsWithTransformationAsync(
                "products",
                It.Is<IEnumerable<object>>(objects => objects.Count() == 1),
                true,
                250,
                null,
                CancellationToken.None,
                null),
            Times.Once);
    }

    [Fact]
    public async Task Retries_Only_The_Failed_Batch()
    {
        var client = new Mock<ISearchClient>();
        var finalBatchCalls = 0;
        var records = Enumerable.Range(0, 251).Select(x => new TestRecord { ObjectID = x.ToString() }).ToArray();
        client
            .Setup(x => x.SaveObjectsWithTransformationAsync(
                "products",
                It.Is<IEnumerable<object>>(objects => objects.Count() == 1),
                true,
                250,
                null,
                CancellationToken.None,
                null))
            .Callback(() =>
            {
                finalBatchCalls++;
                if (finalBatchCalls == 1)
                    throw new AlgoliaUnreachableHostException("temporary failure");
            });
        var service = CreateService(client.Object, maxAttempts: 2);

        await service.SaveAsync("products", records, 1000, null, CancellationToken.None);

        client.Verify(
            x => x.SaveObjectsWithTransformationAsync(
                "products",
                It.Is<IEnumerable<object>>(objects => objects.Count() == 250),
                true,
                250,
                null,
                CancellationToken.None,
                null),
            Times.Once);
        Assert.Equal(2, finalBatchCalls);
    }

    [Fact]
    public async Task Retries_Transient_Replacement_Failures()
    {
        var client = new Mock<ISearchClient>();
        var calls = 0;
        var records = new[] { new TestRecord() };
        var chunkedOptions = new ChunkedHelperOptions { MaxRetries = 10 };
        client
            .Setup(x => x.ReplaceAllObjectsWithTransformationAsync(
                "products",
                It.IsAny<IEnumerable<object>>(),
                250,
                null,
                null,
                It.IsAny<CancellationToken>(),
                chunkedOptions))
            .Callback(() =>
            {
                calls++;
                if (calls == 1)
                    throw new AlgoliaUnreachableHostException("temporary failure");
            });
        var service = CreateService(client.Object, maxAttempts: 2);

        await service.ReplaceAllAsync("products", records, 1000, chunkedOptions, CancellationToken.None);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Stops_After_Configured_Transient_Attempts()
    {
        var client = new Mock<ISearchClient>();
        var records = new[] { new TestRecord() };
        client
            .Setup(x => x.SaveObjectsWithTransformationAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                true,
                It.IsAny<int>(),
                null,
                It.IsAny<CancellationToken>(),
                null))
            .Throws(new AlgoliaUnreachableHostException("temporary failure"));
        var service = CreateService(client.Object, maxAttempts: 2);

        await Assert.ThrowsAsync<AlgoliaUnreachableHostException>(
            () => service.SaveAsync("products", records, 1000, null, CancellationToken.None));

        client.Verify(
            x => x.SaveObjectsWithTransformationAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                true,
                It.IsAny<int>(),
                null,
                It.IsAny<CancellationToken>(),
                null),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Does_Not_Retry_Non_Transient_Failures()
    {
        var client = new Mock<ISearchClient>();
        var records = new[] { new TestRecord() };
        client
            .Setup(x => x.SaveObjectsWithTransformationAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                true,
                It.IsAny<int>(),
                null,
                It.IsAny<CancellationToken>(),
                null))
            .Throws(new InvalidOperationException("invalid operation"));
        var service = CreateService(client.Object, maxAttempts: 3);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveAsync("products", records, 1000, null, CancellationToken.None));

        client.Verify(
            x => x.SaveObjectsWithTransformationAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                true,
                It.IsAny<int>(),
                null,
                It.IsAny<CancellationToken>(),
                null),
            Times.Once);
    }

    [Fact]
    public async Task Does_Not_Retry_Cancellation()
    {
        var client = new Mock<ISearchClient>();
        var records = new[] { new TestRecord() };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        client
            .Setup(x => x.SaveObjectsWithTransformationAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                true,
                It.IsAny<int>(),
                null,
                cts.Token,
                null))
            .Throws(new OperationCanceledException(cts.Token));
        var service = CreateService(client.Object, maxAttempts: 3);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SaveAsync("products", records, 1000, null, cts.Token));

        client.Verify(
            x => x.SaveObjectsWithTransformationAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                true,
                It.IsAny<int>(),
                null,
                cts.Token,
                null),
            Times.Once);
    }

    [Fact]
    public async Task Cancellation_Stops_Retry_Backoff()
    {
        var client = new Mock<ISearchClient>();
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var records = new[] { new TestRecord() };
        using var cts = new CancellationTokenSource();
        client
            .Setup(x => x.SaveObjectsWithTransformationAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                true,
                It.IsAny<int>(),
                null,
                cts.Token,
                null))
            .Callback(() =>
            {
                attempted.TrySetResult();
                throw new AlgoliaUnreachableHostException("temporary failure");
            });
        var service = CreateService(client.Object, maxAttempts: 3, retryDelayMilliseconds: 10_000);

        var save = service.SaveAsync("products", records, 1000, null, cts.Token);
        await attempted.Task;
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => save);
        client.Verify(
            x => x.SaveObjectsWithTransformationAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<object>>(),
                true,
                It.IsAny<int>(),
                null,
                cts.Token,
                null),
            Times.Once);
    }

    private static AlgoliaTransformationWriteService CreateService(
        ISearchClient client,
        int maxAttempts = 3,
        int retryDelayMilliseconds = 0)
        => new(
            client,
            Options.Create(new AlgoliaOptions
            {
                ApplicationId = "app-id",
                AdminApiKey = "admin-key",
                SearchApiKey = "search-key",
                Transformation = new AlgoliaTransformationWriteOptions
                {
                    MaxBatchSize = 250,
                    MaxAttempts = maxAttempts,
                    RetryBaseDelayMilliseconds = retryDelayMilliseconds,
                },
            }),
            NullLogger<AlgoliaTransformationWriteService>.Instance);

    private sealed class TestRecord
    {
        public string ObjectID { get; init; } = "record-id";
    }
}
