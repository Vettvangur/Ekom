using Ekom.Utilities;

namespace Ekom.Models
{
    public class SearchRequest : ProductQueryBase
    {
        public string SearchNodeById { get; set; } = "";
        public string[] NodeTypeAlias { get; set; } = null;
        public Dictionary<string, List<string>> MetaFilters { get; set; }
        public Dictionary<string, List<string>> PropertyFilters { get; set; }
        public OrderBy OrderBy { get; set; } = OrderBy.NoOrder;
    }

    public class EkomSearchField
    {
        public string Name { get; set; }
        public EkomSearchType SearchType { get; set; } = EkomSearchType.FuzzyAndWilcard;
        public string FuzzyConfiguration { get; set; } = "0.5";
        public string Booster { get; set; }
    }
    public enum EkomSearchType
    {
        Exact,
        Wildcard,
        Fuzzy,
        FuzzyAndWilcard
    }
}
