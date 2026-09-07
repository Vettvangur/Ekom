using Microsoft.Extensions.Logging;

namespace Ekom.Algolia.Indexing;

public interface IAlgoliaFullIndexRebuildCoordinator
{
    bool TryStart();
}

public sealed class AlgoliaFullIndexRebuildCoordinator : IAlgoliaFullIndexRebuildCoordinator
{
    private readonly IAlgoliaProductIndexService _productIndexService;
    private readonly IAlgoliaCategoryIndexService _categoryIndexService;
    private readonly IAlgoliaContentIndexService _contentIndexService;
    private readonly ILogger<AlgoliaFullIndexRebuildCoordinator> _logger;
    private int _isRunning;

    public AlgoliaFullIndexRebuildCoordinator(
        IAlgoliaProductIndexService productIndexService,
        IAlgoliaCategoryIndexService categoryIndexService,
        IAlgoliaContentIndexService contentIndexService,
        ILogger<AlgoliaFullIndexRebuildCoordinator> logger)
    {
        _productIndexService = productIndexService;
        _categoryIndexService = categoryIndexService;
        _contentIndexService = contentIndexService;
        _logger = logger;
    }

    public bool TryStart()
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            return false;
        }

        _ = Task.Run(RunAsync, CancellationToken.None);
        return true;
    }

    private async Task RunAsync()
    {
        try
        {
            await Task.WhenAll(
                _productIndexService.RebuildAllAndWaitAsync(),
                _categoryIndexService.RebuildAllAndWaitAsync(),
                _contentIndexService.RebuildAndWaitAsync()).ConfigureAwait(false);

            _logger.LogInformation("Algolia manual full reindex completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Algolia manual full reindex failed.");
        }
        finally
        {
            Volatile.Write(ref _isRunning, 0);
        }
    }
}
