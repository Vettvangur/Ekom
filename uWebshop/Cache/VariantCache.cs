using System;
using Examine;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class VariantCache : PerStoreCache<Variant, VariantCache>
    {
        protected override string nodeAlias { get; } = "uwbsProductVariant";

        protected override Variant New(SearchResult r, Store store)
        {
            return new Variant(r, store);
        }
    }
}
