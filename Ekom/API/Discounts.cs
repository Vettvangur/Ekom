using Ekom.Cache;
using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        public static Discounts Instance => Configuration.Resolver.GetService<Discounts>();

        readonly ILogger<Discounts> _logger;
        readonly Configuration _config;
        readonly IPerStoreCache<IDiscount> _discountCache;
        readonly IStoreService _storeSvc;


        /// <summary>
        /// ctor
        /// </summary>
        internal Discounts(
            Configuration config,
            ILogger<Discounts> logger,
            IPerStoreCache<IDiscount> discountCache,
            IStoreService storeService
        )
        {
            _config = config;
            _logger = logger;
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

            return Enumerable.Empty<IDiscount>();
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


        /// <summary>
        /// Gets all discounts
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IDiscount> GetGlobalDiscounts()
        {
            var store = _storeSvc.GetStoreFromCache();

            if (store != null)
            {
                return GetGlobalDiscounts(store.Alias);
            }

            return Enumerable.Empty<IDiscount>();
        }
        /// <summary>
        /// Gets all discounts
        /// </summary>
        /// <param name="storeAlias"></param>
        /// <returns></returns>
        public IEnumerable<IDiscount> GetGlobalDiscounts(string storeAlias)
        {
            return GetDiscounts(storeAlias).Where(d => d.GlobalDiscount);
        }
    }
}
