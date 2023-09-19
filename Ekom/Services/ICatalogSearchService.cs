using Ekom.Models;

namespace Ekom.Services
{
    public interface ICatalogSearchService
    {
        IEnumerable<SearchResultEntity> PublicQuery(SearchRequest req, out long total);
        IEnumerable<SearchResultEntity> InternalQuery(SearchRequest req, out long total);
        IEnumerable<int> ProductQuery(SearchRequest req, out long total);
    }
}
