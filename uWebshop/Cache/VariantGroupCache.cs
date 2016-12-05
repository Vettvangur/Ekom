using uWebshop.Adapters;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class VariantGroupCache : BaseCache<VariantGroup>
    {
        public static VariantGroupCache Instance { get; } = new VariantGroupCache();

        public override string nodeAlias { get; set; } = "uwbsProductVariantGroup";

        public void FillCache()
        {
            FillCache(VariantAdapter.CreateVariantGroupItemFromExamine);
        }
    }
}
