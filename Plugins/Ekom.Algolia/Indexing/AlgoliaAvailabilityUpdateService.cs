using Algolia.Search.Clients;
using Ekom.Algolia.Services;
using Ekom.API;
using Ekom.Events;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Algolia.Indexing;

internal sealed class AlgoliaAvailabilityUpdateService
{
    private readonly ISearchClient _client;
    private readonly AlgoliaOptions _options;
    private readonly AlgoliaStoreResolver _storeResolver;
    private readonly IndexNameBuilder _indexNameBuilder;
    private readonly AlgoliaSearchCacheVersionProvider _searchCacheVersions;
    private readonly ILogger<AlgoliaAvailabilityUpdateService> _logger;

    public AlgoliaAvailabilityUpdateService(
        ISearchClient client,
        IOptions<AlgoliaOptions> options,
        AlgoliaStoreResolver storeResolver,
        IndexNameBuilder indexNameBuilder,
        AlgoliaSearchCacheVersionProvider searchCacheVersions,
        ILogger<AlgoliaAvailabilityUpdateService> logger)
    {
        _client = client;
        _options = options.Value;
        _storeResolver = storeResolver;
        _indexNameBuilder = indexNameBuilder;
        _searchCacheVersions = searchCacheVersions;
        _logger = logger;
    }

    public async Task UpdateAsync(StockChangedEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Indexing.Enabled || !_options.Indexing.Products || !_options.Indexing.EnableAvailabilityUpdates)
            return;

        var storeAliases = string.IsNullOrWhiteSpace(args.StoreAlias)
            ? _options.Stores.Select(store => store.Alias).Distinct(StringComparer.OrdinalIgnoreCase)
            : [args.StoreAlias];

        foreach (var storeAlias in storeAliases)
        {
            try
            {
                await UpdateStoreAsync(args, storeAlias, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Algolia availability update failed for stock item {Key} in store {Store}.", args.Key, storeAlias);
            }
        }
    }

    private async Task UpdateStoreAsync(StockChangedEventArgs args, string storeAlias, CancellationToken ct)
    {
        var store = _storeResolver.Resolve(storeAlias);
        var product = await Catalog.Instance.GetProductAsync(args.Key, store.Alias, raiseEvent: false, ct: ct).ConfigureAwait(false);

        if (product is not null)
        {
            var attributes = CreateProductAttributes(product, args, store.IncludeStock);
            await PartialUpdateAsync(store, product.Key.ToString(), attributes, ct).ConfigureAwait(false);
            return;
        }

        var variant = await Catalog.Instance.GetVariantAsync(args.Key, store.Alias, ct).ConfigureAwait(false);
        var parentProduct = variant?.Product;
        if (variant is null || parentProduct is null)
            return;

        var variantWasAvailable = IsVariantAvailable(variant, parentProduct, args.OldValue);
        var variantIsAvailable = variant.Available;
        var productWasAvailable = WasProductAvailable(parentProduct, variant, variantWasAvailable);
        var productIsAvailable = parentProduct.Available;

        if (_options.Indexing.Variants)
        {
            var variantAttributes = new Dictionary<string, object?>();
            if (store.IncludeStock)
            {
                variantAttributes["Stock"] = variant.Stock;
                variantAttributes["variantStock"] = variant.Stock;
            }

            if (variantWasAvailable != variantIsAvailable)
            {
                var availability = variantIsAvailable ? 1 : 0;
                variantAttributes["Available"] = availability;
                variantAttributes["variantAvailable"] = availability;
            }

            await PartialUpdateAsync(store, $"{parentProduct.Key}_{variant.Key}", variantAttributes, ct).ConfigureAwait(false);
        }

        var productAttributes = new Dictionary<string, object?>();
        if (productWasAvailable != productIsAvailable)
            productAttributes["Available"] = productIsAvailable ? 1 : 0;

        await PartialUpdateAsync(store, parentProduct.Key.ToString(), productAttributes, ct).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> CreateProductAttributes(IProduct product, StockChangedEventArgs args, bool includeStock)
    {
        var attributes = new Dictionary<string, object?>();
        var hasVariants = product.AllVariants.Any();
        var wasAvailable = product.Backorder || StockBufferHelper.GetEffectiveStock(args.OldValue, product) > 0;

        if (!hasVariants && wasAvailable != product.Available)
            attributes["Available"] = product.Available ? 1 : 0;

        if (includeStock)
            attributes["Stock"] = product.Stock;

        return attributes;
    }

    private static bool IsVariantAvailable(IVariant variant, IProduct product, decimal stock)
        => variant.Backorder || StockBufferHelper.GetEffectiveStock(stock, product, variant) > 0;

    private static bool WasProductAvailable(IProduct product, IVariant changedVariant, bool changedVariantWasAvailable)
        => product.Backorder
        || product.AllVariants.Any(variant => variant.Key == changedVariant.Key ? changedVariantWasAvailable : variant.Available);

    private async Task PartialUpdateAsync(AlgoliaResolvedStore store, string objectId, IReadOnlyDictionary<string, object?> attributes, CancellationToken ct)
    {
        if (attributes.Count == 0)
            return;

        foreach (var target in store.ExpandIndexTargets())
        {
            var indexName = _indexNameBuilder.BuildPrimary("products", target);
            await _client.PartialUpdateObjectAsync(
                indexName,
                objectId,
                attributes,
                createIfNotExists: false,
                options: null,
                cancellationToken: ct).ConfigureAwait(false);
        }

        _searchCacheVersions.InvalidateStore(store.Alias);
    }
}
