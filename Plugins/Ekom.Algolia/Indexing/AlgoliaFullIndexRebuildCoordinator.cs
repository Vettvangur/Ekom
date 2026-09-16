using Microsoft.Extensions.Logging;

namespace Ekom.Algolia.Indexing;

public interface IAlgoliaFullIndexRebuildCoordinator
{
    bool TryStart();
    bool TryStartStore(string storeAlias);
}

public sealed class AlgoliaFullIndexRebuildCoordinator : IAlgoliaFullIndexRebuildCoordinator
{
    private readonly IAlgoliaProductIndexService _productIndexService;
    private readonly IAlgoliaCategoryIndexService _categoryIndexService;
    private readonly IAlgoliaContentIndexService _contentIndexService;
    private readonly ILogger<AlgoliaFullIndexRebuildCoordinator> _logger;
    private readonly HashSet<string> _activeStoreRebuilds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private bool _isFullRebuildRunning;

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
        lock (_sync)
        {
            if (_isFullRebuildRunning || _activeStoreRebuilds.Count > 0)
                return false;

            _isFullRebuildRunning = true;
        }

        _ = Task.Run(RunFullAsync, CancellationToken.None);
        return true;
    }

    public bool TryStartStore(string storeAlias)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
            return false;

        storeAlias = storeAlias.Trim();

        lock (_sync)
        {
            if (_isFullRebuildRunning || !_activeStoreRebuilds.Add(storeAlias))
                return false;
        }

        _ = Task.Run(() => RunStoreAsync(storeAlias), CancellationToken.None);
        return true;
    }

    private async Task RunFullAsync()
    {
        try
        {
            await RunStagesAsync(
                ("products", () => _productIndexService.RebuildAllAndWaitAsync()),
                ("categories", () => _categoryIndexService.RebuildAllAndWaitAsync()),
                ("content", () => _contentIndexService.RebuildAndWaitAsync())).ConfigureAwait(false);

            _logger.LogInformation("Algolia manual full reindex completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Algolia manual full reindex failed.");
        }
        finally
        {
            lock (_sync)
                _isFullRebuildRunning = false;
        }
    }

    private async Task RunStoreAsync(string storeAlias)
    {
        try
        {
            await RunStagesAsync(
                ("products", () => _productIndexService.RebuildStoreAndWaitAsync(storeAlias)),
                ("categories", () => _categoryIndexService.RebuildStoreAndWaitAsync(storeAlias))).ConfigureAwait(false);

            _logger.LogInformation("Algolia manual store reindex completed for store {Store}.", storeAlias);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Algolia manual store reindex failed for store {Store}.", storeAlias);
        }
        finally
        {
            lock (_sync)
                _activeStoreRebuilds.Remove(storeAlias);
        }
    }

    private async Task RunStagesAsync(params (string Name, Func<Task> Run)[] stages)
    {
        var failures = new List<Exception>();

        foreach (var stage in stages)
        {
            try
            {
                await stage.Run().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add(new InvalidOperationException($"Algolia {stage.Name} rebuild stage failed.", ex));
                _logger.LogWarning(
                    "Algolia {Stage} rebuild stage failed; continuing with the remaining stages.",
                    stage.Name);
            }
        }

        if (failures.Count > 0)
            throw new AggregateException("One or more Algolia rebuild stages failed.", failures);
    }
}
