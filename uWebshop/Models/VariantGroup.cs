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
        public int Id { get; set; }
        public Guid Key { get; set; }
        public Guid ProductKey { get; set; }
        public string Title { get; set; }
        public Store Store { get; set; }
        public int SortOrder { get; set; }
        public IEnumerable<Variant> Variants {
            get
            {
                return VariantCache.Cache[Store.Alias]
                                   .Where(x => x.Value.VariantGroupKey == Key)
                                   .Select(x => x.Value);
            }
        }

        public VariantGroup(): base() { }
        public VariantGroup(SearchResult item, Store store)
        {
            var key = item.Fields["key"];

            var _key = new Guid();

            if (!Guid.TryParse(key, out _key))
            {
                throw new Exception("No key present for variant group.");
            }

            int productId = Convert.ToInt32(item.Fields["parentID"]);

            var productExamine = ExamineHelper.GetExamindeNode(productId);

            if (productExamine == null)
            {
                throw new Exception("Product not found in examine for Variant group. Id:" + productId);
            }

            var productKey = productExamine.Fields["key"];

            var _productKey = new Guid();

            if (!Guid.TryParse(productKey, out _productKey))
            {
                throw new Exception("No key present for product in variant group.");
            }

            Id        = item.Id;
            ProductKey = _productKey;
            Key         = _key;
            Title     = item.GetStoreProperty("title", store.Alias);
            Store     = store;
            SortOrder = Convert.ToInt32(item.Fields["sortOrder"]);
        }
        public VariantGroup(IContent item, Store store)
        {
            var productId = item.ParentId;

            var product = API.Catalog.GetProduct(store.Alias,productId);

            Id        = item.Id;
            Key = item.Key;
            ProductKey = product.Key;
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
