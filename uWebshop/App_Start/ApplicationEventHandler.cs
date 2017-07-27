﻿using Microsoft.Practices.Unity;
using System;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Publishing;
using Umbraco.Core.Services;
using Umbraco.Web.Routing;
using uWebshop.Cache;
using uWebshop.Helpers;
using System.Configuration;
using uWebshop.App_Start;
using System.Linq;
using Umbraco.Core.Persistence;
using uWebshop.Models.Data;

namespace uWebshop
{
    /// <summary>
    /// Here we hook into the umbraco lifecycle methods to configure uWebshop
    /// </summary>
    public class uWebshopStartup : ApplicationEventHandler
    {
        Configuration _config;

        /// <summary>
        /// Event fired at start of ApplicationStarted
        /// </summary>
        public static event ExtensionRegistrations ApplicationStartedCalled;

        /// <summary>
        /// Methods that override unity type registrations
        /// </summary>
        /// <param name="c"></param>
        public delegate void ExtensionRegistrations(IUnityContainer c);

        /// <summary>
        /// Umbraco startup lifecycle method
        /// </summary>
        /// <param name="umbracoApplication"></param>
        /// <param name="applicationContext"></param>
        protected override void ApplicationStarting(
            UmbracoApplicationBase umbracoApplication, 
            ApplicationContext applicationContext
        )
        {
            LogHelper.Info(GetType(), "OnApplicationStarting...");
            
            ContentFinderResolver.Current.InsertTypeBefore<ContentFinderByPageIdQuery, CatalogContentFinder>();
            UrlProviderResolver.Current.InsertTypeBefore<DefaultUrlProvider, CatalogUrlProvider>();
        }

        /// <summary>
        /// Umbraco startup lifecycle method
        /// </summary>
        /// <param name="umbracoApplication"></param>
        /// <param name="applicationContext"></param>
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            LogHelper.Info(GetType(), "ApplicationStarted...");

            // Register extension types
            var container = UnityConfig.GetConfiguredContainer();
            ApplicationStartedCalled?.Invoke(container);

            // Settings
            _config = container.Resolve<Configuration>();

            // Fill Caches
            foreach (var cacheEntry in _config.CacheList.Value)
            {
                cacheEntry.FillCache();
            }

            // Allows for configuration of content nodes to use for matching all requests
            // Use case: uWebshop populated by adapter, used as in memory cache with no backing umbraco nodes
            if (!_config.VirtualContent)
            {
                // Hook into Umbraco Events
                ContentService.Published += ContentService_Published;
                ContentService.UnPublished += ContentService_UnPublished;
                ContentService.Deleted += ContentService_Deleted;
            }

            var dbCtx = applicationContext.DatabaseContext;
            var db = new DatabaseSchemaHelper(dbCtx.Database, applicationContext.ProfilingLogger.Logger, dbCtx.SqlSyntax);

            //Check if the DB table does NOT exist
            if (!db.TableExist("uWebshopStock"))
            {
                //Create DB table - and set overwrite to false
                db.CreateTable<StockData>(false);
            }
        }

        private void ContentService_Published(
            IPublishingStrategy sender, 
            PublishEventArgs<IContent> args
        )
        {
            foreach (var node in args.PublishedEntities)
            {
                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                cacheEntry?.AddReplace(node);
            }
        }

        private void ContentService_UnPublished(
            IPublishingStrategy sender, 
            PublishEventArgs<IContent> args
        )
        {
            foreach (var node in args.PublishedEntities)
            {
                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                cacheEntry?.Remove(node.Id);
            }
        }

        private void ContentService_Deleted(IContentService sender, DeleteEventArgs<IContent> args)
        {
            foreach (var node in args.DeletedEntities)
            {
                var cacheEntry = FindMatchingCache(node.ContentType.Alias);

                cacheEntry?.Remove(node.Id);
            }
        }

        private ICache FindMatchingCache(string contentTypeAlias)
        {
            return _config.CacheList.Value.FirstOrDefault(x 
                => !string.IsNullOrEmpty(x.nodeAlias) 
                && x.nodeAlias == contentTypeAlias
            );
        }
    }
}