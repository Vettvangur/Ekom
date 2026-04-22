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
        var domain = ResolveDomain(configuredStore?.Domain, resolvedAlias);
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
            Domain = domain,
            Locale = ekomStore?.Culture?.Name,
            Currency = ekomStore?.Currency?.CurrencyValue,
            IncludeStock = configuredStore?.IncludeStock ?? false,
            Locales = locales,
            Currencies = currencies
        };
    }

    private string? ResolveDomain(string? domain, string storeAlias)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        if (Uri.TryCreate(domain, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString();

        _logger.LogWarning(
            "Algolia store domain is invalid for store {Store}. Domain={Domain}. Relative product and image URLs will not be converted to absolute URLs.",
            storeAlias,
            domain);

        return null;
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
