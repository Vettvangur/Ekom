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

        /// <summary>
        /// <see cref="ICache"/> implementation,
        /// accesses the product objects and removes given category from their list of categories. <para />
        /// Then removes category from all caches.
        /// </summary>
        public override void Remove(int id)
        {
            // Loop over each store specific cache of categories
            foreach (var kvp in this._cache)
            {
                var categoryBeingRemoved = this._cache[kvp.Key][id];

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
