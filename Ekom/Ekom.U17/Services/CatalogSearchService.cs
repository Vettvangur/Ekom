using Ekom.Models;
using Ekom.Services;

namespace Ekom.Umb.Services;

internal sealed class CatalogSearchService : ICatalogSearchService
{
    public Task<(IEnumerable<SearchResultEntity> Results, long Total)> PublicQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        return Task.FromResult<(IEnumerable<SearchResultEntity>, long)>((Array.Empty<SearchResultEntity>(), 0));
    }

    public Task<(IEnumerable<SearchResultEntity> Results, long Total)> InternalQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        return Task.FromResult<(IEnumerable<SearchResultEntity>, long)>((Array.Empty<SearchResultEntity>(), 0));
    }

    public Task<(IEnumerable<int> Ids, long Total)> ProductQueryAsync(
        SearchRequest req,
        CancellationToken ct = default)
    {
        return Task.FromResult<(IEnumerable<int>, long)>((Array.Empty<int>(), 0));
    }
}
