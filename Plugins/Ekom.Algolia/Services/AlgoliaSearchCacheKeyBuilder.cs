using Algolia.Search.Models.Search;
using Ekom.Algolia.Models.Search;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ekom.Algolia.Services;

internal sealed class AlgoliaSearchCacheKeyBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AlgoliaSearchCacheVersionProvider _cacheVersions;

    public AlgoliaSearchCacheKeyBuilder(AlgoliaSearchCacheVersionProvider cacheVersions)
    {
        _cacheVersions = cacheVersions;
    }

    public string BuildProductsKey(AlgoliaSearchRequest request, SearchForHits query, string indexName)
        => BuildKey("products", request, query, indexName);

    public string BuildCategoriesKey(AlgoliaSearchRequest request, SearchForHits query, string indexName)
        => BuildKey("categories", request, query, indexName);

    public string BuildQuerySuggestionsKey(AlgoliaSearchRequest request, SearchForHits query, string indexName)
        => BuildKey("query-suggestions", request, query, indexName);

    public string BuildContentKey(AlgoliaContentSearchRequest request, SearchForHits query, string indexName)
    {
        var payload = JsonSerializer.Serialize(query, JsonOptions);
        var version = _cacheVersions.GetVersion("content");

        var raw = string.Join(
            '|',
            "content",
            version.ToString(),
            request.IndexName.Trim().ToLowerInvariant(),
            indexName.Trim().ToLowerInvariant(),
            request.Culture.Trim().ToLowerInvariant(),
            ComputeHash(payload));

        return "algolia-search:" + raw;
    }

    private string BuildKey(string entity, AlgoliaSearchRequest request, SearchForHits query, string indexName)
    {
        var payload = JsonSerializer.Serialize(query, JsonOptions);
        var version = _cacheVersions.GetVersion(request.StoreAlias);

        var raw = string.Join(
            '|',
            entity,
            version.ToString(),
            request.StoreAlias.Trim().ToLowerInvariant(),
            indexName.Trim().ToLowerInvariant(),
            request.Locale?.Trim().ToLowerInvariant() ?? string.Empty,
            request.Currency?.Trim().ToLowerInvariant() ?? string.Empty,
            ComputeHash(payload));

        return "algolia-search:" + raw;
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
