using Ekom.Models;
using System.Collections.Generic;

namespace Ekom.Models
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
        decimal EndRange { get; }

        List<CurrencyValue> EndRanges { get; }
        /// <summary>
        /// Start of range that provider supports.
        /// </summary>
        decimal StartRange { get; }
        List<CurrencyValue> StartRanges { get; }
        /// <summary>
        /// Determine if constraints conditions are met.
        /// </summary>
        /// <param name="countryCode"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        bool IsValid(string countryCode, decimal amount);
    }
}
