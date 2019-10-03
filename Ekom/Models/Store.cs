using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Utilities;
using Examine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Ekom.Models
{
    /// <summary>
    /// Ekom Store, used to f.x. have seperate products and entities per store.
    /// </summary>
    public class Store : NodeEntity, IStore
    {
        /// <summary>
        /// Usually a two letter code, f.x. EU/IS/DK
        /// </summary>
        public virtual string Alias => Properties["nodeName"];
        public virtual int StoreRootNode { get; }
        public virtual IEnumerable<Domain> Domains { get; }
        public virtual bool VatIncludedInPrice => Properties["vatIncludedInPrice"].ConvertToBool();
        public virtual string OrderNumberTemplate => Properties.GetPropertyValue("orderNumberTemplate");
        public virtual string OrderNumberPrefix => Properties.GetPropertyValue("orderNumberPrefix");
        public virtual string Url { get; }
        public virtual CultureInfo Culture => new CultureInfo(Properties["culture"]);
        public virtual string Currency => Properties.GetPropertyValue("currency");
        public virtual List<CurrencyModel> CurrencyModel
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
                    Current.Logger.Error<Store>("Unable to parse currency object from store", ex);
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
        public Store() : base() { }
        /// <summary>
        /// Construct Store from Examine item
        /// </summary>
        /// <param name="item"></param>
        public Store(ISearchResult item) : base(item)
        {
            var uCtx = Current.Factory.GetInstance<UmbracoContext>();
            var storeDomainCache = Current.Factory.GetInstance<IBaseCache<IDomain>>();

            if (int.TryParse(item.Values["storeRootNode"], out int tempStoreRootNode))
            {
                StoreRootNode = tempStoreRootNode;
            }
            else
            {
                var srn = Udi.Parse(item.Values["storeRootNode"]);
                var umbracoHelper = Current.Factory.GetInstance<UmbracoHelper>();
                var rootNode = umbracoHelper.Content(srn);
                StoreRootNode = rootNode.Id;
            }
            Url = uCtx.UrlProvider.GetUrl(StoreRootNode);

            if (storeDomainCache.Cache.Any(x => x.Value.RootContentId == StoreRootNode))
            {
                Domains = storeDomainCache.Cache.Where(x => x.Value.RootContentId == StoreRootNode)
                    .Select(x => new Domain(x.Value));
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

        /// <summary>
        /// Construct Store from umbraco publish event
        /// </summary>
        /// <param name="item"></param>
        public Store(IContent item) : base(item)
        {
            if (int.TryParse(item.GetValue<string>("storeRootNode"), out int tempStoreRootNode))
            {
                StoreRootNode = tempStoreRootNode;
            }
            else
            {
                var srn = Udi.Parse(item.GetValue<string>("storeRootNode"));
                var umbracoHelper = Current.Factory.GetInstance<UmbracoHelper>();
                var rootNode = umbracoHelper.Content(srn);
                StoreRootNode = rootNode.Id;
            }

            var storeDomainCache = Current.Factory.GetInstance<IBaseCache<IDomain>>();
            var uCtx = Current.Factory.GetInstance<UmbracoContext>();

            if (storeDomainCache.Cache.Any(x => x.Value.RootContentId == StoreRootNode))
            {
                Domains = storeDomainCache.Cache
                    .Where(x => x.Value.RootContentId == StoreRootNode)
                    .Select(x => new Domain(x.Value));
            }
            else
            {
                //TODO If not culture/domain is set then add default
                //if (uCtx.HttpContext != null)
                //{

                //    Domains = Enumerable.Repeat(new Domain(uCtx.HttpContext.Request.Url?.Host, StoreRootNode), 1);
                //}
            }

            Url = uCtx.UrlProvider.GetUrl(StoreRootNode);
        }
    }
}
