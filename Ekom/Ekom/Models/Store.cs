using Ekom.Cache;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Globalization;
using System.Xml.Serialization;

namespace Ekom.Models;

/// <summary>
/// Ekom Store, used to f.x. have seperate products and entities per store.
/// </summary>
public class Store : NodeEntity, IStore
{
    /// <summary>
    /// Usually a two letter code, f.x. EU/IS/DK
    /// </summary>
    public virtual string Alias => Properties["nodeName"];


    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual UmbracoContent? StoreRootNode { get; set; }
    public virtual int StoreRootNodeId
    {
        get
        {
            if (StoreRootNode != null)
            {
                return StoreRootNode.Id;
            }
            return 0;
        }
    }
    public virtual IEnumerable<UmbracoDomain> Domains { get; } = new List<UmbracoDomain>();
    public virtual bool VatIncludedInPrice => Properties["vatIncludedInPrice"].ConvertToBool();
    public virtual string OrderNumberTemplate => GetValue("orderNumberTemplate");
    public virtual string OrderNumberPrefix => GetValue("orderNumberPrefix");
    public virtual string UrlPrefix(string culture)
    {
        var value = GetValue("urlPrefix", culture);
        if (string.IsNullOrWhiteSpace(value))
            return value;

        // Ensure it starts with "/" and does NOT end with "/"
        if (!value.StartsWith("/"))
            value = "/" + value;

        if (value.Length > 1 && value.EndsWith("/"))
            value = value.TrimEnd('/');

        return value;
        
    }

    public virtual string Url { get; }


    public virtual CultureInfoDto Culture
    {
        get
        {
            HttpContext? httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;

            CultureInfo? culture = httpContext?.Request.HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture;

            if (culture != null)
            {
                var c = Cultures.FirstOrDefault(x => x.Name == culture.Name);

                if (c != null)
                {
                    return c;
                }
            }

            return Cultures.FirstOrDefault() ?? new CultureInfoDto() {  Name = "en-US" };
        }
    }

    public virtual List<CultureInfoDto> Cultures
    {
        get
        {
            if (!Properties.ContainsKey("cultures"))
            {
                CultureInfo ci = new CultureInfo(Properties["culture"]);

                ci = ci.TwoLetterISOLanguageName == "is" ? Configuration.IsCultureInfo : ci;

                return new List<CultureInfoDto>() { CultureInfoDto.From(ci) };
            }

            string cultures = Properties["cultures"];

            return cultures.Split(["\r\n", "\n", "\r"], StringSplitOptions.None).Select(x => CultureInfoDto.From(new CultureInfo(x))).ToList();
        }
    }

    public virtual CurrencyModel Currency
    {
        get
        {
            return GetCurrentCurrency();
        }
    }

    public virtual bool UserBasket
    {
        get
        {
            return Properties.ContainsKey("userBasket") ? Properties.GetValue("userBasket").IsBoolean() : Configuration.Instance.UserBasket;
        }
    }

    public virtual bool ShareBasketBetweenStores
    {
        get
        {
            return Properties.ContainsKey("shareBasketBetweenStores") ? Properties.GetValue("shareBasketBetweenStores").IsBoolean() : Configuration.Instance.ShareBasketBetweenStores;
        }
    }

    public virtual bool ApplyVatOnShipping
    {
        get
        {
            return Properties.ContainsKey("applyVatOnShipping") ? Properties.GetValue("applyVatOnShipping").IsBoolean() : Configuration.Instance.ApplyVatOnShipping;
        }
    }

    public CurrencyModel GetCurrentCurrency()
    {
        return CookieHelper.GetCurrencyCookieValue(Currencies, Alias);
    }

    public virtual List<CurrencyModel> Currencies
    {
        get
        {
            // Retrieve the currency property value once
            Properties.TryGetValue("currency", out string? currencyJson);

            // Check if the value is JSON array format
            if (!string.IsNullOrEmpty(currencyJson) && currencyJson.Contains("[", StringComparison.InvariantCultureIgnoreCase))
            {
                return TryDeserializeCurrencyList(currencyJson);
            }

            // Default single currency scenario
            return CreateDefaultCurrencyList(currencyJson);
        }
    }

    private List<CurrencyModel> TryDeserializeCurrencyList(string json)
    {
        try
        {
            List<CurrencyModel>? deserializedList = JsonConvert.DeserializeObject<List<CurrencyModel>>(json);
            if (deserializedList != null)
            {
                return deserializedList;
            }
        }
        catch (JsonException ex)
        {
            throw new JsonException("Failed to deserialize currency JSON: " + ex.Message);
        }

        return new List<CurrencyModel>();
    }

    private List<CurrencyModel> CreateDefaultCurrencyList(string currency)
    {
        // Use the currency value if available, otherwise default to Culture
        string currencyValue = !string.IsNullOrEmpty(currency) ? currency : Culture.Name;

        return new List<CurrencyModel>
        {
            new CurrencyModel
            {
                CurrencyValue = currencyValue,
                CurrencyFormat = "C"
            }
        };
    }

    /// <summary>
    /// Umbraco input: 28.5 <para></para>
    /// Stored VAT value: 0.285<para></para>
    /// Effective VAT value: 28.5%<para></para>
    /// </summary>
    public virtual decimal Vat => string.IsNullOrEmpty(Properties.GetPropertyValue("vat"))
        ? 0
        : Convert.ToDecimal(Properties["vat"]) / 100;

    /// <summary>
    /// Used by Ekom extensions
    /// </summary>
    internal protected Store() : base() { }
    /// <summary>
    /// Construct Store
    /// </summary>
    /// <param name="item"></param>
    internal protected Store(UmbracoContent item) : base(item)
    {
        using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var nodeService = scope.ServiceProvider.GetService<INodeService>();
        var storeDomainCache = scope.ServiceProvider.GetService<IStoreDomainCache>();
        var umbracoService = scope.ServiceProvider.GetService<Services.IUmbracoService>();

        if (item.Properties.HasPropertyValue("storeRootNode"))
        {
            string storeRootNodeUdi = item.GetValue("storeRootNode");

            Url = nodeService?.GetUrl(storeRootNodeUdi) ?? "";
            StoreRootNode = nodeService?.NodeById(storeRootNodeUdi);

            if (StoreRootNode == null)
            {
                throw new InvalidOperationException("Store root node could not be found for store: " + Alias + " storeRootNodeUdi: " + storeRootNodeUdi);
            }
            if (string.IsNullOrEmpty(Url))
            {
                throw new InvalidOperationException("Store root node URL could not be resolved for store: " + Alias + " storeRootNodeUdi: " + storeRootNodeUdi);
            }
        }

        if (storeDomainCache?.Cache.Any(x => x.Value.RootContentId == StoreRootNodeId) == true)
        {
            var defaultLanguageCode = umbracoService?.DefaultLanguage();

            Domains = storeDomainCache.Cache
                .Where(x => x.Value.RootContentId == StoreRootNodeId)
                .Select(x => x.Value)
                .OrderBy(x => !string.Equals(x.LanguageIsoCode, defaultLanguageCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            //TODO If not culture/domain is set then add default
            //if (uCtx.HttpContext != null)
            //{
            //    Domains = Enumerable.Repeat(new Domain(uCtx.HttpContext.Request.Url?.Host, StoreRootNode), 1);
            //}

        }
    }
}
