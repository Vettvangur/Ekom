using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Ekom.Cache;

class DiscountCache : PerStoreCache<IDiscount>
{
    public override string NodeAlias { get; } = "ekmOrderDiscount";

    /// <summary>
    /// ctor
    /// </summary>
    public DiscountCache(
        Configuration config,
        ILogger<IPerStoreCache<IDiscount>> logger,
        IBaseCache<IStore> storeCache,
        IPerStoreFactory<IDiscount> perStoreFactory,
        IServiceProvider serviceProvider)
        : base(config, logger, storeCache, perStoreFactory, serviceProvider)
    {
    }

    /// <summary>
    /// Fill the given stores cache of TItem
    /// </summary>
    /// <param name="store">The current store being filled of TItem</param>
    /// <param name="results">Examine search results</param>
    /// <returns>Count of items added</returns>
    protected override int FillStoreCache(IStore store, List<UmbracoContent> results, string nodeAlias)
    {
        int count = 0;

        ConcurrentDictionary<Guid, IDiscount> curStoreCache
            = Cache[store.Alias] = new ConcurrentDictionary<Guid, IDiscount>();

        foreach (UmbracoContent r in results)
        {
            try
            {
                IEnumerable<UmbracoContent> ancestors = nodeService.NodeAncestors(r.Id.ToString());

                // Traverse up parent nodes, checking disabled status and published status
                if (r.IsItemDisabled(store, ancestors)) continue;

                IDiscount item = _objFac?.Create(r, store) ?? new Discount(r, store);

                count++;

                curStoreCache[r.Key] = item;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error on adding item with id: {Id} from Examine in Store: {Store}",
                    r.Id,
                    store.Alias
                );
            }
        }

        return count;
    }
}
