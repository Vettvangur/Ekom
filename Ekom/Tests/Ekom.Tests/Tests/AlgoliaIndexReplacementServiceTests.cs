using Algolia.Search.Clients;
using Algolia.Search.Utils;
using Ekom.Algolia;
using Ekom.Algolia.Indexing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaIndexReplacementServiceTests
{
    [Fact]
    public async Task Saves_Records_Directly_When_Index_Does_Not_Exist()
    {
        var client = new Mock<ISearchClient>();
        var records = new[] { new TestRecord() };
        using var cts = new CancellationTokenSource();
        client
            .Setup(x => x.IndexExistsAsync("products", cts.Token))
            .ReturnsAsync(false);
        var service = CreateService(client.Object, maxRetries: 800);

        await service.ReplaceAllAsync("products", records, 1000, cts.Token);

        client.Verify(
            x => x.SaveObjectsAsync(
                "products",
                records,
                true,
                1000,
                null,
                cts.Token,
                It.Is<ChunkedHelperOptions>(options => options.MaxRetries == 800)),
            Times.Once);
        client.Verify(
            x => x.ReplaceAllObjectsAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<TestRecord>>(),
                It.IsAny<int>(),
                null,
                null,
                It.IsAny<CancellationToken>(),
                It.IsAny<ChunkedHelperOptions>()),
            Times.Never);
    }

    [Fact]
    public async Task Replaces_Existing_Index_With_Configured_Max_Retries()
    {
        var client = new Mock<ISearchClient>();
        var records = new[] { new TestRecord() };
        using var cts = new CancellationTokenSource();
        client
            .Setup(x => x.IndexExistsAsync("products", cts.Token))
            .ReturnsAsync(true);
        var service = CreateService(client.Object, maxRetries: 321);

        await service.ReplaceAllAsync("products", records, 500, cts.Token);

        client.Verify(
            x => x.ReplaceAllObjectsAsync(
                "products",
                records,
                500,
                null,
                null,
                cts.Token,
                It.Is<ChunkedHelperOptions>(options => options.MaxRetries == 321)),
            Times.Once);
        client.Verify(
            x => x.SaveObjectsAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<TestRecord>>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                null,
                It.IsAny<CancellationToken>(),
                It.IsAny<ChunkedHelperOptions>()),
            Times.Never);
    }

    private static AlgoliaIndexReplacementService CreateService(ISearchClient client, int maxRetries)
        => new(
            client,
            Options.Create(new AlgoliaOptions
            {
                ApplicationId = "app-id",
                AdminApiKey = "admin-key",
                SearchApiKey = "search-key",
                Replacement = new AlgoliaIndexReplacementOptions
                {
                    MaxRetries = maxRetries,
                },
            }),
            NullLogger<AlgoliaIndexReplacementService>.Instance);

    private sealed class TestRecord
    {
        public string ObjectID { get; init; } = "record-id";
    }
}
