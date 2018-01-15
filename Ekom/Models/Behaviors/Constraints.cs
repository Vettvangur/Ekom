using Ekom.Cache;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;

namespace Ekom.Models.Behaviors
{
    /// <summary>
    /// Constraints behavior for Shipping/Payment providers, and Discounts.
    /// </summary>
    public class Constraints
    {
        private IBaseCache<Zone> __zoneCache;
        private IBaseCache<Zone> _zoneCache =>
            __zoneCache ?? (__zoneCache = Configuration.container.GetInstance<IBaseCache<Zone>>());

        INodeEntity _node;

        /// <summary>
        /// Start of range that provider supports.
        /// </summary>
        public int StartRange
        {
            get
            {
                int startRange = 0;

                if (_node is PerStoreNodeEntity perStoreNode)
                {
                    int.TryParse(_node.Properties.GetPropertyValue("startOfRange", perStoreNode.Store.Alias), out startRange);
                }
                else
                {
                    int.TryParse(_node.Properties.GetPropertyValue("startOfRange"), out startRange);
                }

                return startRange;
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
                int endRange = 0;

                if (_node is PerStoreNodeEntity perStoreNode)
                {
                    int.TryParse(_node.Properties.GetPropertyValue("endOfRange", perStoreNode.Store.Alias), out endRange);
                }
                else
                {
                    int.TryParse(_node.Properties.GetPropertyValue("endOfRange"), out endRange);
                }

                return endRange;
            }
        }

        /// <summary>
        /// Umbraco node id of zone this provider is a member of.
        /// Guid.Empty if none.
        /// </summary>
        public Guid Zone
        {
            get
            {
                if (_node.Properties.ContainsKey("zone") && GuidUdi.TryParse(_node.Properties["zone"], out var zoneKey))
                {
                    return zoneKey.Guid;
                }
                else
                {
                    return Guid.Empty;
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
                if (Zone != Guid.Empty
                && _zoneCache.Cache.ContainsKey(Zone))
                {
                    var zone = _zoneCache.Cache[Zone];

                    return zone.Countries;
                }

                return Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// ctor
        /// </summary>
        public Constraints(INodeEntity node)
        {
            _node = node;
        }
    }
}
