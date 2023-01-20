using Ekom.Models;
using System;
using System.Collections.Generic;

namespace Ekom.Services
{
    interface INodeService
    {
        IEnumerable<UmbracoContent> NodesByTypes(string contentTypeAlias);
        IEnumerable<UmbracoContent> NodeAncestors(string t);
        IEnumerable<UmbracoContent> NodeCatalogAncestors(string t);
        IEnumerable<UmbracoContent> NodeChildren(string t);
        bool IsItemUnpublished(UmbracoContent content);
        UmbracoContent NodeById(Guid t);
        UmbracoContent NodeById(int t);
        UmbracoContent NodeById(string t);
        UmbracoContent MediaById(Guid t);
        UmbracoContent MediaById(int t);
        UmbracoContent MediaById(string t);
        string GetUrl(string t, string url = null);
        IEnumerable<UmbracoContent> GetAllCatalogAncestors(UmbracoContent item);
    }
}
