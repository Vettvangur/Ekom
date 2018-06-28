using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.API
{
    /// <summary>
    /// The Ekom API, grants access to the current discounts 
    /// </summary>
    public class Discount
    {
        /// <summary>
        /// Discount Instance
        /// </summary>
        public static Discount Instance => Configuration.container.GetInstance<Discount>();

        ILog _log;
        Configuration _config;
        IPerStoreCache<IDiscount> _discountCache;
        IStoreService _storeSvc;

        /// <summary>
        /// ctor
        /// </summary>
        internal Discount(

            Configuration config,
            ILogFactory logFac,
            IPerStoreCache<IDiscount> discountCache,
            IStoreService storeService
        )
        {
            _config = config; 
            _log = logFac.GetLogger<Catalog>();
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
