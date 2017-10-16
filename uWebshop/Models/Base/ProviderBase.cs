﻿using Examine;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Umbraco.Core.Models;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Interfaces;

namespace uWebshop.Models.Base
{
    /// <summary>
    /// Base class for Shipping/Payment providers
    /// </summary>
    public abstract class ProviderBase : PerStoreNodeEntity, INodeEntity
    {
        private IBaseCache<Zone> __zoneCache;
        private IBaseCache<Zone> _zoneCache =>
            __zoneCache ?? (__zoneCache = Configuration.container.GetService<IBaseCache<Zone>>());

        /// <summary>
        /// Start of range that provider supports.
        /// </summary>
        public int StartRange
        {
            get
            {
                if (int.TryParse(Properties.GetStoreProperty("startOfRange", _store.Alias), out int startRange))
                {
                    return startRange;
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// End of range that provider supports.
        /// 0 means this provider supports carts of any cost.
        /// </summary>
        public int EndRange
        {
            get
            {
                if (int.TryParse(Properties.GetStoreProperty("endOfRange", _store.Alias), out int endRange))
                {
                    return endRange;
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Umbraco node id of zone this provider is a member of.
        /// -1 if none.
        /// </summary>
        public int Zone
        {
            get
            {
                if (int.TryParse(Properties.GetStoreProperty("zone", _store.Alias), out int zoneId))
                {
                    return zoneId;
                }
                else
                {
                    return -1;
                }
            }
        }

        /// <summary>
        /// All countries in <see cref="Models.Zone"/>
        /// </summary>
        public IEnumerable<string> CountriesInZone
        {
            get
            {
                if (Zone != -1)
                {
                    var zone = _zoneCache.Cache.FirstOrDefault(x => x.Value.Id == Zone);

                    return zone.Value.Countries;
                }

                return Enumerable.Empty<string>();
            }
        }

        IDiscountedPrice _price;
        /// <summary>
        /// 
        /// </summary>
        public IDiscountedPrice Price => _price
            ?? (_price = new Price(Properties.GetStoreProperty("price", _store.Alias), _store));

        /// <summary>
        /// Used by uWebshop extensions
        /// </summary>
        /// <param name="store"></param>
        public ProviderBase(Store store) : base(store) { }

        /// <summary>
        /// Construct Provider from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public ProviderBase(SearchResult item, Store store) : base(item, store) { }

        /// <summary>
        /// Construct Provider from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public ProviderBase(IContent node, Store store) : base(node, store) { }
    }
}
