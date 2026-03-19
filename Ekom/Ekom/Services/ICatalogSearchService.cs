using Ekom.Models;

namespace Ekom.Services;

public interface ICatalogSearchService
{
    IEnumerable<SearchResultEntity> PublicQuery(SearchRequest req, out long total);
    IEnumerable<SearchResultEntity> InternalQuery(SearchRequest req, out long total);
    IEnumerable<int> ProductQuery(SearchRequest req, out long total);
    Task<(IEnumerable<SearchResultEntity> Results, long Total)> PublicQueryAsync(
        SearchRequest req,
        CancellationToken ct = default);

    Task<(IEnumerable<SearchResultEntity> Results, long Total)> InternalQueryAsync(
        SearchRequest req,
        CancellationToken ct = default);

    Task<(IEnumerable<int> Ids, long Total)> ProductQueryAsync(
        SearchRequest req,
        CancellationToken ct = default);
}
