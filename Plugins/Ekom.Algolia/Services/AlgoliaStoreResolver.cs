using Ekom.Exceptions;
using Ekom.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Algolia.Services;

internal sealed class AlgoliaStoreResolver
{
    private readonly AlgoliaOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlgoliaStoreResolver> _logger;

    public AlgoliaStoreResolver(
        IOptions<AlgoliaOptions> options,
        IServiceProvider serviceProvider,
        ILogger<AlgoliaStoreResolver> logger)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public AlgoliaResolvedStore Resolve(string storeAlias)
    {
        var configuredStore = _options.Stores.FirstOrDefault(s => s.Alias.Equals(storeAlias, StringComparison.OrdinalIgnoreCase));
        var resolvedAlias = configuredStore?.Alias ?? storeAlias;
        var ekomStore = ResolveStore(resolvedAlias);
        var locales = ekomStore?.Cultures
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
        var currencies = ekomStore?.Currencies
            .Select(x => x.CurrencyValue)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        return new AlgoliaResolvedStore
        {
            Alias = resolvedAlias,
            Locale = ekomStore?.Culture?.Name,
            Currency = ekomStore?.Currency?.CurrencyValue,
            IncludeStock = configuredStore?.IncludeStock ?? false,
            Locales = locales,
            Currencies = currencies
        };
    }

    private IStore? ResolveStore(string storeAlias)
    {
        try
        {
            return _serviceProvider.GetRequiredService<Ekom.API.Store>().GetStore(storeAlias);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Algolia failed to resolve Ekom store {Store}; using config defaults only.", storeAlias);
            return null;
        }
        catch (StoreNotFoundException ex)
        {
            _logger.LogDebug(ex, "Algolia failed to resolve Ekom store {Store}; using config defaults only.", storeAlias);
            return null;
        }
    }
}
