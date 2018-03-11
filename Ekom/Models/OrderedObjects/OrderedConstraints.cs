using Ekom.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Ekom.Models.OrderedObjects
{
    /// <summary>
    /// Constraints behavior for Shipping/Payment providers, and Discounts.
    /// </summary>
    public class OrderedConstraints : IConstraints
    {
        /// <summary>
        /// 
        /// </summary>
        [JsonConstructor]
        public OrderedConstraints(int startRange, int endRange, Guid zone, IEnumerable<string> countriesInZone)
        {
            StartRange = startRange;
            EndRange = endRange;
            Zone = zone;
            CountriesInZone = countriesInZone;
        }

        /// <summary>
        /// Freeze and clone <see cref="IConstraints"/>
        /// </summary>
        /// <param name="constraints"></param>
        public OrderedConstraints(IConstraints constraints)
        {
            StartRange = constraints.StartRange;
            EndRange = constraints.EndRange;
            Zone = constraints.Zone;
            CountriesInZone = new List<string>(constraints.CountriesInZone);
        }


        /// <summary>
        /// Start of range that provider supports.
        /// </summary>
        public int StartRange { get; internal set; }
        /// <summary>
        /// End of range that provider supports.
        /// 0 means this provider supports carts of any cost.
        /// </summary>
        public int EndRange { get; internal set; }

        /// <summary>
        /// Umbraco node id of zone this provider is a member of.
        /// Guid.Empty if none.
        /// </summary>
        public Guid Zone { get; internal set; }

        /// <summary>
        /// All countries in <see cref="Models.Zone"/>
        /// </summary>
        public IEnumerable<string> CountriesInZone { get; internal set; }
    }
}
