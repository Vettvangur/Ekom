using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
            try
            {
                int variantGroupId = Convert.ToInt32(item.Fields["parentID"]);

                var pathField = item.Fields["path"];
                var paths = pathField.Split(',');

                int productId = Convert.ToInt32(paths.Reverse().Skip(2).Take(1).FirstOrDefault());

                var priceField = ExamineService.GetProperty(item, "price", store.Alias);
                decimal originalPrice = 0;

                decimal.TryParse(priceField, out originalPrice);

                Id             = item.Id;
                Title          = ExamineService.GetProperty(item, "title", store.Alias);
                Path           = pathField;
                OriginalPrice  = originalPrice;
                VariantGroupId = variantGroupId;
                ProductId      = productId;
                Store          = store;
                SortOrder      = Convert.ToInt32(item.Fields["sortOrder"]);
            }
            catch (Exception ex)
            {
                Log.Error("Error on creating variant item from Examine. Node id: " + item.Id, ex);
                throw;
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
