using Ekom.API;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace Ekom.Site.Extensions.Services
{
    public class UmbracoService
    {
        public static IPublishedContent GetRootNode(IPublishedContent currentNode)
        {
            // Add Cache

            var root = currentNode.Site();

            if (root.DocumentTypeAlias == "ekom")
            {
                var local = Store.Current.GetStore();

                if (local != null)
                {
                    var helper = new UmbracoHelper(UmbracoContext.Current);

                    var storeNode = helper.TypedContent(local.StoreRootNode);

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
