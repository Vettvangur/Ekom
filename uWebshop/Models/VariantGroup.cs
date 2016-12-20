using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Cache;
using uWebshop.Services;

namespace uWebshop.Models
{
    public class VariantGroup
    {
        public int ProductId;

        public VariantGroup(int id)
        {
            this.ProductId = id;
        }
        public int Id { get; set; }
        public string Title { get; set; }
        public Store Store { get; set; }
        public int SortOrder { get; set; }
        public IEnumerable<Variant> Variants {
            get
            {
                return VariantCache.Instance._cache.Where(x => x.Value.VariantGroupId == Id && x.Value.Store.Alias == Store.Alias).Select(x => x.Value);
            }
        }

        public VariantGroup(): base() { }
        public VariantGroup(SearchResult item, Store store)
        {
            try
            {
                int productId = Convert.ToInt32(item.Fields["parentID"]);

                Id        = item.Id;
                Title     = ExamineService.GetProperty(item, "title", store.Alias);
                Store     = store;
                SortOrder = Convert.ToInt32(item.Fields["sortOrder"]);
            }
            catch (Exception ex)
            {
                Log.Error("Error on creating variant group item from Examine. Node id: " + item.Id, ex);
                throw;
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
