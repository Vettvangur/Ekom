namespace Ekom.Models
{
    public class ProductQueryBase
    {
        public int? Page { get; set; }
        public int? PageSize { get; set; }
        public string SearchQuery { get; set; }
        public IEnumerable<int> Ids { get; set; }
        public IEnumerable<Guid> Keys { get; set; }
        public List<EkomSearchField> SearchFields { get; set; } = null; 
        public string Culture { get; set; } = null;
        public string StoreAlias { get; set; } = null;
    }
}
