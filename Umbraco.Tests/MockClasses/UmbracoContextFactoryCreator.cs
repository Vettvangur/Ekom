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
        public UmbracoContextFactoryCreator()
        {
            var mockUrlProvider = new Mock<IUrlProvider>();

            UrlProvider = mockUrlProvider;
            UrlProviders = new List<IUrlProvider> { mockUrlProvider.Object };
        }

        public Mock<IUrlProvider> UrlProvider;
        public List<IUrlProvider> UrlProviders;
        public Mock<IPublishedSnapshotService> PublishedSnapshotService = new Mock<IPublishedSnapshotService>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<IUmbracoSettingsSection> UmbracoSettingsSection = new Mock<IUmbracoSettingsSection>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<IGlobalSettings> GlobalSettings = new Mock<IGlobalSettings>() 
        { 
            DefaultValue = DefaultValue.Mock,
        };

        public UmbracoContextFactory Create()
        {
            return new UmbracoContextFactory(
                Mock.Of<IUmbracoContextAccessor>(),
                PublishedSnapshotService.Object,
                Mock.Of<IVariationContextAccessor>(),
                Mock.Of<IDefaultCultureAccessor>(),
                UmbracoSettingsSection.Object,
                GlobalSettings.Object,
                new UrlProviderCollection(UrlProviders ?? Enumerable.Empty<IUrlProvider>()),
                new MediaUrlProviderCollection(Enumerable.Empty<IMediaUrlProvider>()),
                Mock.Of<IUserService>());
        }
    }
}
