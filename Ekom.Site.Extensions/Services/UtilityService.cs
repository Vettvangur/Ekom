using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Composing;

namespace Ekom.Site.Extensions.Services
{
    public class UtilityService
    {
        public static string GetTitle(IPublishedContent node)
        {
            var title = node.HasValue("pageTitle") ? node.Value<string>("pageTitle") : node.Name;

            return title;
        }
        public static string GetPreValue(string id)
        {

            var value = string.Empty;

            if (!string.IsNullOrEmpty(id))
            {

                var parse = int.TryParse(id, out int preId);

                if (parse)
                {
                    var dt = Current.Services.DataTypeService.GetDataType(preId);

                    value = dt.ConfigurationAs<string>();
                }

            }

            return value;

        }

    }
}
