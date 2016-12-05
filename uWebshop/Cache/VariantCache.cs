using uWebshop.Adapters;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class VariantCache : BaseCache<Variant>
    {
        public static VariantCache Instance { get; } = new VariantCache();

        public override string nodeAlias { get; set; } = "uwbsProductVariant";

        public void FillCache()
        {
            FillCache(VariantAdapter.CreateVariantItemFromExamine);
        }
    }
}
