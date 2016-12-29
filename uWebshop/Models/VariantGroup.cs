using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Services;

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
                return VariantCache.Cache[Store.Alias]
                                   .Where(x => x.Value.VariantGroupId == Id)
                                   .Select(x => x.Value);
            }
        }

        public VariantGroup(): base() { }
        public VariantGroup(SearchResult item, Store store)
        {
            int productId = Convert.ToInt32(item.Fields["parentID"]);

            Id        = item.Id;
            Title     = item.GetStoreProperty("title", store.Alias);
            Store     = store;
            SortOrder = Convert.ToInt32(item.Fields["sortOrder"]);
        }
        public VariantGroup(IContent item, Store store)
        {
            int productId = item.ParentId;

            Id        = item.Id;
            Title     = item.GetStoreProperty("title", store.Alias);
            Store     = store;
            SortOrder = item.SortOrder;
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
