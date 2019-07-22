using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;

namespace Ekom.Models.Behaviors
{
    /// <summary>
    /// Constraints behavior for Shipping/Payment providers, and Discounts.
    /// </summary>
    public class Constraints : IConstraints
    {
        /// <summary>
        /// Determine if the given provider is valid given the provided properties.
        /// </summary>
        /// <param name="countryCode"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public bool IsValid(
            string countryCode,
            decimal amount
        )
            => (!CountriesInZone.Any() || CountriesInZone.Contains(countryCode.ToUpper()))
            && StartRange <= amount
            && (EndRange == 0 || EndRange >= amount)
        ;

        /// <summary>
        /// Start of range that provider supports.
        /// </summary>
        public int StartRange { get; }

        /// <summary>
        /// End of range that provider supports.
        /// 0 means this provider supports carts of any cost.
        /// </summary>
        public int EndRange { get; }

        /// <summary>
        /// All countries in <see cref="Models.Zone"/>
        /// </summary>
        public IEnumerable<string> CountriesInZone { get; }

        /// <summary>
        /// ctor
        /// </summary>
        public Constraints(INodeEntity node)
        {
            int startRange = 0;
            int endRange = int.MaxValue;
            Guid zoneKey = Guid.Empty;
            if (node is PerStoreNodeEntity perStoreNode)
            {
                var startPropValue = node.Properties.GetPropertyValue("startOfRange", perStoreNode.Store.Alias);
                var endPropValue = node.Properties.GetPropertyValue("endOfRange", perStoreNode.Store.Alias);
                int.TryParse(startPropValue, out startRange);
                int.TryParse(endPropValue, out endRange);
            }
            else
            {
                var startPropValue = node.Properties.GetPropertyValue("startOfRange");
                var endPropValue = node.Properties.GetPropertyValue("startOfRange");
                int.TryParse(startPropValue, out startRange);
                int.TryParse(endPropValue, out endRange);
            }

            StartRange = startRange;
            EndRange = endRange;

            if (node.Properties.ContainsKey("zone")
            && GuidUdi.TryParse(node.Properties["zone"], out var guidUdi))
            {
                zoneKey = guidUdi.Guid;
            }

            var zoneCache = Current.Factory.GetInstance<IBaseCache<IZone>>();
            if (zoneKey != Guid.Empty
            && zoneCache.Cache.ContainsKey(zoneKey))
            {
                var zone = zoneCache.Cache[zoneKey];

                CountriesInZone = zone.Countries;
            }
            else
            {
                CountriesInZone = Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        [JsonConstructor]
        public Constraints(
            int startRange,
            int endRange,
            Guid zone,
            IEnumerable<string> countriesInZone)
        {
            StartRange = startRange;
            EndRange = endRange;
            CountriesInZone = countriesInZone;
        }

        /// <summary>
        /// Freeze and clone <see cref="IConstraints"/>
        /// </summary>
        /// <param name="constraints"></param>
        public Constraints(IConstraints constraints)
        {
            StartRange = constraints.StartRange;
            EndRange = constraints.EndRange;
            CountriesInZone = new List<string>(constraints.CountriesInZone);
        }
    }
}
