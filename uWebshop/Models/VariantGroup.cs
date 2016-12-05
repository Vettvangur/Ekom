using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Cache;

namespace uWebshop.Models
{
    public class VariantGroup
    {
        public int ProductId;

        public VariantGroup(int id)
        {
            this.ProductId = id;
        }
        public int Id { get; set; }
        public string Title { get; set; }
        public Store Store { get; set; }
        public int SortOrder { get; set; }
        public IEnumerable<Variant> Variants {
            get
            {
                return VariantCache.Instance._cache.Where(x => x.Value.VariantGroupId == Id && x.Value.Store.Alias == Store.Alias).Select(x => x.Value);
            }
        }
    }
}
