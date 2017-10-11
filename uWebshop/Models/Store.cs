using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Core.Models;
using uWebshop.Cache;
using uWebshop.Interfaces;
using uWebshop.Utilities;

namespace uWebshop.Models
{
    /// <summary>
    /// uWebshop Store, used to f.x. have seperate products and entities per store.
    /// </summary>
    public class Store : NodeEntity, IStore
    {
        private IBaseCache<IDomain> _storeDomainCache
        {
            get
            {
                return Configuration.container.GetService<IBaseCache<IDomain>>();
            }
        }

        /// <summary>
        /// Usually a two letter code, f.x. EU/IS/DK
        /// </summary>
        public string Alias => Properties["nodeName"];
        public int StoreRootNode { get; set; }
        public IEnumerable<IDomain> Domains { get; set; }
        public decimal Vat { get; set; }
        public CultureInfo Culture { get; set; }
        public bool VatIncludedInPrice { get; set; }
        public string OrderNumberTemplate { get; set; }
        public string OrderNumberPrefix { get; set; }
        /// <summary>
        /// Used by uWebshop extensions
        /// </summary>
        public Store() : base() { }
        /// <summary>
        /// Construct Store from Examine item
        /// </summary>
        /// <param name="item"></param>
        public Store(SearchResult item) : base(item)
        {
            if (int.TryParse(item.Fields["storeRootNode"], out int tempStoreRootNode))
            {
                StoreRootNode = tempStoreRootNode;
            }
            else
            {
                var srn = Udi.Parse(item.Fields["storeRootNode"]);
                var umbracoHelper = new Umbraco.Web.UmbracoHelper(Umbraco.Web.UmbracoContext.Current);
                var rootNode = umbracoHelper.TypedContent(srn);
                StoreRootNode = rootNode.Id;
            }

            Domains = _storeDomainCache.Cache.Where(x => x.Value.RootContentId == StoreRootNode)
                                            .Select(x => x.Value);

            var _culture = item.Fields["culture"];

            Culture = new CultureInfo(_culture);

            Vat = string.IsNullOrEmpty(item.Fields["vat"]) ? 0 : Convert.ToDecimal(item.Fields["vat"]);
            VatIncludedInPrice = item.Fields["vatIncludedInPrice"].ConvertToBool();

            item.Fields.TryGetValue("orderNumberTemplate", out string orderNumberTemplate);
            OrderNumberTemplate = orderNumberTemplate;

            item.Fields.TryGetValue("orderNumberPrefix", out string orderNumberPrefix);
            OrderNumberPrefix = orderNumberPrefix;
        }

        /// <summary>
        /// Construct Store from umbraco publish event
        /// </summary>
        /// <param name="item"></param>
        public Store(IContent item) : base(item)
        {
            StoreRootNode = item.GetValue<int>("storeRootNode");

            Domains = _storeDomainCache.Cache
                                      .Where(x => x.Value.RootContentId == StoreRootNode)
                                      .Select(x => x.Value);

            var vat = item.GetValue<string>("vat");

            Vat = string.IsNullOrEmpty(vat) ? 0 : Convert.ToDecimal(vat);

            var _culture = item.GetValue<string>("culture");

            Culture = new CultureInfo(_culture);

            VatIncludedInPrice = item.GetValue<bool>("vatIncludedInPrice");

            if (item.HasProperty("orderNumberTemplate"))
            {
                OrderNumberTemplate = item.GetValue<string>("orderNumberTemplate");
            }
            if (item.HasProperty("orderNumberPrefix"))
            {
                OrderNumberPrefix = item.GetValue<string>("orderNumberPrefix");
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
