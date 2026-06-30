using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace Ekom.Site.U17;

public static class PublishedContentExtensions
{
    public static string GetTitle(this IPublishedContent node)
        => node.HasValue("pageTitle") ? node.Value<string>("pageTitle") ?? node.Name : node.Name;

    public static string GetNavigationTitle(this IPublishedContent node)
        => node.HasValue("navigationTitle") ? node.Value<string>("navigationTitle") ?? node.GetTitle() : node.GetTitle();

    public static IPublishedContent GetRootNode(this IPublishedContent node)
        => node.Root();
}
