using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ekom.Cache;

class StockCache : BaseCache<StockData>
{
    private readonly StockRepository _stockRepo;

    public StockCache(
        Configuration config,
        ILogger<BaseCache<StockData>> logger,
        StockRepository stockRepo,
        IServiceProvider serviceProvider
    ) : base(config, logger, null, serviceProvider)
    {
        _stockRepo = stockRepo;
    }

    public override ConcurrentDictionary<Guid, StockData> Cache
    {
        get
        {
            if (!_config.PerStoreStock)
                return base.Cache;

            throw new StockException("PerStoreStock configuration enabled, please disable PerStoreStock before accessing this cache.");
        }
    }

    public override string NodeAlias { get; } = "";

    public override void FillCache()
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting to fill stock cache...");

        var allStock = _stockRepo.GetAllStockAsync().GetAwaiter().GetResult();

        foreach (var stock in allStock)
        {
            if (stock?.UniqueId == null || stock.UniqueId.Length != 36)
                continue;

            if (!Guid.TryParse(stock.UniqueId, out var key))
                continue;

            AddOrReplaceFromCache(key, stock);
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Finished filling Stock cache with {Count} items. Time it took to fill: {Elapsed}",
            allStock.Count,
            stopwatch.Elapsed);
    }
    
}
