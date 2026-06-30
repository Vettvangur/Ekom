using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Routing;

namespace Ekom.Umb;

/// <summary>
/// Hooks Ekom into the Umbraco 17 application startup lifecycle.
/// </summary>
public sealed class EkomComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.ContentFinders()
            .InsertBefore<ContentFinderByPageIdQuery, CatalogContentFinder>();
        builder.UrlProviders()
            .Insert<CatalogUrlProvider>();

        builder.Components()
            .Append<EnsureTablesExist>()
            .Append<EnsureNodesExist>()
            .Append<EkomStartup>()
            .Append<TrackingAutomationEvents>();

        builder
            .AddNotificationAsyncHandler<ContentPublishedNotification, UmbracoEventListeners>()
            .AddNotificationAsyncHandler<ContentUnpublishedNotification, UmbracoEventListeners>()
            .AddNotificationAsyncHandler<ContentSavingNotification, UmbracoEventListeners>()
            .AddNotificationAsyncHandler<ContentDeletedNotification, UmbracoEventListeners>()
            .AddNotificationHandler<ContentMovedToRecycleBinNotification, UmbracoEventListeners>()
            .AddNotificationHandler<ContentMovedNotification, UmbracoEventListeners>()
            .AddNotificationHandler<ServerVariablesParsingNotification, UmbracoEventListeners>()
            .AddNotificationHandler<LanguageSavedNotification, UmbracoEventListeners>()
            .AddNotificationHandler<LanguageDeletedNotification, UmbracoEventListeners>();

        builder.Services.AddEkom(builder.Config);
    }
}
