using Ekom.Models;

namespace Ekom.Services;
public interface INodeService
{
    IEnumerable<UmbracoContent> NodesByTypes(string contentTypeAlias);
    IEnumerable<UmbracoContent> NodesByTypesFaster(string contentTypeAlias);
    IEnumerable<UmbracoContent> NodeAncestors(string t);
    IEnumerable<UmbracoContent> NodeCatalogAncestors(string t);
    IEnumerable<UmbracoContent> NodeChildren(string t);
    bool IsItemUnpublished(UmbracoContent content);
    UmbracoContent? NodeById(Guid t, bool preview = false);
    UmbracoContent? NodeById(int t, bool preview = false);
    UmbracoContent? NodeById(string t, bool preview = false);
    UmbracoContent MediaById(Guid t);
    UmbracoContent MediaById(int t);
    UmbracoContent MediaById(string t);
    string GetUrl(string t, string url = null);
    IEnumerable<UmbracoContent> GetAllCatalogAncestors(UmbracoContent item);
}
