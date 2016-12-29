using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Publishing;
using Umbraco.Core.Services;
using Umbraco.Web.Routing;
using uWebshop.Cache;
using uWebshop.Data;

namespace uWebshop
{
    public class Application : IApplicationEventHandler
    {
        public void OnApplicationInitialized(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            LogHelper.Info(GetType(), "OnApplicationInitialized...");
        }

        public void OnApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            LogHelper.Info(GetType(), "OnApplicationStarting...");
            
            ContentFinderResolver.Current.InsertTypeBefore<ContentFinderByPageIdQuery, CatalogContentFinder>();
            UrlProviderResolver.Current.InsertTypeBefore<DefaultUrlProvider, CatalogUrlProvider>();
        }

        public void OnApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            LogHelper.Info(GetType(), "OnApplicationStarted...");

            // Fill Caches
            StoreDomainCache.Instance.FillCache();
            StoreCache.Instance.FillCache();
            VariantCache.Instance.FillCache();
            VariantGroupCache.Instance.FillCache();
            CategoryCache.Instance.FillCache();
            ProductCache.Instance.FillCache();
            ZoneCache.Instance.FillCache();
            PaymentProviderCache.Instance.FillCache();

            // Hook into Umbraco Events
            ContentService.Published += ContentService_Published;
            ContentService.UnPublished += ContentService_UnPublished;
            ContentService.Deleted += ContentService_Deleted;
        }

        private void ContentService_Published(IPublishingStrategy sender, 
                                              PublishEventArgs<IContent> args)
        {
            foreach (var node in args.PublishedEntities)
            {
                if (Mappings.registeredTypes.ContainsKey(node.ContentType.Alias))
                {
                    Mappings.registeredTypes[node.ContentType.Alias].AddReplace(node);
                }
            }
        }

        private void ContentService_UnPublished(IPublishingStrategy sender, 
                                                PublishEventArgs<IContent> args)
        {
            foreach (var node in args.PublishedEntities)
            {
                if (Mappings.registeredTypes.ContainsKey(node.ContentType.Alias))
                {
                    Mappings.registeredTypes[node.ContentType.Alias].Remove(node.Id);
                }
            }
        }

        private void ContentService_Deleted(IContentService sender, DeleteEventArgs<IContent> args)
        {
            foreach (var node in args.DeletedEntities)
            {
                if (Mappings.registeredTypes.ContainsKey(node.ContentType.Alias))
                {
                    Mappings.registeredTypes[node.ContentType.Alias].Remove(node.Id);
                }
            }
        }
    }
}
