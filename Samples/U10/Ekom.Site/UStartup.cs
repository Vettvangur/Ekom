using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.BackOffice.Trees;

namespace Ekom.Site
{
    /// <summary>
    /// Hooks into the umbraco application startup lifecycle 
    /// </summary>
    // Public allows consumers to target type with ComposeAfter / ComposeBefore
    public class SiteComposer : IComposer
    {
        /// <summary>
        /// Umbraco lifecycle method
        /// </summary>
        public void Compose(IUmbracoBuilder builder)
        {
            builder.Components()
                // Can't use umbraco npoco for this since we use linq2db in core
                //.Append<EnsureTablesExist>()
                //.Append<EnsureNodesExist>()
                .Append<SiteStartup>()
                ;
        }
    }


#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    /// <summary>
    /// Here we hook into the umbraco lifecycle methods to configure Ekom.
    /// We use ApplicationEventHandler so that these lifecycle methods are only run
    /// when umbraco is in a stable condition.
    /// </summary>
    class SiteStartup : IComponent
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        readonly ILogger _logger;
        readonly IServiceProvider _factory;
        readonly IUmbracoDatabaseFactory _databaseFactory;
        readonly IUmbracoContextFactory _umbracoContextFactory;

        /// <summary>
        /// 
        /// </summary>
        public SiteStartup(
            ILogger<SiteStartup> logger,
            IServiceProvider factory,
            IUmbracoDatabaseFactory databaseFactory,
            IUmbracoContextFactory umbracoContextFactory)
        {
            _logger = logger;
            _factory = factory;
            _databaseFactory = databaseFactory;
            _umbracoContextFactory = umbracoContextFactory;
        }

        /// <summary>
        /// Umbraco startup lifecycle method
        /// </summary>
        public void Initialize()
        {
        }

        public void Terminate()
        {
        }
    }
}
