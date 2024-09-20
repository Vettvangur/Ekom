using Ekom.Utilities;
using Microsoft.AspNetCore.Http;

namespace Ekom.Models;

public class ProductQuery : ProductQueryBase
{
    private const string FilterPrefix = "filter_";
    private const string PropertyPrefix = "property_";
    private IQueryCollection _query;
    public ProductQuery()
    {
    }

    public ProductQuery(IQueryCollection query)
    {
        if (query == null)
        {
            return;
        }

        _query = query;

        MetaFilters = MetaFilters ?? ExtractFilters(query, FilterPrefix);
        PropertyFilters = PropertyFilters ?? ExtractFilters(query, PropertyPrefix);

        SearchQuery = !string.IsNullOrEmpty(SearchQuery) ?
            SearchQuery :
            (query.TryGetValue("q", out var sq) ? sq.FirstOrDefault() : string.Empty);


        
        Page = Page ??
            (int.TryParse(query["page"], out int page) ? page :
            (int.TryParse(query["p"], out page) ? page : 1));
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
    public Dictionary<string, string> PropertySelectors { get; set; }
    private OrderBy _orderBy = OrderBy.DateDesc;
    public OrderBy OrderBy
    {
        get => _orderBy;
        set
        {
            // Check the query string for 'orderby', otherwise use the passed value
            if (_query != null && _query.TryGetValue("orderby", out var orderByValue) &&
                !string.IsNullOrEmpty(orderByValue) &&
                Enum.TryParse(orderByValue, true, out OrderBy parsedOrderBy))
            {
                _orderBy = parsedOrderBy; // Use parsed value from query
            }
            else
            {
                _orderBy = value; // Use the explicitly passed value
            }
        }
    }
    public bool FilterOutZeroPriceProducts { get; set; } = false;
}
