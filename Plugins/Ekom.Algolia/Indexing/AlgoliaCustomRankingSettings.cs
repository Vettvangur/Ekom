using Algolia.Search.Clients;
using Algolia.Search.Models.Search;

namespace Ekom.Algolia.Indexing;

internal static class AlgoliaCustomRankingSettings
{
    public static List<string>? Normalize(IReadOnlyCollection<string>? configuredRanking)
    {
        if (configuredRanking is null || configuredRanking.Count == 0)
            return null;

        var customRanking = configuredRanking
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return customRanking.Count == 0 ? null : customRanking;
    }

    public static Task ApplyAsync(
        ISearchClient client,
        string indexName,
        IReadOnlyCollection<string>? configuredRanking,
        CancellationToken ct)
    {
        var customRanking = Normalize(configuredRanking);
        if (customRanking is null)
            return Task.CompletedTask;

        return client.SetSettingsAsync(
            indexName,
            new IndexSettings { CustomRanking = customRanking },
            forwardToReplicas: false,
            options: null,
            cancellationToken: ct);
    }
}
