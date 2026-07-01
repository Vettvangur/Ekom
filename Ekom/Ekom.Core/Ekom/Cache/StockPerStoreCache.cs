using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ekom.Cache;

class StockPerStoreCache : PerStoreCache<StockData>
{
    private readonly StockRepository _stockRepo;

    public StockPerStoreCache(
        Configuration config,
        ILogger<IPerStoreCache<StockData>> logger,
        IBaseCache<IStore> storeCache,
        StockRepository stockRepo,
        IServiceProvider serviceProvider
    ) : base(config, logger, storeCache, null, serviceProvider)
    {
        _stockRepo = stockRepo;
    }

    public override ConcurrentDictionary<string, ConcurrentDictionary<Guid, StockData>> Cache
    {
        get
        {
            if (_config.PerStoreStock)
                return base.Cache;

            throw new StockException(
                "PerStoreStock configuration set to disabled, please configure PerStoreStock before accessing the cache."
            );
        }
    }

    // Not used by this cache (we fill from repository, not Umbraco)
    public override string NodeAlias { get; } = "";


    public override void FillCache()
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting to fill stock per store cache...");

        int count = 0;

        var allStock = _stockRepo.GetAllStockAsync().GetAwaiter().GetResult();

        // Expect format: "{storeAlias}_{guid}"
        foreach (var stock in allStock)
        {
            if (stock?.UniqueId == null)
                continue;

            int underscore = stock.UniqueId.IndexOf('_');
            if (underscore <= 0 || underscore >= stock.UniqueId.Length - 1)
                continue;

            var storeAlias = stock.UniqueId[..underscore];
            var keyPart = stock.UniqueId[(underscore + 1)..];

            if (!Guid.TryParse(keyPart, out var key))
                continue;

            // Ensure store cache exists
            var storeCache = Cache[storeAlias] = Cache.TryGetValue(storeAlias, out var existing)
                ? existing
                : new ConcurrentDictionary<Guid, StockData>();

            storeCache[key] = stock;

            count++;
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Finished filling stock per store cache with {Count} items. Time it took to fill: {Elapsed}",
            count,
            stopwatch.Elapsed
        );
    }
}
