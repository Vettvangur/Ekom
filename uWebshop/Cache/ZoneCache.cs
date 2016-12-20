using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System.Diagnostics;
using uWebshop.Models;
using System;

namespace uWebshop.Cache
{
    public class ZoneCache : BaseCache<Zone>
    {
        public static ZoneCache Instance { get; } = new ZoneCache();

        protected override string nodeAlias { get; } = "uwbsZone";

        protected override Zone New(SearchResult r, Store store)
        {
            return new Zone(r);
        }
    }
}
