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
}
