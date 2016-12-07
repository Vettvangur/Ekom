using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using uWebshop.Cache;
using uWebshop.Services;

namespace uWebshop.Models
{
    public class PaymentProvider
    {
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
            try
            {
                var pathField = item.Fields["path"];
                var examineItemsFromPath = ExamineService.GetAllCatalogItemsFromPath(pathField);

                if (!CatalogService.IsItemDisabled(examineItemsFromPath, store))
                {
                    Id = item.Id;
                    Store = store;

                    Title = ExamineService.GetProperty(item, "title", store.Alias);
                    Alias = item.Fields["nodeName"];

                    SortOrder = Convert.ToInt32(item.Fields["sortOrder"]);

                    // Zones
                    foreach (var zone in item.Fields["zone"].Split(','))
                    {
                        var zoneObj = ZoneCache.Instance._cache.FirstOrDefault
                            (x => x.Value.Id.ToString() == zone).Value;

                        if (zone != null) Zones.Add(zoneObj);
                    }

                    // Success / Error / Cancel nodes
                    var examineSuccessNode = ExamineService.GetProperty
                                                (item, "successNode", store.Alias);

                    if (!string.IsNullOrEmpty(examineSuccessNode))
                    {
                        SuccessNodeId = int.Parse(examineSuccessNode);
                    }

                    var examineErrorNode = ExamineService.GetProperty
                                                (item, "errorNode", store.Alias);

                    if (!string.IsNullOrEmpty(examineErrorNode))
                    {
                        ErrorNodeId = int.Parse(examineErrorNode);
                    }

                    var examineCancelNode = ExamineService.GetProperty
                                                (item, "cancelNode", store.Alias);

                    if (!string.IsNullOrEmpty(examineCancelNode))
                    {
                        CancelNodeId = int.Parse(examineCancelNode);
                    }
                }
                else
                {
                    throw new Exception("Error, PaymentProvider disabled. Node id: " + item.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error creating product item from Examine. Node id: " + item.Id, ex);
                throw;
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
