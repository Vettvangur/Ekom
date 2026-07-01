using Ekom.Cache;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;


namespace Ekom.Models;

/// <summary>
/// Constraints behavior for Shipping/Payment providers, and Discounts.
/// </summary>
public class Constraints : IConstraints
{
    private List<CurrencyValue>? _manualStartRanges;
    private List<CurrencyValue>? _manualEndRanges;

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

            IStore? store = _node == null
                ? API.Store.Instance.GetStore()
                : (_node as PerStoreNodeEntity)?.Store ?? API.Store.Instance.GetStore();

            // Check for multiple currencies
            if (store?.Currencies.Count() > 1)
            {
                HttpContext? httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
                string? cookie = httpContext?.Request?.Cookies["EkomCurrency-" + store.Alias];

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
            if (_manualStartRanges != null)
            {
                return _manualStartRanges;
            }

            if (_startRanges == null)
            {
                string? propertyAlias = (_node as PerStoreNodeEntity)?.Store.Alias;
                string value = _node.Properties.GetPropertyValue("startOfRange", propertyAlias);
                _startRanges = value.GetCurrencyValues(propertyAlias) ?? new List<CurrencyValue>();
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
            IStore? store = _node == null
                ? API.Store.Instance.GetStore()
                : (_node as PerStoreNodeEntity)?.Store ?? API.Store.Instance.GetStore();

            if (store?.Currencies.Count() > 1)
            {
                HttpContext? httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;
                string? cookie = httpContext?.Request?.Cookies["EkomCurrency-" + store.Alias];

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
            if (_manualEndRanges != null)
            {
                return _manualEndRanges;
            }

            if (_endRanges == null)
            {
                string? propertyAlias = (_node as PerStoreNodeEntity)?.Store.Alias;
                string value = _node.Properties.GetPropertyValue("endOfRange", propertyAlias);
                _endRanges = value.GetCurrencyValues(propertyAlias) ?? new List<CurrencyValue>();
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

        if (node.Properties.TryGetValue("zone", out string? zoneValue) && Guid.TryParse(zoneValue, out Guid zoneKey))
        {
            IBaseCache<IZone>? zoneCache = Configuration.Resolver.GetService<IBaseCache<IZone>>();

            CountriesInZone = (zoneCache?.Cache.TryGetValue(zoneKey, out IZone? zone) == true)
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
        _manualStartRanges = startRange.ToString().GetCurrencyValues();
        _manualEndRanges = endRange.ToString().GetCurrencyValues();
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
