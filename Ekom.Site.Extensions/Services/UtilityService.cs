using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Ekom.Site.Extensions.Services
{
    public class UtilityService
    {
        public static string GetTitle(IPublishedContent node)
        {
            var title = node.HasValue("pageTitle") ? node.GetPropertyValue<string>("pageTitle") : node.Name;

            return title;
        }
        public static string GetPreValue(string id)
        {

            var value = string.Empty;

            if (!string.IsNullOrEmpty(id))
            {

                int preId = 0;
                var parse = Int32.TryParse(id, out preId);

                if (parse)
                {
                    var helper = new UmbracoHelper(UmbracoContext.Current);

                    value = helper.GetPreValueAsString(preId);
                }

            }

            return value;

        }

    }
}
