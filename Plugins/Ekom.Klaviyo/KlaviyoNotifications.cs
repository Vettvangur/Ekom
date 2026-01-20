using Ekom.Klaviyo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

internal sealed class ArticleComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<ContentPublishedNotification, KlaviyoNotifications>();
        builder.AddNotificationAsyncHandler<ContentUnpublishedNotification, KlaviyoNotifications>();
        builder.AddNotificationAsyncHandler<ContentMovedToRecycleBinNotification, KlaviyoNotifications>();
        builder.AddNotificationAsyncHandler<ContentDeletedNotification, KlaviyoNotifications>();
    }
}

internal sealed class KlaviyoNotifications :
    INotificationAsyncHandler<ContentPublishedNotification>,
    INotificationAsyncHandler<ContentUnpublishedNotification>,
    INotificationAsyncHandler<ContentMovedToRecycleBinNotification>,
    INotificationAsyncHandler<ContentDeletedNotification>
{
    private readonly IKlaviyoProductDispatcher _dispatcher;
    private readonly KlaviyoOptions _options;
    private readonly ILogger<KlaviyoNotifications> _logger;
    private readonly IMemoryCache _cache;

    public KlaviyoNotifications(
        IKlaviyoProductDispatcher dispatcher,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoNotifications> logger,
        IMemoryCache cache)
    {
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
        _cache = cache;
    }

    public async Task HandleAsync(ContentPublishedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var entity in notification.PublishedEntities)
            await EnqueueIfProductAsync(entity, isPublished: true, cancellationToken);
    }

    public async Task HandleAsync(ContentUnpublishedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var entity in notification.UnpublishedEntities)
            await EnqueueIfProductAsync(entity, isPublished: false, cancellationToken);
    }

    public async Task HandleAsync(ContentMovedToRecycleBinNotification notification, CancellationToken cancellationToken)
    {
        foreach (var entity in notification.MoveInfoCollection.Select(x => x.Entity))
            await EnqueueIfProductAsync(entity, isPublished: false, cancellationToken);
    }

    public async Task HandleAsync(ContentDeletedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var entity in notification.DeletedEntities)
            await EnqueueIfProductAsync(entity, isPublished: false, cancellationToken);
    }

    private async Task EnqueueIfProductAsync(IContent entity, bool isPublished, CancellationToken ct)
    {
        if (!string.Equals(entity.ContentType.Alias, "ekmProduct", StringComparison.OrdinalIgnoreCase))
            return;

        if (!_options.Enabled || !_options.ProductEvents.Enabled)
        {
            _logger.LogDebug("Klaviyo disabled; skipping ekmProduct {Id}.", entity.Id);
            return;
        }

        foreach (var storeAlias in _options.Stores)
        {
            _logger.LogDebug("Klaviyo: enqueue ekmProduct {Id} store {Store}. Published={Published}", entity.Id, storeAlias, isPublished);

            try
            {
                await _dispatcher.EnqueueAsync(storeAlias, entity.Key, isPublished, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Klaviyo enqueue failed for product {Id} store {Store}.", entity.Id, storeAlias);
            }

            _cache.Remove($"klaviyo:feed:v1:{storeAlias}");
        }
    }
}
