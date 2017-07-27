using System;
using Examine;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    public class CategoryCache : PerStoreCache<Category>
    {
        public override string nodeAlias { get; } = "uwbsCategory";

        protected override Category New(SearchResult r, Store store)
        {
            return new Category(r, store);
        }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logFac"></param>
        /// <param name="config"></param>
        /// <param name="examineManager"></param>
        /// <param name="storeCache"></param>
        public CategoryCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager,
            IBaseCache<Store> storeCache
        )
        {
            _config = config;
            _examineManager = examineManager;
            _storeCache = storeCache;

            _log = logFac.GetLogger(typeof(CategoryCache));
        }

        /// <summary>
        /// <see cref="ICache"/> implementation,
        /// accesses the product objects and removes given category from their list of categories. <para />
        /// Then removes category from all caches.
        /// </summary>
        public override void Remove(int id)
        {
            // Loop over each store specific cache of categories
            foreach (var kvp in Cache)
            {
                var categoryBeingRemoved = Cache[kvp.Key][id];

                // Loop over all the products in the category being removed
                foreach (var product in categoryBeingRemoved.Products)
                {
                    product.Categories.RemoveAll(x => x == categoryBeingRemoved);
                }
            }

            RemoveItemFromAllCaches(id);
        }
    }
}
