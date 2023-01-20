using System.Collections.Generic;
using System.Web;

namespace Ekom.Models
{
    public class ProductQuery
    {
        public ProductQuery()
        {
            
        }
        public Dictionary<string, List<string>> MetaFilters { get; set; }
        public Dictionary<string, List<string>> PropertyFilters { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
        public string SearchQuery { get; set; }
    }
}
