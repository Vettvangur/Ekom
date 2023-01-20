using Ekom.Models;
using System.Collections.Generic;

namespace Ekom.Services
{
    interface ICatalogSearchService
    {
        IEnumerable<SearchResultEntity> QueryCatalog(string query, out long totalRecords, int take = 30);
    }
}
