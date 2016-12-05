using uWebshop.Adapters;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class ProductCache : BaseCache<Product>
    {
        public static ProductCache Instance { get; } = new ProductCache();

        public override string nodeAlias { get; set; } = "uwbsProduct";

        public void FillCache()
        {
            FillCache(ProductAdapter.CreateProductItemFromExamine);
        }
    }
}
