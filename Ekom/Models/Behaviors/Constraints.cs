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

        private INodeEntity? _node;
        /// <summary>
        /// Start of range that provider supports.
        /// </summary>
        public decimal StartRange
        {
            get
            {

                var store = _node == null
                    ? API.Store.Instance.GetStore()
                    : (_node as PerStoreNodeEntity)?.Store ?? API.Store.Instance.GetStore();

                // Check for multiple currencies
                if (store?.Currencies.Count() > 1)
                {
                    var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
                    var cookie = httpContext?.Request?.Cookies["EkomCurrency-" + store.Alias];

                    if (!string.IsNullOrEmpty(cookie))
                    {
                        return StartRanges.FirstOrDefault(x => x.Currency == cookie)?.Value ?? 0;
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
                if (_startRanges == null)
                {
                    var propertyAlias = (_node as PerStoreNodeEntity)?.Store.Alias;
                    var value = _node.Properties.GetPropertyValue("startOfRange", propertyAlias);
                    _startRanges = value.GetCurrencyValues() ?? new List<CurrencyValue>();
                }
                return _startRanges;
            }
            set => _startRanges = value;
        }

        /// <summary>
        /// End of range that provider supports.
        /// 0 means this provider supports carts of any cost.
        /// </summary>
        public decimal EndRange
        {
            get
            {
                var store = _node == null
                    ? API.Store.Instance.GetStore()
                    : (_node as PerStoreNodeEntity)?.Store ?? API.Store.Instance.GetStore();

                if (store?.Currencies.Count() > 1)
                {
                    var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
                    var cookie = httpContext?.Request?.Cookies["EkomCurrency-" + store.Alias];

                    if (!string.IsNullOrEmpty(cookie))
                    {
                        return EndRanges.FirstOrDefault(x => x.Currency == cookie)?.Value ?? 0;
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
                if (_endRanges == null)
                {
                    var propertyAlias = (_node as PerStoreNodeEntity)?.Store.Alias;
                    var value = _node.Properties.GetPropertyValue("endOfRange", propertyAlias);
                    _endRanges = value.GetCurrencyValues() ?? new List<CurrencyValue>();
                }
                return _endRanges;
            }
            set => _endRanges = value;
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

            if (node.Properties.TryGetValue("zone", out var zoneValue) && Guid.TryParse(zoneValue, out var zoneKey))
            {
                var zoneCache = Configuration.Resolver.GetService<IBaseCache<IZone>>();

                CountriesInZone = (zoneCache?.Cache.TryGetValue(zoneKey, out var zone) == true)
                    ? zone.Countries
                    : Enumerable.Empty<string>();
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
