using Umbraco.Cms.Core.Models.PublishedContent;

namespace Ekom.Site;

public static class PublishedContentExtensions
{
    public static string GetTitle(this IPublishedContent node)
    {
        return node.HasValue("pageTitle") ? node.Value<string>("pageTitle") : node.Name;
    }

    public static string GetNavigationTitle(this IPublishedContent node)
    {
        return node.HasValue("navigationTitle") ? node.Value<string>("navigationTitle") : GetTitle(node);
    }
    public static IPublishedContent GetRootNode(this IPublishedContent node)
    {
        var root = node.Root();

        if (root != null && root.ContentType.Alias == "ekom")
        {
            var store = Ekom.API.Store.Instance.GetStore();

            if (store != null)
            {
                var _umbService = GlobalSettings.HttpContextAccessor.HttpContext?.RequestServices.GetService<UmbracoService>();

                if (_umbService == null)
                {
                    return root;
                }
                
                var storeNode = _umbService.GetNodeById(store.Id);

                if (storeNode != null)
                {
                    var storeRootNode = storeNode.Value<IPublishedContent>("storeRootNode");

                    if (storeRootNode != null)
                    {
                        return storeRootNode;
                    }
                }
            }
        }

        return root;
    }
}
