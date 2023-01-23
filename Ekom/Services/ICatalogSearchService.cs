using Ekom.Models;

namespace Ekom.Services
{
    interface ICatalogSearchService
    {
        IEnumerable<SearchResultEntity> QueryCatalog(string query, out long totalRecords, int take = 30);
    }
}
