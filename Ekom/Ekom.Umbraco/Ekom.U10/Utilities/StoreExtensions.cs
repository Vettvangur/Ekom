using Ekom.Models;
using Ekom.Umb.Services;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Ekom.Utilities
{
    public static class StoreExtensions
    {
        public static IPublishedContent GetRootNode(this IStore store)
        {
            using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var r = scope.ServiceProvider.GetRequiredService<NodeService>();

            return r.GetNodeById(store.StoreRootNodeId);
        }
    }
}
