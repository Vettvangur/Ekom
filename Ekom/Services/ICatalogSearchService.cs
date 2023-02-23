using Ekom.Models;

namespace Ekom.Services
{
    interface ICatalogSearchService
    {
        IEnumerable<SearchResultEntity> Query(SearchRequest req, out long total);
    }
}
