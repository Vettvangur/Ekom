using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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
        {
            return (!string.IsNullOrEmpty(countryCode) ? (!CountriesInZone.Any() || CountriesInZone.Contains(countryCode.ToUpper())) : true)
            && StartRange <= amount
            && (EndRange == 0 || EndRange >= amount);
        }

        private INodeEntity _node;
        /// <summary>
        /// Start of range that provider supports.
        /// </summary>
        public decimal StartRange
        {

            get
            {
                var httpContext = Current.Factory.GetInstance<HttpContextBase>();

                if (httpContext?.Request != null)
                {
                    var store = _node is PerStoreNodeEntity perStoreNode 
                        ? perStoreNode.Store 
                        : API.Store.Instance.GetStore();

                    var cookie = httpContext.Request.Cookies["EkomCurrency-" + store.Alias];

                    if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                    {
                        var price = StartRanges.FirstOrDefault(x => x.Currency == cookie.Value);

                        if (price != null)
                        {
                            return price.Value;
                        }
                    }
                }

                return StartRanges.FirstOrDefault()?.Value ?? 0;
            }
        }
        private List<CurrencyValue> _startRanges;
        public List<CurrencyValue> StartRanges
        {
            get
            {
                if (_startRanges != null)
                {
                    return _startRanges;
                }

                if (_node is PerStoreNodeEntity perStoreNode)
                {
                    var value = _node.Properties.GetPropertyValue("startOfRange", perStoreNode.Store.Alias);

                    return value.GetCurrencyValues();
                }
                else
                {
                    var value = _node.Properties.GetPropertyValue("startOfRange");

                    return value.GetCurrencyValues();
                }
            }
            set
            {
                _startRanges = value;
            }
        }

        /// <summary>
        /// End of range that provider supports.
        /// 0 means this provider supports carts of any cost.
        /// </summary>
        public decimal EndRange
        {
            get
            {
                var httpContext = Current.Factory.GetInstance<HttpContextBase>();

                if (httpContext?.Request != null)
                {
                    var store = _node is PerStoreNodeEntity perStoreNode ? perStoreNode.Store : API.Store.Instance.GetStore();

                    var cookie = httpContext.Request.Cookies["EkomCurrency-" + store.Alias];

                    if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                    {
                        var price = EndRanges.FirstOrDefault(x => x.Currency == cookie.Value);

                        if (price != null)
                        {
                            return price.Value;
                        }
                    }
                }

                return EndRanges.FirstOrDefault()?.Value ?? 0;
            }
        }
        private List<CurrencyValue> _endRanges;
        public List<CurrencyValue> EndRanges
        {
            get
            {
                if (_endRanges != null)
                {
                    return _endRanges;
                }

                if (_node is PerStoreNodeEntity perStoreNode)
                {
                    var value = _node.Properties.GetPropertyValue("endOfRange", perStoreNode.Store.Alias);

                    return value.GetCurrencyValues();
                }
                else
                {
                    var value = _node.Properties.GetPropertyValue("endOfRange");

                    return value.GetCurrencyValues();
                }

            }
            set
            {
                _endRanges = value;
            }
        }

        /// <summary>
        /// All countries in <see cref="Models.Zone"/>
        /// </summary>
        public IEnumerable<string> CountriesInZone { get; }

        /// <summary>
        /// ctor
        /// </summary>
        public Constraints(INodeEntity node)
        {
            _node = node;

            Guid zoneKey = Guid.Empty;

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
            CountriesInZone = countriesInZone;
        }

        /// <summary>
        /// Freeze and clone <see cref="IConstraints"/>
        /// </summary>
        /// <param name="constraints"></param>
        public Constraints(IConstraints constraints)
        {
            StartRanges = constraints.StartRanges;
            EndRanges = constraints.EndRanges;
            CountriesInZone = new List<string>(constraints.CountriesInZone);
        }
    }
}
