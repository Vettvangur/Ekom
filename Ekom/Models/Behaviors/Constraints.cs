using Ekom.Cache;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


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

                IStore store = null;

                try
                {
                    store = _node is PerStoreNodeEntity perStoreNode
                    ? perStoreNode.Store
                    : API.Store.Instance.GetStore();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Provider constraints for StartRange could not get store.");
                }

                if (store != null && store.Currencies.Count() > 1)
                {
                    try
                    {
                        var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;

                        if (httpContext?.Request != null)
                        {
                            var cookie = httpContext.Request.Cookies["EkomCurrency-" + store.Alias];

                            if (cookie != null && !string.IsNullOrEmpty(cookie))
                            {
                                var price = StartRanges.FirstOrDefault(x => x.Currency == cookie);

                                if (price != null)
                                {
                                    return price.Value;
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed get StartRange from httpContext. Node: " + _node?.Id);
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

                IStore store = null;

                try
                {
                    store = _node is PerStoreNodeEntity perStoreNode
                    ? perStoreNode.Store
                    : API.Store.Instance.GetStore();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Provider constraints for EndRange could not get store.");
                }

                if (store != null && store.Currencies.Count() > 1)
                {
                    try
                    {
                        var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;

                        if (httpContext?.Request != null)
                        {
                            var cookie = httpContext.Request.Cookies["EkomCurrency-" + store.Alias];

                            if (cookie != null && !string.IsNullOrEmpty(cookie))
                            {
                                var price = EndRanges.FirstOrDefault(x => x.Currency == cookie);

                                if (price != null)
                                {
                                    return price.Value;
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed get EndRange from httpContext. Node: " + _node?.Id);
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
