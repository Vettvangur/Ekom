using Ekom.Cache;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Globalization;
using System.Xml.Serialization;

namespace Ekom.Models
{
    /// <summary>
    /// Ekom Store, used to f.x. have seperate products and entities per store.
    /// </summary>
    public class Store : NodeEntity, IStore
    {
        private INodeService nodeService => Configuration.Resolver.GetService<INodeService>();
        private IStoreDomainCache storeDomainCache => Configuration.Resolver.GetService<IStoreDomainCache>();
        /// <summary>
        /// Usually a two letter code, f.x. EU/IS/DK
        /// </summary>
        public virtual string Alias => Properties["nodeName"];
        

        [JsonIgnore]
        [XmlIgnore]
        public virtual UmbracoContent StoreRootNode { get; set; }
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
        public virtual string OrderNumberTemplate => Properties.GetPropertyValue("orderNumberTemplate");
        public virtual string OrderNumberPrefix => Properties.GetPropertyValue("orderNumberPrefix");
        public virtual string Url { get; }
        public virtual CultureInfo Culture
        {
            get
            {
                var httpContext = Configuration.Resolver.GetService<IHttpContextAccessor>()?.HttpContext;

                var culture = httpContext?.Request.HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture;

                if (culture != null)
                {
                    var c = Cultures.FirstOrDefault(x => x.Name == culture.Name);

                    if (c != null)
                    {
                        return c;
                    }
                }

                return Cultures.FirstOrDefault();
            }
        }
        public virtual List<CultureInfo> Cultures
        {
            get
            {
                if (!Properties.ContainsKey("cultures"))
                {
                    var ci = new CultureInfo(Properties["culture"]);

                    ci = ci.TwoLetterISOLanguageName == "is" ? Configuration.IsCultureInfo : ci;

                    return new List<CultureInfo>() { ci };
                }

                var cultures = Properties["cultures"];

                return cultures.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).Select(x => new CultureInfo(x)).ToList();
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
                return Properties.ContainsKey("userBasket") ? Properties.GetPropertyValue("userBasket").IsBoolean() : Configuration.Instance.UserBasket;
            }
        }

        public virtual bool ShareBasketBetweenStores
        {
            get
            {
                return Properties.ContainsKey("ShareBasketBetweenStores") ? Properties.GetPropertyValue("ShareBasketBetweenStores").IsBoolean() : Configuration.Instance.ShareBasketBetweenStores;
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
                Properties.TryGetValue("currency", out var currencyJson);

                // Check if the value is JSON array format
                if (!string.IsNullOrEmpty(currencyJson) && currencyJson.Contains("["))
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
                var deserializedList = JsonConvert.DeserializeObject<List<CurrencyModel>>(json);
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
            var currencyValue = !string.IsNullOrEmpty(currency) ? currency : Culture.ToString();

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
            if (item.Properties.HasPropertyValue("storeRootNode"))
            {
                var storeRootNodeUdi = item.GetValue("storeRootNode");

                Url = nodeService.GetUrl(storeRootNodeUdi);
                StoreRootNode = nodeService.NodeById(storeRootNodeUdi);
            }

            if (storeDomainCache.Cache.Any(x => x.Value.RootContentId == StoreRootNodeId))
            {
                Domains = storeDomainCache.Cache
                    .Where(x => x.Value.RootContentId == StoreRootNodeId && Cultures.Select(x => x.Name).Contains(x.Value.LanguageIsoCode))
                    .Select(x => x.Value)
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
}
