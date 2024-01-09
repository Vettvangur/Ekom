using Ekom.Utilities;
using Microsoft.AspNetCore.Http;

namespace Ekom.Models
{
    public class ProductQuery : ProductQueryBase
    {
        private const string FilterPrefix = "filter_";
        private const string PropertyPrefix = "property_";

        public ProductQuery()
        {
        }

        public ProductQuery(IQueryCollection query)
        {
            if (query == null)
            {
                return;
            }

            MetaFilters = MetaFilters ?? ExtractFilters(query, FilterPrefix);
            PropertyFilters = PropertyFilters ?? ExtractFilters(query, PropertyPrefix);

            SearchQuery = !string.IsNullOrEmpty(SearchQuery) ?
                SearchQuery :
                (query.TryGetValue("q", out var sq) ? sq.FirstOrDefault() : string.Empty);


            Page = Page ?? (int.TryParse(query["page"], out int page) ? page : 1);

            if (query.TryGetValue("orderby", out var orderByValue) &&
                Enum.TryParse(orderByValue, true, out OrderBy orderBy))
            {
                OrderBy = orderBy;
            }
        }

        private static Dictionary<string, List<string>> ExtractFilters(IQueryCollection query, string prefix)
        {
            return query
                .Where(x => x.Key.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase) &&
                            x.Value.All(v => !string.IsNullOrEmpty(v)))
                .ToDictionary(
                    x => x.Key.Replace(prefix, "", StringComparison.InvariantCultureIgnoreCase),
                    x => x.Value.ToList());
        }

        public Dictionary<string, List<string>> MetaFilters { get; set; }
        public Dictionary<string, List<string>> PropertyFilters { get; set; }
        public OrderBy OrderBy { get; set; } = OrderBy.TitleAsc;
    }
}
