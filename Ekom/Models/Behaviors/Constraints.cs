using Ekom.Cache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Ekom.Utilities;
#if NETCOREAPP
using Microsoft.AspNetCore.Http;
#else
using Microsoft.Identity.Client;
using System.Web;
#endif

namespace Ekom.Models
{
    /// <summary>
    /// Constraints behavior for Shipping/Payment providers, and Discounts.
    /// </summary>
    public class Constraints : IConstraints
    {
        private readonly ILogger Logger;
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
                try
                {
#if NETCOREAPP
                    var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>().HttpContext;
#else
                    var httpContext = Configuration.Resolver.GetService<HttpContextBase>();
#endif

                    if (httpContext?.Request != null)
                    {
                        var store = _node is PerStoreNodeEntity perStoreNode
                            ? perStoreNode.Store
                            : API.Store.Instance.GetStore();

                        var cookie = httpContext.Request.Cookies["EkomCurrency-" + store.Alias];

#if NETCOREAPP
                        if (cookie != null && !string.IsNullOrEmpty(cookie))
                        {
                            var price = StartRanges.FirstOrDefault(x => x.Currency == cookie);
#else
                        if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                        {
                            var price = StartRanges.FirstOrDefault(x => x.Currency == cookie.Value);
#endif
                            if (price != null)
                            {
                                return price.Value;
                            }
                        }
                    }
                }
                catch (ArgumentNullException ex)
                {
                    Logger.LogError(ex, "Failed get start range from httpContext. Node: " + _node?.Id);
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
                try
                {
#if NETCOREAPP
                    var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>().HttpContext;
#else
                    var httpContext = Configuration.Resolver.GetService<HttpContextBase>();
#endif

                    if (httpContext?.Request != null)
                    {
                        var store = _node is PerStoreNodeEntity perStoreNode ? perStoreNode.Store : API.Store.Instance.GetStore();

                        var cookie = httpContext.Request.Cookies["EkomCurrency-" + store.Alias];

#if NETCOREAPP
                        if (cookie != null && !string.IsNullOrEmpty(cookie))
                        {
                            var price = EndRanges.FirstOrDefault(x => x.Currency == cookie);
#else
                        if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                        {
                            var price = EndRanges.FirstOrDefault(x => x.Currency == cookie.Value);
#endif
                            if (price != null)
                            {
                                return price.Value;
                            }
                        }
                    }
                }
                catch (ArgumentNullException ex)
                {
                    Logger.LogError(ex, "Failed get end range from httpContext. Node: " + _node?.Id);
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
            && Guid.TryParse(node.Properties["zone"], out var guidUdi))
            {
                zoneKey = guidUdi;
            }

            var zoneCache = Configuration.Resolver.GetService<IBaseCache<IZone>>();
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
