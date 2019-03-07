using Ekom.Exceptions;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models.Discounts;
using Ekom.Services;
using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Umbraco.Core.Models;


namespace Ekom.Cache
{
    class ProductDiscountCache : PerStoreCache<IProductDiscount>
    {
        //protected BaseSearchProvider _searcher => _examineManager.SearchProviderCollection[_config.ExamineSearcher];

        public override string NodeAlias { get; } = "ekmProductDiscount";

        public ProductDiscountCache(
            ILogFactory logFac,
            Configuration config,
            IBaseCache<IStore> storeCache,
            IPerStoreFactory<IProductDiscount> perStoreFactory
        ) : base(config, storeCache, perStoreFactory)
        {
            _log = logFac.GetLogger(typeof(ProductDiscountCache));
        }

    }
}
