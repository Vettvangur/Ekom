using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Ekom.Umb;

internal sealed class EkomCacheInitializationHandler : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private readonly EkomCacheInitializer _cacheInitializer;

    public EkomCacheInitializationHandler(EkomCacheInitializer cacheInitializer)
    {
        _cacheInitializer = cacheInitializer;
    }

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        _cacheInitializer.Initialize(notification.IsRestarting);
    }
}
