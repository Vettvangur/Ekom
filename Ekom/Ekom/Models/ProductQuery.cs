using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.ComponentModel;

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
            (query.TryGetValue("q", out Microsoft.Extensions.Primitives.StringValues sq) ? sq.FirstOrDefault() : string.Empty);



        Page = Page ??
            (int.TryParse(query["page"], out int page) ? page :
            (int.TryParse(query["p"], out page) ? page : 1));
    }

    private static Dictionary<string, List<string>> ExtractFilters(IQueryCollection query, string prefix)
    {
        return query
            .Where(x => x.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                        x.Value.All(v => !string.IsNullOrEmpty(v)))
            .ToDictionary(
                x => x.Key.Replace(prefix, "", StringComparison.OrdinalIgnoreCase),
                x => x.Value.ToList());
    }

    public Dictionary<string, List<string>>? MetaFilters { get; set; } = [];
    public Dictionary<string, List<string>>? PropertyFilters { get; set; } = [];
    public string PropertySelectorsSeparator { get; set; } = string.Empty;
    public Dictionary<string, string>? PropertySelectors { get; set; } = [];

    [JsonConverter(typeof(OrderByJsonConverter))]
    [TypeConverter(typeof(OrderByTypeConverter))]
    public OrderBy? OrderBy { get; set; } = Configuration.Instance.DefaultProductOrderBy;
    public bool FilterOutZeroPriceProducts { get; set; } = false;
    public Func<IProduct, bool>? Filter { get; set; }
    public bool RaiseEvents { get; set; } = true;
}
