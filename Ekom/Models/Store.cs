﻿using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Utilities;
using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Umbraco.Core;
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
        public virtual IEnumerable<IDomain> Domains { get; }
        public virtual bool VatIncludedInPrice => Properties["vatIncludedInPrice"].ConvertToBool();
        public virtual string OrderNumberTemplate => Properties.GetPropertyValue("orderNumberTemplate");
        public virtual string OrderNumberPrefix => Properties.GetPropertyValue("orderNumberPrefix");
        public virtual string Url { get; }
        public virtual CultureInfo Culture => new CultureInfo(Properties["culture"]);
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
        public Store(SearchResult item) : base(item)
        {
            var uCtx = Configuration.container.GetInstance<UmbracoContext>();
            var storeDomainCache = Configuration.container.GetInstance<IBaseCache<IDomain>>();

            if (int.TryParse(item.Fields["storeRootNode"], out int tempStoreRootNode))
            {
                StoreRootNode = tempStoreRootNode;
            }
            else
            {
                var srn = Udi.Parse(item.Fields["storeRootNode"]);
                var umbracoHelper = Configuration.container.GetInstance<UmbracoHelper>();
                var rootNode = umbracoHelper.TypedContent(srn);
                StoreRootNode = rootNode.Id;
            }
            Url = uCtx.UrlProvider.GetUrl(StoreRootNode);

            Domains = storeDomainCache.Cache.Where(x => x.Value.RootContentId == StoreRootNode)
                                            .Select(x => x.Value);
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
                var umbracoHelper = Configuration.container.GetInstance<UmbracoHelper>();
                var rootNode = umbracoHelper.TypedContent(srn);
                StoreRootNode = rootNode.Id;
            }

            var storeDomainCache = Configuration.container.GetInstance<IBaseCache<IDomain>>();
            Domains = storeDomainCache.Cache
                                      .Where(x => x.Value.RootContentId == StoreRootNode)
                                      .Select(x => x.Value);

            var uCtx = Configuration.container.GetInstance<UmbracoContext>();
            Url = uCtx.UrlProvider.GetUrl(StoreRootNode);
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
