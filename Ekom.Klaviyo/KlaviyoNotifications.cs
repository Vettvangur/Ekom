using Ekom.Klaviyo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

internal sealed class KlaviyoNotifications :
    INotificationHandler<ContentPublishedNotification>,
    INotificationHandler<ContentUnpublishedNotification>
{
    private readonly IKlaviyoProductDispatcher _dispatcher;
    private readonly KlaviyoOptions _options;
    private readonly ILogger<KlaviyoNotifications> _logger;

    public KlaviyoNotifications(
        IKlaviyoProductDispatcher dispatcher,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoNotifications> logger)
    {
        _options = options.Value;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public void Handle(ContentPublishedNotification notification)
    {
        foreach (var entity in notification.PublishedEntities)
            EnqueueIfProduct(entity, isPublished: true);
    }

    public void Handle(ContentUnpublishedNotification notification)
    {
        foreach (var entity in notification.UnpublishedEntities)
            EnqueueIfProduct(entity, isPublished: false);
    }

    private void EnqueueIfProduct(IContent entity, bool isPublished)
    {
        if (!string.Equals(entity.ContentType.Alias, "ekmProduct", StringComparison.OrdinalIgnoreCase))
            return;

        if (_options.Stores is null || _options.Stores.Count == 0)
        {
            _logger.LogDebug("Klaviyo: ekmProduct {Id} has no affected stores; skipping.", entity.Id);
            return;
        }

        foreach (var storeAlias in _options.Stores)
            _ = _dispatcher.EnqueueAsync(storeAlias, entity.Key, isPublished, default);
    }

}
