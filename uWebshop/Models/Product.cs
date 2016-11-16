using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core.Logging;
using uWebshop.Cache;

namespace uWebshop.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Path { get; set; }
        public decimal OriginalPrice { get; set; }
        public int Stock { get; set; }
        public List<Category> Categories { get; set; }
        public Store Store { get; set; }
        public int SortOrder { get; set; }
        public int Level { get; set; }
        public string Url {
            get {

                var r = (ContentRequest)HttpContext.Current.Cache["uwbsRequest"];

                var defaulUrl = Urls.FirstOrDefault();
                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(r.DomainPrefix));

                if (findUrlByPrefix != null) {
                    return findUrlByPrefix;
                }

                return defaulUrl;
            }
        }
        public IEnumerable<String> Urls { get; set; }
        public Price Price
        {
            get
            {
                return new Price(OriginalPrice);
            }
        }
        public IEnumerable<VariantGroup> VariantGroups {
            get
            {
                return VariantCache._variantGroupCache.Where(x => x.Value.ProductId == Id && x.Value.Store.Alias == Store.Alias).Select(x => x.Value);
            }
        }
        public IEnumerable<Variant> AllVariants {
            get
            {
                return VariantCache._variantCache.Where(x => x.Value.ProductId == Id && x.Value.Store.Alias == Store.Alias).Select(x => x.Value);
            }
        }
    }
}
