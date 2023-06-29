using Ekom.Models;

namespace Ekom.Services
{
    public interface ICatalogSearchService
    {
        IEnumerable<SearchResultEntity> Query(SearchRequest req, out long total);
        IEnumerable<int> ProductQuery(SearchRequest req, out long total);
    }
}
