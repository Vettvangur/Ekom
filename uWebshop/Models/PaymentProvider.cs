using Examine;
using log4net;
using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Umbraco.Core.Models;
using uWebshop.App_Start;
using uWebshop.Cache;
using uWebshop.Helpers;

namespace uWebshop.Models
{
    public class PaymentProvider
    {
        private IBaseCache<Zone> _zoneCache
        {
            get
            {
                return UnityConfig.GetConfiguredContainer().Resolve<IBaseCache<Zone>>();
            }
        }

        public int Id { get; set; }
        public string Title { get; set; }
        public string Alias { get; set; }
        public Store Store { get; set; }
        public int SortOrder { get; set; }
        public List<Zone> Zones { get; set; }
        public int SuccessNodeId { get; set; }
        public int ErrorNodeId { get; set; }
        public int CancelNodeId { get; set; }

        public PaymentProvider() : base() { }
        public PaymentProvider(SearchResult item, Store store)
        {
            Id = item.Id;
            Store = store;

            Title = item.GetStoreProperty("title", store.Alias);
            Alias = item.Fields["nodeName"];

            SortOrder = Convert.ToInt32(item.Fields["sortOrder"]);

            // Zones
            foreach (var zone in item.Fields["zone"].Split(','))
            {
                var zoneObj
                    = _zoneCache.Cache.FirstOrDefault(x => x.Value.Id.ToString() == zone).Value;

                if (zone != null) Zones.Add(zoneObj);
            }

            // Success / Error / Cancel nodes
            var examineSuccessNode = item.GetStoreProperty("successNode", store.Alias);

            if (!string.IsNullOrEmpty(examineSuccessNode))
            {
                SuccessNodeId = int.Parse(examineSuccessNode);
            }

            var examineErrorNode = item.GetStoreProperty("errorNode", store.Alias);

            if (!string.IsNullOrEmpty(examineErrorNode))
            {
                ErrorNodeId = int.Parse(examineErrorNode);
            }

            var examineCancelNode = item.GetStoreProperty("cancelNode", store.Alias);

            if (!string.IsNullOrEmpty(examineCancelNode))
            {
                CancelNodeId = int.Parse(examineCancelNode);
            }
        }
        public PaymentProvider(IContent item, Store store)
        {
            Id = item.Id;
            Store = store;

            Title = item.GetStoreProperty("title", store.Alias);
            Alias = item.Name;

            SortOrder = item.SortOrder;

            // Zones
            foreach (var zone in item.GetValue<string>("zone").Split(','))
            {
                var zoneObj
                    = _zoneCache.Cache.FirstOrDefault(x => x.Value.Id.ToString() == zone).Value;

                if (zone != null) Zones.Add(zoneObj);
            }

            // Success / Error / Cancel nodes
            var examineSuccessNode = item.GetStoreProperty("successNode", store.Alias);

            if (!string.IsNullOrEmpty(examineSuccessNode))
            {
                SuccessNodeId = int.Parse(examineSuccessNode);
            }

            var examineErrorNode = item.GetStoreProperty("errorNode", store.Alias);

            if (!string.IsNullOrEmpty(examineErrorNode))
            {
                ErrorNodeId = int.Parse(examineErrorNode);
            }

            var examineCancelNode = item.GetStoreProperty("cancelNode", store.Alias);

            if (!string.IsNullOrEmpty(examineCancelNode))
            {
                CancelNodeId = int.Parse(examineCancelNode);
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
