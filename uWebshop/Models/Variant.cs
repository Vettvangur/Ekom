using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using uWebshop.Helpers;
using uWebshop.Services;

namespace uWebshop.Models
{
    public class Variant
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public decimal OriginalPrice { get; set; }
        public int Stock { get; set; }
        public int ProductId { get; set; }
        public int VariantGroupId { get; set; }
        public Store Store { get; set; }
        public int SortOrder { get; set; }
        public Price Price
        {
            get
            {
                return new Price(OriginalPrice);
            }
            set
            { }
        }

        public Variant(): base() { }
        public Variant(SearchResult item, Store store)
        {
            int variantGroupId = Convert.ToInt32(item.Fields["parentID"]);

            var pathField = item.Fields["path"];
            var paths = pathField.Split(',');

            int productId = Convert.ToInt32(paths[paths.Length - 3]);

            var priceField = item.GetStoreProperty("price", store.Alias);
            decimal originalPrice = 0;

            decimal.TryParse(priceField, out originalPrice);

            Id             = item.Id;
            Title          = item.GetStoreProperty("title", store.Alias);
            Path           = pathField;
            OriginalPrice  = originalPrice;
            VariantGroupId = variantGroupId;
            ProductId      = productId;
            Store          = store;
            SortOrder      = Convert.ToInt32(item.Fields["sortOrder"]);
        }
        public Variant(IContent item, Store store)
        {
            int variantGroupId = item.GetValue<int>("parentID");

            var pathField = item.Path;
            var paths = pathField.Split(',');

            int productId = Convert.ToInt32(paths[paths.Length - 3]);

            var priceField = item.GetStoreProperty("price", store.Alias);
            decimal originalPrice = 0;

            decimal.TryParse(priceField, out originalPrice);

            Id = item.Id;
            Title = item.GetStoreProperty("title", store.Alias);
            Path = pathField;
            OriginalPrice = originalPrice;
            VariantGroupId = variantGroupId;
            ProductId = productId;
            Store = store;
            SortOrder = item.SortOrder;
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
