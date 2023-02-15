using Ekom.Utilities;
using Microsoft.AspNetCore.Http;

namespace Ekom.Models
{
    public class ProductQuery : ProductQueryBase
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

            MetaFilters = MetaFilters == null || !MetaFilters.Any() ? query.Where(x => x.Key.StartsWith("filter_", StringComparison.InvariantCultureIgnoreCase)).ToDictionary(x => x.Key.Replace("filter_", "", StringComparison.InvariantCultureIgnoreCase), x => x.Value.ToList()) : MetaFilters;
            PropertyFilters = PropertyFilters == null || !PropertyFilters.Any() ?  query.Where(x => x.Key.StartsWith("property_", StringComparison.InvariantCultureIgnoreCase)).ToDictionary(x => x.Key.Replace("property_", "",  StringComparison.InvariantCultureIgnoreCase), x => x.Value.ToList()) : PropertyFilters;

            SearchQuery = string.IsNullOrEmpty(SearchQuery) ? query.ContainsKey("q") ? query["q"] : "" : SearchQuery;

            int page = int.TryParse(query["page"], out page) ? page : 1;

            Page = Page.HasValue ? Page : page;
        }
        
        public Dictionary<string, List<string>> MetaFilters { get; set; }
        public Dictionary<string, List<string>> PropertyFilters { get; set; }
        public OrderBy OrderBy { get; set; }



    }
}
