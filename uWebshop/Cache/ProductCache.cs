using System;
using Examine;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class ProductCache : BaseCache<Product>
    {
        public static ProductCache Instance { get; } = new ProductCache();

        protected override string nodeAlias { get; } = "uwbsProduct";

        protected override Product New(SearchResult r, Store store)
        {
            return new Product(r, store);
        }
    }
}
