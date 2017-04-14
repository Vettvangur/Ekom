using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System.Diagnostics;
using uWebshop.Models;
using System;

namespace uWebshop.Cache
{
    public class ZoneCache : BaseCache<Zone, ZoneCache>
    {
        protected override string nodeAlias { get; } = "uwbsZone";

        public ZoneCache(ICacheExtensions ext) : base(ext) { }

        private Zone New(SearchResult r, Store store)
        {
            return new Zone(r);
        }
    }
}
