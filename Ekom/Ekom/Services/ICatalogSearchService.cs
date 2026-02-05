using Ekom.Models;

namespace Ekom.Services;

public interface ICatalogSearchService
{
    IEnumerable<SearchResultEntity> PublicQuery(SearchRequest req, out long total);
    IEnumerable<SearchResultEntity> InternalQuery(SearchRequest req, out long total);
    IEnumerable<int> ProductQuery(SearchRequest req, out long total);
    ValueTask<(IEnumerable<SearchResultEntity> Results, long Total)> PublicQueryAsync(
        SearchRequest req,
        CancellationToken ct = default);

    ValueTask<(IEnumerable<SearchResultEntity> Results, long Total)> InternalQueryAsync(
        SearchRequest req,
        CancellationToken ct = default);

    ValueTask<(IEnumerable<int> Ids, long Total)> ProductQueryAsync(
        SearchRequest req,
        CancellationToken ct = default);
}
