namespace Ekom.Models;

public class ProductQueryBase
{
    public int? Page { get; set; } 
    public int? PageSize { get; set; } 
    public string SearchQuery { get; set; } = string.Empty;
    public IEnumerable<int> Ids { get; set; } = new List<int>();
    public IEnumerable<Guid> Keys { get; set; } = new List<Guid>();
    public IEnumerable<string> Skus { get; set; } = new List<string>();
    public List<EkomSearchField> SearchFields { get; set; } = new();
    public string StoreAlias { get; set; } = string.Empty;
    public bool AllFiltersVisible { get; set; } = false;
}
