using Examine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Models;

namespace uWebshop.Services
{
    public static class CatalogService
    {
        public static bool IsItemDisabled(IEnumerable<SearchResult> items, Store store)
        {

            foreach (var item in items)
            {
                if (item != null)
                {
                    var disableField = ExamineService.GetProperty(item, "disable", store.Alias);

                    if (disableField == "1")
                    {
                        return true;
                    }
                } else
                {
                    return true;
                }

            }

            return false;
        }
    }
}
