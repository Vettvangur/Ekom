using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Services
{
    interface IUrlService
    {
        IEnumerable<string> BuildCategoryUrls(IEnumerable<UmbracoContent> items, IStore store);
        IEnumerable<string> BuildCategoryUrls(string slug, List<string> hierarchy, IStore store);
        IEnumerable<string> BuildProductUrls(string slug, IEnumerable<ICategory> categories, IStore store, int nodeId);
        string GetDomainPrefix(string url);
        string GetNodeEntityUrl(INodeEntityWithUrl node);
    }
}
