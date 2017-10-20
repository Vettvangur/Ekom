using Examine;
using System;
using Umbraco.Core.Models;
using Ekom.Cache;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;

namespace Ekom.Factories
{
    class VariantGroupFactory : IObjectFactory<VariantGroup>
    {
        ExamineService _examineSvc;
        IPerStoreCache<Variant> _variantCache;
        IPerStoreCache<Product> _productCache;
        public VariantGroupFactory(
            ExamineService examineSvc,
            IPerStoreCache<Variant> cache,
            IPerStoreCache<Product> productCache
        )
        {
            _examineSvc = examineSvc;
            _variantCache = cache;
            _productCache = productCache;
        }

        public VariantGroup Create(SearchResult item, Store store)
        {
            var key = item.Fields["key"];

            var _key = new Guid();

            if (!Guid.TryParse(key, out _key))
            {
                throw new Exception("No key present for variant group.");
            }

            int productId = Convert.ToInt32(item.Fields["parentID"]);

            var productExamine = _examineSvc.GetExamineNode(productId);

            if (productExamine == null)
            {
                throw new Exception("Product not found in examine for Variant group. Id:" + productId);
            }

            var productKey = productExamine.Fields["key"];

            var _productKey = new Guid();

            if (!Guid.TryParse(productKey, out _productKey))
            {
                throw new Exception("No key present for product in variant group.");
            }

            return new VariantGroup(store, _variantCache)
            {
                Key = _key,
                Id = item.Id,
                ProductKey = _productKey,
                Title = item.GetStoreProperty("title", store.Alias),
                SortOrder = Convert.ToInt32(item.Fields["sortOrder"]),
            };
        }

        public VariantGroup Create(IContent item, Store store)
        {
            var productKey = item.Parent().Key;

            return new VariantGroup(store, _variantCache)
            {

                Id = item.Id,
                Key = item.Key,
                ProductKey = productKey,
                Title = item.GetStoreProperty("title", store.Alias),
                SortOrder = item.SortOrder,
            };
        }
    }
}
