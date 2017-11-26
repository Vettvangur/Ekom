using Ekom.Cache;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Examine;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Models;

namespace Ekom.Factories
{
    /// <summary>
    /// Unused atm
    /// </summary>
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
            var properties = new Dictionary<string, string>();

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

            foreach (var field in item.Fields.Where(x => !x.Key.StartsWith("__")))
            {
                properties.Add(field.Key, field.Value);
            }

            return new VariantGroup(store, _variantCache)
            {
                Properties = properties,
                Key = _key,
                Id = item.Id,
                ProductKey = _productKey,
                Title = item.GetStoreProperty("title", store.Alias),
                SortOrder = Convert.ToInt32(item.Fields["sortOrder"]),
            };
        }

        public VariantGroup Create(IContent item, Store store)
        {
            var properties = new Dictionary<string, string>();

            var productKey = item.Parent().Key;

            foreach (var prop in item.Properties)
            {
                properties.Add(prop.Alias, prop.Value?.ToString());
            }

            return new VariantGroup(store, _variantCache)
            {
                Properties = properties,
                Id = item.Id,
                Key = item.Key,
                ProductKey = productKey,
                Title = item.GetStoreProperty("title", store.Alias),
                SortOrder = item.SortOrder,
            };
        }
    }
}
