using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Publishing;
using Umbraco.Core.Services;
using Umbraco.Web;
using uWebshop.App_Start;
using uWebshop.Services;
using log4net;
using Microsoft.Practices.Unity;

namespace uWebshop.Events
{
    public class ApplicationEventHandler : IApplicationEventHandler
    {
        private static ILog _log;

        public ApplicationEventHandler()
        {
            var container = UnityConfig.GetConfiguredContainer();

            var logFac = UnityConfig.GetConfiguredContainer().Resolve<ILogFactory>();
            _log = logFac.GetLogger(typeof(CatalogContentFinder));
        }

        public void OnApplicationInitialized(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {

        }

        public void OnApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {

        }

        public void OnApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            ContentService.Publishing += ContentService_Publishing;
        }

        private static void ContentService_Publishing(IPublishingStrategy strategy, PublishEventArgs<IContent> e)
        {
            //var umbHelper = new UmbracoHelper(UmbracoContext.Current);
            //var contentService = ApplicationContext.Current.Services.ContentService;

            foreach (var content in e.PublishedEntities)
            {
                var alias = content.ContentType.Alias;

                if (alias == "uwbsProduct")
                {
                }

                if (alias == "uwbsCategory")
                {

                }

                if (alias == "uwbsProduct" || alias == "uwbsCategory")
                {
                    // Need to get this into function
                    var slug = content.GetValue<string>("slug");
                    var siblings = content.Parent().Children().Where(x => x.Published && x.Id != content.Id);

                    // Update Slug if Slug Exist on same Level and is Published
                    if (siblings.Any(x => x.GetValue<string>("slug").ToLowerInvariant() == slug.ToLowerInvariant()))
                    {
                        // Random not a nice solution
                        Random rnd = new Random();

                        slug = slug + "-" + rnd.Next(1, 150);

                        content.SetValue("slug", slug);

                        _log.Warn("Duplicate slug found for product : " + content.Id);

                        e.Messages.Add(new EventMessage("Duplicate Slug Found.", "Sorry but this slug is already in use, we updated it for you.", EventMessageType.Warning));
                    }

                    content.SetValue("slug", slug.ToUrlSegment());

                }
            }

        }

    }
}
