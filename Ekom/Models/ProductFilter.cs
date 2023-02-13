using Ekom.Utilities;
using Microsoft.AspNetCore.Http;

namespace Ekom.Models
{
    public class ProductQuery
    {
        public ProductQuery()
        {
            
        }
        public ProductQuery(IQueryCollection query)
        {
            if (query == null)
            {
                return;
            }

            var metaFilters = query.Where(x => x.Key.StartsWith("filter_", StringComparison.InvariantCultureIgnoreCase)).ToDictionary(x => x.Key.Replace("filter_", "", StringComparison.InvariantCultureIgnoreCase), x => x.Value.ToList());
            var propertyFilters = query.Where(x => x.Key.StartsWith("property_", StringComparison.InvariantCultureIgnoreCase)).ToDictionary(x => x.Key.Replace("property_", "",  StringComparison.InvariantCultureIgnoreCase), x => x.Value.ToList());

            MetaFilters = metaFilters;
            PropertyFilters = propertyFilters;
        }
        
        public Dictionary<string, List<string>> MetaFilters { get; set; }
        public Dictionary<string, List<string>> PropertyFilters { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
        public string SearchQuery { get; set; }
        public OrderBy OrderBy { get; set; }
        public IEnumerable<int> Ids { get; set; }
        public IEnumerable<Guid> Keys { get; set; }

    }
}
