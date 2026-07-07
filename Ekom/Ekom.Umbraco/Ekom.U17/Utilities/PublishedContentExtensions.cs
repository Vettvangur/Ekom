using Ekom.API;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Utilities;

public static class PublishedContentExtensions
{
    public static IPublishedContent GetSite(this IPublishedContent node)
    {
        var root = node.Root();

        if (!root.IsDocumentType("ekom"))
        {
            return root;
        }

        var store = Store.Instance.GetStore();

        if (store == null || store.StoreRootNodeId <= 0)
        {
            return root;
        }

        var umbracoContextFactory = Configuration.Resolver.GetService<IUmbracoContextFactory>();

        if (umbracoContextFactory == null)
        {
            return root;
        }

        using var cref = umbracoContextFactory.EnsureUmbracoContext();
        var storeRootNode = cref.UmbracoContext.Content?.GetById(store.StoreRootNodeId);

        return storeRootNode ?? root;
    }
}
