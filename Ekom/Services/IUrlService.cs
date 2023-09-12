using Ekom.Models;

namespace Ekom.Services
{
    interface IUrlService
    {
        Dictionary<string,string> BuildCategoryUrls(IEnumerable<UmbracoContent> items, IStore store);
        IEnumerable<string> BuildCategoryUrls(string slug, List<string> hierarchy, IStore store);
        [Obsolete]
        IEnumerable<string> BuildProductUrls(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId);
        Dictionary<string,string> BuildProductUrlsWithContext(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId);
        string GetNodeEntityUrl(INodeEntityWithUrl node);
    }
}
