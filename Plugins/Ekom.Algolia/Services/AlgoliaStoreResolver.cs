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
            Locale = ResolveDefaultLocale(ekomStore?.Culture?.Name, locales),
            Currency = ekomStore?.Currency?.CurrencyValue,
            IncludeStock = configuredStore?.IncludeStock ?? false,
            EnableAvailabilityUpdates = configuredStore?.EnableAvailabilityUpdates ?? false,
            Locales = locales,
            Currencies = currencies,
            Indexing = ResolveIndexing(configuredStore?.Indexing),
            HasIndexingOverride = configuredStore?.Indexing is not null,
            SearchableAttributes = configuredStore?.SearchableAttributes ?? [],
            Collections = configuredStore?.Collections ?? new(),
            LanguageSettings = configuredStore?.LanguageSettings ?? new(),
        };
    }

    // Ekom reports "en-US" when a store culture cannot be read, so only accept a culture the store lists.
    internal static string? ResolveDefaultLocale(string? storeLocale, IReadOnlyList<string> locales)
    {
        if (locales.Count == 0)
            return storeLocale;

        foreach (var locale in locales)
        {
            if (locale.Equals(storeLocale, StringComparison.OrdinalIgnoreCase))
                return locale;
        }

        return locales[0];
    }

    private AlgoliaIndexingOptions ResolveIndexing(AlgoliaStoreIndexingOptions? storeIndexing)
    {
        if (storeIndexing is null)
            return _options.Indexing;

        return new AlgoliaIndexingOptions
        {
            Enabled = storeIndexing.Enabled,
            Products = storeIndexing.Products,
            Categories = storeIndexing.Categories,
            Variants = storeIndexing.Variants,
            BatchSize = storeIndexing.BatchSize,
            ProductProperties = storeIndexing.ProductProperties,
            ProductCustomRanking = storeIndexing.ProductCustomRanking,
            CategoryCustomRanking = storeIndexing.CategoryCustomRanking,
            AttributesForFaceting = storeIndexing.AttributesForFaceting,
            FacetAttributes = storeIndexing.FacetAttributes,
            VariantFacetAttributes = storeIndexing.VariantFacetAttributes,
            SortedReplicas = storeIndexing.SortedReplicas,
            Dispatching = _options.Indexing.Dispatching,
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
