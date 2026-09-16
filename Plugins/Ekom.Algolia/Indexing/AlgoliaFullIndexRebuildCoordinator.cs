using Microsoft.Extensions.Logging;
using System.Diagnostics;

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
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Algolia full rebuild started.");

        try
        {
            await RunStagesAsync(
                "Full",
                storeAlias: null,
                ("products", () => _productIndexService.RebuildAllAndWaitAsync()),
                ("categories", () => _categoryIndexService.RebuildAllAndWaitAsync()),
                ("content", () => _contentIndexService.RebuildAndWaitAsync())).ConfigureAwait(false);

            _logger.LogInformation(
                "Algolia full rebuild completed. DurationMilliseconds={DurationMilliseconds}",
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Algolia full rebuild canceled. DurationMilliseconds={DurationMilliseconds}",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Algolia full rebuild failed. DurationMilliseconds={DurationMilliseconds}",
                stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            lock (_sync)
                _isFullRebuildRunning = false;
        }
    }

    private async Task RunStoreAsync(string storeAlias)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Algolia store rebuild started. Store={Store}", storeAlias);

        try
        {
            await RunStagesAsync(
                "Store",
                storeAlias,
                ("products", () => _productIndexService.RebuildStoreAndWaitAsync(storeAlias)),
                ("categories", () => _categoryIndexService.RebuildStoreAndWaitAsync(storeAlias))).ConfigureAwait(false);

            _logger.LogInformation(
                "Algolia store rebuild completed. Store={Store} DurationMilliseconds={DurationMilliseconds}",
                storeAlias,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Algolia store rebuild canceled. Store={Store} DurationMilliseconds={DurationMilliseconds}",
                storeAlias,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Algolia store rebuild failed. Store={Store} DurationMilliseconds={DurationMilliseconds}",
                storeAlias,
                stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            lock (_sync)
                _activeStoreRebuilds.Remove(storeAlias);
        }
    }

    private async Task RunStagesAsync(
        string scope,
        string? storeAlias,
        params (string Name, Func<Task> Run)[] stages)
    {
        var failures = new List<Exception>();

        foreach (var stage in stages)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation(
                "Algolia rebuild stage started. Stage={Stage} Scope={Scope} Store={Store}",
                stage.Name,
                scope,
                storeAlias);

            try
            {
                await stage.Run().ConfigureAwait(false);
                _logger.LogInformation(
                    "Algolia rebuild stage completed. Stage={Stage} Scope={Scope} Store={Store} DurationMilliseconds={DurationMilliseconds}",
                    stage.Name,
                    scope,
                    storeAlias,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failures.Add(new InvalidOperationException($"Algolia {stage.Name} rebuild stage failed.", ex));
                _logger.LogError(
                    ex,
                    "Algolia rebuild stage failed; continuing with remaining stages. Stage={Stage} Scope={Scope} Store={Store} DurationMilliseconds={DurationMilliseconds}",
                    stage.Name,
                    scope,
                    storeAlias,
                    stopwatch.ElapsedMilliseconds);
            }
        }

        if (failures.Count > 0)
            throw new AggregateException("One or more Algolia rebuild stages failed.", failures);
    }
}
