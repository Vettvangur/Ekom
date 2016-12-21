using System;
using Examine;
using uWebshop.Models;

namespace uWebshop.Cache
{
    public class VariantGroupCache : PerStoreCache<VariantGroup, VariantGroupCache>
    {
        protected override string nodeAlias { get; } = "uwbsProductVariantGroup";

        protected override VariantGroup New(SearchResult r, Store store)
        {
            return new VariantGroup(r, store);
        }
    }
}
