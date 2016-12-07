using System;
using Examine;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class VariantCache : BaseCache<Variant>
    {
        public static VariantCache Instance { get; } = new VariantCache();

        protected override string nodeAlias { get; } = "uwbsProductVariant";

        protected override Variant New(SearchResult r, Store store)
        {
            return new Variant(r, store);
        }
    }
}
