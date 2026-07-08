using Ekom.Algolia.Indexing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;

namespace Ekom.Algolia.Events;

internal sealed class AlgoliaUmbracoNotificationComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<ContentPublishedNotification, AlgoliaUmbracoNotifications>();
        builder.AddNotificationAsyncHandler<ContentUnpublishedNotification, AlgoliaUmbracoNotifications>();
        builder.AddNotificationAsyncHandler<ContentMovedToRecycleBinNotification, AlgoliaUmbracoNotifications>();
        builder.AddNotificationAsyncHandler<ContentDeletedNotification, AlgoliaUmbracoNotifications>();
    }
}

internal sealed class AlgoliaUmbracoNotifications :
    INotificationAsyncHandler<ContentPublishedNotification>,
    INotificationAsyncHandler<ContentUnpublishedNotification>,
    INotificationAsyncHandler<ContentMovedToRecycleBinNotification>,
    INotificationAsyncHandler<ContentDeletedNotification>
{
    private const string ProductAlias = "ekmProduct";
    private const string ProductVariantAlias = "ekmProductVariant";
    private const string ProductVariantGroupAlias = "ekmProductVariantGroup";
    private const string CategoryAlias = "ekmCategory";

    private readonly IAlgoliaProductIndexService _productIndexer;
    private readonly IAlgoliaCategoryIndexService _categoryIndexer;
    private readonly IAlgoliaContentIndexService _contentIndexer;
    private readonly IContentService _contentService;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly AlgoliaOptions _options;
    private readonly ILogger<AlgoliaUmbracoNotifications> _logger;

    public AlgoliaUmbracoNotifications(
        IAlgoliaProductIndexService productIndexer,
        IAlgoliaCategoryIndexService categoryIndexer,
        IAlgoliaContentIndexService contentIndexer,
        IContentService contentService,
        IServerRoleAccessor serverRoleAccessor,
        IOptions<AlgoliaOptions> options,
        ILogger<AlgoliaUmbracoNotifications> logger)
    {
        _productIndexer = productIndexer;
        _categoryIndexer = categoryIndexer;
        _contentIndexer = contentIndexer;
        _contentService = contentService;
        _serverRoleAccessor = serverRoleAccessor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Algolia received ContentPublishedNotification with {Count} published entities.", notification.PublishedEntities.Count());

        foreach (var entity in notification.PublishedEntities)
            await EnqueueIfSupportedAsync(entity, isPublished: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleAsync(ContentUnpublishedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Algolia received ContentUnpublishedNotification with {Count} unpublished entities.", notification.UnpublishedEntities.Count());

        foreach (var entity in notification.UnpublishedEntities)
            await EnqueueIfSupportedAsync(entity, isPublished: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleAsync(ContentMovedToRecycleBinNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Algolia received ContentMovedToRecycleBinNotification with {Count} entities.", notification.MoveInfoCollection.Count());

        foreach (var entity in notification.MoveInfoCollection.Select(x => x.Entity))
            await EnqueueIfSupportedAsync(entity, isPublished: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleAsync(ContentDeletedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Algolia received ContentDeletedNotification with {Count} deleted entities.", notification.DeletedEntities.Count());

        foreach (var entity in notification.DeletedEntities)
            await EnqueueIfSupportedAsync(entity, isPublished: false, cancellationToken).ConfigureAwait(false);
    }

    private Task EnqueueIfSupportedAsync(IContent entity, bool isPublished, CancellationToken ct)
    {
        if (string.Equals(entity.ContentType.Alias, ProductAlias, StringComparison.OrdinalIgnoreCase))
            return EnqueueProductAsync(entity, isPublished, ct);

        if (_options.Indexing.Variants && IsProductVariantContent(entity.ContentType.Alias))
            return EnqueueVariantProductAsync(entity, isPublished, ct);

        if (string.Equals(entity.ContentType.Alias, CategoryAlias, StringComparison.OrdinalIgnoreCase))
            return EnqueueCategoryAsync(entity, isPublished, ct);

        if (IsConfiguredContentType(entity.ContentType.Alias))
            return EnqueueContentAsync(entity, isPublished, ct);

        _logger.LogDebug(
            "Algolia skipping content {Id} because alias {Alias} is not supported.",
            entity.Id,
            entity.ContentType.Alias);

        return Task.CompletedTask;
    }

    private async Task EnqueueProductAsync(IContent entity, bool isPublished, CancellationToken ct)
    {

        _logger.LogDebug(
            "Algolia handling product publish event for content {Id} key {Key}. Published={IsPublished}, StoresConfigured={StoreCount}",
            entity.Id,
            entity.Key,
            isPublished,
            _options.Stores.Count);

        if (!_options.Enabled || !_options.Indexing.Enabled || !_options.Indexing.Products)
        {
            _logger.LogDebug("Algolia disabled; skipping ekmProduct {Id}.", entity.Id);
            return;
        }

        if (_options.Stores.Count == 0)
        {
            _logger.LogDebug("Algolia stores not configured; skipping ekmProduct {Id}.", entity.Id);
            return;
        }

        foreach (var store in _options.Stores)
        {
            try
            {
                _logger.LogDebug(
                    "Algolia enqueue requested for product {Id} key {Key} store {Store}. Published={IsPublished}",
                    entity.Id,
                    entity.Key,
                    store.Alias,
                    isPublished);

                await _productIndexer.EnqueueProductAsync(store.Alias, entity.Key, isPublished, ct).ConfigureAwait(false);

                _logger.LogDebug(
                    "Algolia enqueue completed for product {Id} key {Key} store {Store}. Published={IsPublished}",
                    entity.Id,
                    entity.Key,
                    store.Alias,
                    isPublished);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Algolia enqueue failed for product {Id} store {Store}.", entity.Id, store.Alias);
            }
        }
    }

    private async Task EnqueueVariantProductAsync(IContent entity, bool isPublished, CancellationToken ct)
    {
        var product = ResolveProductAncestor(entity);
        if (product is null)
        {
            _logger.LogDebug(
                "Algolia could not resolve parent product for variant content {Id} alias {Alias}.",
                entity.Id,
                entity.ContentType.Alias);
            return;
        }

        await EnqueueProductAsync(product, isPublished: true, ct).ConfigureAwait(false);
    }

    private async Task EnqueueCategoryAsync(IContent entity, bool isPublished, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Indexing.Enabled || !_options.Indexing.Categories)
        {
            _logger.LogDebug("Algolia disabled; skipping ekmCategory {Id}.", entity.Id);
            return;
        }

        if (_options.Stores.Count == 0)
        {
            _logger.LogDebug("Algolia stores not configured; skipping ekmCategory {Id}.", entity.Id);
            return;
        }

        foreach (var store in _options.Stores)
        {
            try
            {
                await _categoryIndexer.EnqueueCategoryAsync(store.Alias, entity.Key, isPublished, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Algolia enqueue failed for category {Id} store {Store}.", entity.Id, store.Alias);
            }
        }
    }

    private Task EnqueueContentAsync(IContent entity, bool isPublished, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.ContentIndexing.Enabled)
        {
            _logger.LogDebug("Algolia disabled; skipping standard content {Id}.", entity.Id);
            return Task.CompletedTask;
        }

        if (_options.ContentIndexing.EnforcePublisherOnly && _serverRoleAccessor.CurrentServerRole is ServerRole.Subscriber or ServerRole.Unknown)
        {
            _logger.LogDebug("Algolia standard content indexing skipped on server role {ServerRole}.", _serverRoleAccessor.CurrentServerRole);
            return Task.CompletedTask;
        }

        return isPublished
            ? _contentIndexer.UpdateByIdsAsync([entity.Id], ct)
            : _contentIndexer.DeleteByKeysAsync([entity.Key], ct);
    }

    private bool IsConfiguredContentType(string alias)
        => _options.ContentIndexing.Indexes
            .SelectMany(x => x.ContentTypes)
            .Any(x => x.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));

    private static bool IsProductVariantContent(string alias)
        => string.Equals(alias, ProductVariantAlias, StringComparison.OrdinalIgnoreCase)
            || string.Equals(alias, ProductVariantGroupAlias, StringComparison.OrdinalIgnoreCase);

    private IContent? ResolveProductAncestor(IContent entity)
    {
        var current = entity;

        while (current.ParentId > 0)
        {
            var parent = _contentService.GetById(current.ParentId);
            if (parent is null)
                return null;

            if (string.Equals(parent.ContentType.Alias, ProductAlias, StringComparison.OrdinalIgnoreCase))
                return parent;

            current = parent;
        }

        return null;
    }
}
