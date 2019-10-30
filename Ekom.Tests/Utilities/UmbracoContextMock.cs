using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.Routing;

namespace Ekom.Tests.Utilities
{
    /// <summary>
    /// We cannot mock the <see cref="UmbracoContext"/> but we can mock it's factory constructor arguments
    /// </summary>
    class UmbracoContextFactoryCreator
    {
        public List<IUrlProvider> UrlProviders;
        public IPublishedSnapshotService PublishedSnapshotService;
        public IUmbracoSettingsSection UmbracoSettingsSection;
        public IGlobalSettings GlobalSettings;

        public UmbracoContextFactory Create()
        {
            return new UmbracoContextFactory(
                Mock.Of<IUmbracoContextAccessor>(),
                PublishedSnapshotService ?? Mock.Of<IPublishedSnapshotService>(),
                Mock.Of<IVariationContextAccessor>(),
                Mock.Of<IDefaultCultureAccessor>(),
                UmbracoSettingsSection ?? new Mock <IUmbracoSettingsSection> { DefaultValue = DefaultValue.Mock }.Object,
                GlobalSettings ?? new Mock <IGlobalSettings> { DefaultValue = DefaultValue.Mock }.Object,
                new UrlProviderCollection(UrlProviders ?? Enumerable.Empty<IUrlProvider>()),
                new MediaUrlProviderCollection(Enumerable.Empty<IMediaUrlProvider>()),
                Mock.Of<IUserService>());
        }
    }
}
