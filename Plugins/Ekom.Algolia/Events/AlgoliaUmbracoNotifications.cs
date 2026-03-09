using Ekom.Algolia.Indexing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

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

    private readonly IAlgoliaProductIndexService _indexer;
    private readonly AlgoliaOptions _options;
    private readonly ILogger<AlgoliaUmbracoNotifications> _logger;

    public AlgoliaUmbracoNotifications(
        IAlgoliaProductIndexService indexer,
        IOptions<AlgoliaOptions> options,
        ILogger<AlgoliaUmbracoNotifications> logger)
    {
        _indexer = indexer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var entity in notification.PublishedEntities)
            await EnqueueIfProductAsync(entity, isPublished: true, cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleAsync(ContentUnpublishedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var entity in notification.UnpublishedEntities)
            await EnqueueIfProductAsync(entity, isPublished: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleAsync(ContentMovedToRecycleBinNotification notification, CancellationToken cancellationToken)
    {
        foreach (var entity in notification.MoveInfoCollection.Select(x => x.Entity))
            await EnqueueIfProductAsync(entity, isPublished: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleAsync(ContentDeletedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var entity in notification.DeletedEntities)
            await EnqueueIfProductAsync(entity, isPublished: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnqueueIfProductAsync(IContent entity, bool isPublished, CancellationToken ct)
    {
        if (!string.Equals(entity.ContentType.Alias, ProductAlias, StringComparison.OrdinalIgnoreCase))
            return;

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
                await _indexer.EnqueueProductAsync(store.Alias, entity.Key, isPublished, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Algolia enqueue failed for product {Id} store {Store}.", entity.Id, store.Alias);
            }
        }
    }
}
