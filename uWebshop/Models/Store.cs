using Examine;
using log4net;
using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Umbraco.Core;
using Umbraco.Core.Models;
using uWebshop.App_Start;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Utilities;

namespace uWebshop.Models
{
    public class Store : IStore
    {
        private IBaseCache<IDomain> _storeDomainCache
        {
            get
            {
                return UnityConfig.GetConfiguredContainer().Resolve<IBaseCache<IDomain>>();
            }
        }

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string ContentTypeAlias { get; set; }
        public string Alias { get; set; }
        public int StoreRootNode { get; set; }
        public int Level { get; set; }
        public IEnumerable<IDomain> Domains { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public int SortOrder { get; set; }
        public string Path { get; set; }
        public decimal Vat { get; set; }
        public CultureInfo Culture { get; set; }
        public bool VatIncludedInPrice { get; set; }
        public string OrderNumberTemplate { get; set; }
        public string OrderNumberPrefix { get; set; }
        public Store() : base() { }
        public Store(SearchResult item)
        {
            var key = item.Fields["key"];

            var _key = new Guid();

            if (!Guid.TryParse(key, out _key))
            {
                throw new Exception("No key present for store.");
            }

            var contentTypeAlias = item.Fields["nodeTypeAlias"];

            Id = item.Id;
            Key = _key;
            ContentTypeAlias = contentTypeAlias;
            Alias = item.Fields["nodeName"];

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

            //StoreRootNode = Convert.ToInt32(item.Fields["storeRootNode"]);
            Level = Convert.ToInt32(item.Fields["level"]);

            Domains = _storeDomainCache.Cache.Where(x => x.Value.RootContentId == StoreRootNode)
                                            .Select(x => x.Value);

            SortOrder = Convert.ToInt32(item.Fields["sortOrder"]);
            CreateDate = ExamineHelper.ConvertToDatetime(item.Fields["createDate"]);
            UpdateDate = ExamineHelper.ConvertToDatetime(item.Fields["updateDate"]);

            var _culture = item.Fields["culture"];

            Culture = new CultureInfo(_culture);

            Vat = string.IsNullOrEmpty(item.Fields["vat"]) ? 0 : Convert.ToDecimal(item.Fields["vat"]);
            VatIncludedInPrice = item.Fields["vatIncludedInPrice"].ConvertToBool();

            item.Fields.TryGetValue("orderNumberTemplate", out string orderNumberTemplate);
            OrderNumberTemplate = orderNumberTemplate;

            item.Fields.TryGetValue("orderNumberPrefix", out string orderNumberPrefix);
            OrderNumberPrefix = orderNumberPrefix;
        }

        public Store(IContent item)
        {
            Id = item.Id;
            Key = item.Key;
            Alias = item.Name;
            ContentTypeAlias = item.ContentType.Alias;

            StoreRootNode = item.GetValue<int>("storeRootNode");
            Level = item.Level;

            Domains = _storeDomainCache.Cache
                                      .Where(x => x.Value.RootContentId == StoreRootNode)
                                      .Select(x => x.Value);

            SortOrder = item.SortOrder;
            CreateDate = item.CreateDate;
            UpdateDate = item.UpdateDate;

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
