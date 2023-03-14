using Ekom.Models;

namespace Ekom.Services
{
    interface IUrlService
    {
        IEnumerable<string> BuildCategoryUrls(IEnumerable<UmbracoContent> items, IStore store);
        IEnumerable<string> BuildCategoryUrls(string slug, List<string> hierarchy, IStore store);
        IEnumerable<string> BuildProductUrls(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId);
        string GetDomainPrefix(string url);
        string GetNodeEntityUrl(INodeEntityWithUrl node);
    }
}
