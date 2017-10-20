using Examine;
using System;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
    class CategoryCache : PerStoreCache<Category>
    {
        public override string NodeAlias { get; } = "uwbsCategory";

        protected override Category New(SearchResult r, Store store)
        {
            return new Category(r, store);
        }

        public CategoryCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager,
            IBaseCache<Store> storeCache
        ) : base(config, examineManager, storeCache)
        {
            _log = logFac.GetLogger(typeof(CategoryCache));
        }

        /// <summary>
        /// <see cref="ICache"/> implementation,
        /// accesses the product objects and removes given category from their list of categories. <para />
        /// Then removes category from all caches.
        /// </summary>
        public override void Remove(Guid id)
        {
            // Loop over each store specific cache of categories
            foreach (var kvp in Cache)
            {
                // This is failing sometimes when using content service to delete items, need more time to look into it, this is a try/catch hack
                try
                {
                    var categoryBeingRemoved = Cache[kvp.Key][id];

                    // Loop over all the products in the category being removed
                    foreach (var product in categoryBeingRemoved.Products)
                    {
                        product.Categories().RemoveAll(x => x == categoryBeingRemoved);
                    }

                }
                catch (Exception)
                {
                    // Logging 
                }
            }

            RemoveItemFromAllCaches(id);
        }
    }
}
