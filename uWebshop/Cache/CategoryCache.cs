using uWebshop.Adapters;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class CategoryCache : BaseCache<Category>
    {
        public static CategoryCache Instance { get; } = new CategoryCache();

        public override string nodeAlias { get; set; } = "uwbsCategory";

        public void FillCache()
        {
            FillCache(CategoryAdapter.CreateCategoryItemFromExamine);
        }
    }
}
