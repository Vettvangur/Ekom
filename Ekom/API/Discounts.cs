using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Services;
using log4net;
using System.Collections.Generic;
using System.Linq;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, grants access to the current discounts 
    /// </summary>
    public class Discounts
    {
        /// <summary>
        /// Discount Instance
        /// </summary>
        public static Discounts Instance => Configuration.container.GetInstance<Discounts>();

        readonly ILog _log;
        readonly Configuration _config;
        readonly IPerStoreCache<IDiscount> _discountCache;
        readonly IStoreService _storeSvc;

        /// <summary>
        /// ctor
        /// </summary>
        internal Discounts(
            Configuration config,
            ILogFactory logFac,
            IPerStoreCache<IDiscount> discountCache,
            IStoreService storeService
        )
        {
            _config = config;
            _log = logFac.GetLogger<Discounts>();
            _discountCache = discountCache;
            _storeSvc = storeService;
        }

        /// <summary>
        /// Gets all discounts
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IDiscount> GetDiscounts()
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                return GetDiscounts(store.Alias);
            }

            return null;
        }

        /// <summary>
        /// Gets all discounts
        /// </summary>
        /// <param name="storeAlias"></param>
        /// <returns></returns>
        public IEnumerable<IDiscount> GetDiscounts(string storeAlias)
        {
            return _discountCache.Cache[storeAlias].Select(x => x.Value);
        }
    }
}
