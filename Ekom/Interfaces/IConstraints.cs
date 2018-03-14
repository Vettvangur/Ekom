using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Constraints behavior for Shipping/Payment providers, and Discounts.
    /// </summary>
    public interface IConstraints
    {
        /// <summary>
        /// All countries in <see cref="Models.Zone"/>
        /// </summary>
        IEnumerable<string> CountriesInZone { get; }
        /// <summary>
        /// End of range that provider supports.
        /// 0 means this provider supports carts of any cost.
        /// </summary>
        int EndRange { get; }
        /// <summary>
        /// Start of range that provider supports.
        /// </summary>
        int StartRange { get; }
        /// <summary>
        /// Umbraco node id of zone this provider is a member of.
        /// Guid.Empty if none.
        /// </summary>
        Guid Zone { get; }

        /// <summary>
        /// Determine if constraints conditions are met.
        /// </summary>
        /// <param name="countryCode"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        bool IsValid(string countryCode, decimal amount);
    }
}
