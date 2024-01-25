using Ekom.Models;
using Ekom.Models.Umbraco;

namespace Ekom.Services
{
    interface IUrlService
    {
        List<UmbracoUrl> BuildCategoryUrls(IEnumerable<UmbracoContent> items, IStore store);
        IEnumerable<string> BuildCategoryUrls(string slug, List<string> hierarchy, IStore store);
        [Obsolete]
        IEnumerable<string> BuildProductUrls(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId);
        List<UmbracoUrl> BuildProductUrlsWithContext(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId);
        string GetNodeEntityUrl(INodeEntityWithUrl node);
    }
}
