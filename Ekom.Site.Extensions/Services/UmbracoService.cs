using Ekom.API;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Composing;

namespace Ekom.Site.Extensions.Services
{
    public class UmbracoService
    {
        public static IPublishedContent GetRootNode(IPublishedContent currentNode)
        {
            // Add Cache

            var root = currentNode.Ancestor(1);

            if (root.ContentType.Alias == "ekom")
            {
                var local = Store.Instance.GetStore();

                if (local != null)
                {
                    var storeNode = Current.UmbracoHelper.Content(local.StoreRootNode);

                    if (storeNode != null)
                    {
                        return storeNode;
                    }
                }
            }

            return root;
        }
    }
}
