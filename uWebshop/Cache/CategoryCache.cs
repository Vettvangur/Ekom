using System;
using Examine;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class CategoryCache : BaseCache<Category>
    {
        public static CategoryCache Instance { get; } = new CategoryCache();

        protected override string nodeAlias { get; } = "uwbsCategory";

        protected override Category New(SearchResult r, Store store)
        {
            return new Category(r, store);
        }
    }
}
