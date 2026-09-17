using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Ekom.Algolia.Indexing;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaCustomRankingSettingsTests
{
    [Fact]
    public void Preserves_Null_Or_Empty_Custom_Ranking_As_Unmanaged()
    {
        var nullResult = AlgoliaCustomRankingSettings.Normalize(null);
        var emptyResult = AlgoliaCustomRankingSettings.Normalize([]);

        Assert.Null(nullResult);
        Assert.Null(emptyResult);
    }

    [Fact]
    public void Normalizes_Custom_Ranking_Without_Changing_Order()
    {
        var result = AlgoliaCustomRankingSettings.Normalize(
            [" desc(ProductRanking) ", "", "asc(Price)", "desc(ProductRanking)", "  "]);

        Assert.Equal(["desc(ProductRanking)", "asc(Price)"], result);
    }

    [Fact]
    public async Task Does_Not_Apply_Unconfigured_Custom_Ranking()
    {
        var client = new Mock<ISearchClient>();

        await AlgoliaCustomRankingSettings.ApplyAsync(
            client.Object,
            "products",
            configuredRanking: null,
            CancellationToken.None);
        await AlgoliaCustomRankingSettings.ApplyAsync(
            client.Object,
            "products",
            [],
            CancellationToken.None);

        Assert.Empty(client.Invocations);
    }

    [Fact]
    public async Task Applies_Configured_Custom_Ranking()
    {
        var client = new Mock<ISearchClient>();
        client
            .Setup(x => x.SetSettingsAsync(
                "products",
                It.IsAny<IndexSettings>(),
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdatedAtResponse)null!);

        await AlgoliaCustomRankingSettings.ApplyAsync(
            client.Object,
            "products",
            [" desc(ProductRanking) ", "desc(ProductRanking)", "asc(Price)"],
            CancellationToken.None);

        client.Verify(
            x => x.SetSettingsAsync(
                "products",
                It.Is<IndexSettings>(settings => settings.CustomRanking != null
                    && settings.CustomRanking.SequenceEqual(new[] { "desc(ProductRanking)", "asc(Price)" })),
                false,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
