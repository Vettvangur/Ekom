using System;
using Examine;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class CategoryCache : PerStoreCache<Category, CategoryCache>
    {
        protected override string nodeAlias { get; } = "uwbsCategory";

        protected override Category New(SearchResult r, Store store)
        {
            return new Category(r, store);
        }
    }
}
