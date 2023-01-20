using Ekom.Cache;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private UmbracoContent _storeRootNode;
        [JsonIgnore]
        [XmlIgnore]
        public virtual UmbracoContent StoreRootNode {
        
            get
            {
                if (_storeRootNode == null)
                {
                    if (Properties.HasPropertyValue("storeRootNode"))
                    {
                        var storeRootNodeUdi = GetValue("storeRootNode");

                        var storeRootNode = nodeService.NodeById(storeRootNodeUdi);

                        if (storeRootNode != null)
                        {
                            _storeRootNode = storeRootNode;
                        }
                    }
                }

                return _storeRootNode;
            }
        }
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
        public virtual IEnumerable<UmbracoDomain> Domains { get; }
        public virtual bool VatIncludedInPrice => Properties["vatIncludedInPrice"].ConvertToBool();
        public virtual string OrderNumberTemplate => Properties.GetPropertyValue("orderNumberTemplate");
        public virtual string OrderNumberPrefix => Properties.GetPropertyValue("orderNumberPrefix");
        public virtual string Url { get; }
        public virtual CultureInfo Culture
        {
            get
            {
                var ci = new CultureInfo(Properties["culture"]);

                return ci.TwoLetterISOLanguageName == "is" ? Configuration.IsCultureInfo : ci;
            }
        }
        public virtual CurrencyModel Currency
        {
            get
            {
                return GetCurrentCurrency();
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
                try
                {
                    var getString = Properties.GetPropertyValue("currency");
                    if (getString.Contains("["))
                    {
                        var deserialized = JsonConvert.DeserializeObject<List<CurrencyModel>>(getString);
                        return deserialized;
                    }
                    else
                    {
                        var c = Properties.ContainsKey("currency") && !string.IsNullOrEmpty(Properties["currency"]) ? Properties["currency"] : Culture.ToString();
                        var l = new List<CurrencyModel>();
                        l.Add(new CurrencyModel
                        {
                            CurrencyValue = c,
                            CurrencyFormat = "C"
                        });
                        return l;
                    }
                }
                catch (Exception ex)
                {
                    //Backword Compatability
                    var l = new List<CurrencyModel>();
                    l.Add(new CurrencyModel
                    {
                        CurrencyValue = Culture.ToString(),
                        CurrencyFormat = "C"
                    });
                    return l;
                }
            }


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
            }

            if (storeDomainCache.Cache.Any(x => x.Value.RootContentId == StoreRootNodeId))
            {
                Domains = storeDomainCache.Cache
                    .Where(x => x.Value.RootContentId == StoreRootNodeId)
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
