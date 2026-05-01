using Ekom.Models;
using Ekom.Models.Umbraco;
using Ekom.Services;

namespace Ekom.Umb.Services;

internal sealed class UrlService : IUrlService
{
    public List<UmbracoUrl> BuildCategoryUrls(IEnumerable<UmbracoContent> items, IStore store) => new();

    public IEnumerable<string> BuildCategoryUrls(string slug, List<string> hierarchy, IStore store) => Array.Empty<string>();

    public IEnumerable<string> BuildProductUrls(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId) => Array.Empty<string>();

    public List<UmbracoUrl> BuildProductUrlsWithContext(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId) => new();

    public string? GetNodeEntityUrl(INodeEntityWithUrl node) => null;
}
