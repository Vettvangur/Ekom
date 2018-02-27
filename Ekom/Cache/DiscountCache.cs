using Ekom.Exceptions;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Abstractions;
using Ekom.Models.Discounts;
using Ekom.Services;
using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Umbraco.Core.Models;
using PerStoreCouponCache
    = System.Collections.Concurrent.ConcurrentDictionary<string,
        System.Collections.Concurrent.ConcurrentDictionary<string, Ekom.Interfaces.IDiscount>>;

namespace Ekom.Cache
{
    class DiscountCache : PerStoreCache<IDiscount>
    {
        protected BaseSearchProvider _searcher;

        /// <summary>
        /// Map coupon strings back to their parent <see cref="IDiscount"/>
        /// </summary>
        public PerStoreCouponCache CouponCache { get; }
            = new PerStoreCouponCache();

        public override string NodeAlias { get; } = "ekmDiscount";
        public virtual string CouponNodeAlias { get; } = "ekmCoupon";

        /// <summary>
        /// ctor
        /// </summary>
        public DiscountCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager,
            IBaseCache<Store> storeCache
        )
            : base(config, examineManager, storeCache)
        {
            _config = config;
            _examineManager = examineManager;
            _log = logFac.GetLogger<DiscountCache>();

            _searcher = _examineManager.SearchProviderCollection[_config.ExamineSearcher];
        }

        protected override IDiscount New(SearchResult r, Store s)
        {
            return new Discount(r, s);
        }

        /// <summary>
        /// Fill the given stores cache of TItem
        /// </summary>
        /// <param name="store">The current store being filled of TItem</param>
        /// <param name="results">Examine search results</param>
        /// <returns>Count of items added</returns>
        protected override int FillStoreCache(Store store, ISearchResults results)
        {
            int count = 0;

            var curStoreCache = Cache[store.Alias] = new ConcurrentDictionary<Guid, IDiscount>();
            var curStoreCouponCache = CouponCache[store.Alias] = new ConcurrentDictionary<string, IDiscount>();

            foreach (var r in results)
            {
                try
                {
                    // Traverse up parent nodes, checking disabled status and published status
                    if (!r.IsItemDisabled(store))
                    {
                        ISearchCriteria searchCriteria = 
                            _searcher.CreateSearchCriteria()
                            .RawQuery($@"+path:\{r.Fields["path"]},* +nodeTypeAlias:{CouponNodeAlias}");
                        
                        ISearchResults coupons = _searcher.Search(searchCriteria);

                        // Ensure coupons are distinct
                        if (!coupons.Any(coupon => curStoreCouponCache.ContainsKey(coupon.Fields["nodeName"])))
                        {
                            var item = _objFac?.Create(r, store) ?? new Discount(r, store);

                            if (item != null)
                            {
                                count++;

                                foreach (var coupon in coupons)
                                {
                                    // no empty strings
                                    if (!string.IsNullOrEmpty(coupon.Fields["nodeName"]))
                                    {
                                        curStoreCouponCache[coupon.Fields["nodeName"]] = item;
                                    }
                                }

                                if (item is Discount discountItem)
                                {
                                    discountItem.CouponsInternal = coupons.Select(c => c.Fields["nodeName"]).ToArray();
                                }

                                var itemKey = Guid.Parse(r.Fields["key"]);
                                curStoreCache[itemKey] = item;

                                _log.Info($"Added {coupons.Count()} coupons for discount {r.Fields["nodeName"]}");
                            }
                        }
                        else
                        {
                            throw new CouponDuplicateException("Duplicate coupon found for discount: " + r.Id);
                        }
                    }
                }
                catch (Exception ex) // Skip on fail
                {
                    _log.Error("Error on adding item with id: " + r.Id + " from Examine in Store: " + store.Alias, ex);
                }
            }

            return count;
        }

        /// <summary>
        /// Adds or replaces an item from all store caches
        /// </summary>
        public override void AddReplace(IContent node)
        {
            foreach (var store in _storeCache.Cache)
            {
                try
                {
                    if (!node.IsItemDisabled(store.Value))
                    {
                        var coupons = node.Children()
                            .Where(nodeChild => nodeChild.ContentType.Alias == CouponNodeAlias);

                        // Ensure coupons are distinct
                        if (!coupons.Any(coupon => CouponCache[store.Value.Alias].ContainsKey(coupon.Name)))
                        {
                            var item = _objFac?.Create(node, store.Value)
                                ?? new Discount(node, store.Value);

                            if (item != null)
                            {
                                foreach (var coupon in coupons)
                                {
                                    // no empty strings
                                    if (!string.IsNullOrEmpty(coupon.Name))
                                    {
                                        CouponCache[store.Value.Alias][coupon.Name] = item;
                                    }
                                }

                                if (item is Discount discountItem)
                                {
                                    discountItem.CouponsInternal = coupons.Select(c => c.Name).ToArray();
                                }

                                Cache[store.Value.Alias][node.Key] = item;
                            }
                        }
                        else
                        {
                            throw new CouponDuplicateException("Duplicate coupon found for discount: " + node.Id);
                        }
                    }
                }
                catch (Exception ex) // Skip on fail
                {
                    _log.Error("Error on Add/Replacing item with id: " + node.Id + " in store: " + store.Value.Alias, ex);
                }
            }
        }

        /// <summary>
        /// <see cref="ICache"/> implementation,
        /// handles removal of nodes when umbraco events fire
        /// </summary>
        public override void Remove(Guid id)
        {
            IDiscount i = null;

            foreach (var store in _storeCache.Cache)
            {
                Cache[store.Value.Alias].TryRemove(id, out i);

                if (i != null)
                {
                    foreach (var coupon in i.Coupons)
                    {
                        CouponCache[store.Value.Alias].TryRemove(coupon, out i);
                    }
                }
            }
        }
    }
}
