using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Tests.Utilities;
using Examine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.Routing;

namespace Ekom.Tests
{
    [TestClass]
    public class ObjectTests
    {
        [TestCleanup]
        public void TearDown()
        {
            Current.Reset();
        }

        //[TestMethod]
        //public void CategoriesCanCallGetPropertyValueWithStoreAlias()
        //{
        //    Helpers.RegisterAll();

        //    ICategory cat = Objects.Objects.Get_Category_Women();
        //    Assert.AreEqual("Konur", cat.GetPropertyValue("title", "IS"));
        //}

        [TestMethod]
        public void StoreFromSearchResult()
        {
            var (fac, reg) = Helpers.RegisterAll();
            var httpCtx = Helpers.GetSetHttpContext();

            var examineSvcMock = new Mock<IExamineService>();
            examineSvcMock.Setup(x => x.GetExamineNode("8cea2a5d-0290-434c-aa98-12850ae4c7d6"))
                .Returns(new SearchResult("10", 0, () => null));
            reg.Register(f => examineSvcMock.Object);

            var mockUrlProvider = new Mock<IUrlProvider>();
            mockUrlProvider.Setup(x => x.GetUrl(
                It.IsAny<UmbracoContext>(),
                It.IsAny<IPublishedContent>(),
                It.IsAny<UrlMode>(),
                It.IsAny<string>(),
                It.IsAny<Uri>()
            ));

            var mockPublishedContentCache = new Mock<IPublishedContentCache>();
            mockPublishedContentCache.Setup(x => x.GetById(10))
                .Returns(Mock.Of<IPublishedContent>());

            var mockPublishedSnapshot = new Mock<IPublishedSnapshot>();
            mockPublishedSnapshot.SetupGet(x => x.Content)
                .Returns(mockPublishedContentCache.Object);

            var mockPublishedSnapshotService = new Mock<IPublishedSnapshotService>();
            mockPublishedSnapshotService.Setup(x => x.CreatePublishedSnapshot(It.IsAny<string>()))
                .Returns(mockPublishedSnapshot.Object);

            var umbSettingsSectionMock = new Mock<IUmbracoSettingsSection>()
            {
                DefaultValue = DefaultValue.Mock
            };
            umbSettingsSectionMock.Setup(x => x.WebRouting.UrlProviderMode).Returns("Auto");

            var globalSettingsMock = new Mock<IGlobalSettings>()
            {
                DefaultValue = DefaultValue.Mock
            };
            globalSettingsMock.Setup(x => x.Path).Returns("/umbraco");

            var umbCtxFacCreator = new UmbracoContextFactoryCreator
            {
                UrlProviders = new List<IUrlProvider> { mockUrlProvider.Object },
                PublishedSnapshotService = mockPublishedSnapshotService.Object,
                UmbracoSettingsSection = umbSettingsSectionMock.Object,
                GlobalSettings = globalSettingsMock.Object,
            };
            var umbCtxFac = umbCtxFacCreator.Create();
            reg.Register<IUmbracoContextFactory>(f => umbCtxFac);

            reg.Register(f => Mock.Of<IStoreDomainCache>(
                sd => sd.Cache == new ConcurrentDictionary<Guid, Umbraco.Core.Models.IDomain> { }));

            var s = new Store(Objects.Objects.StoreResult);
            Assert.AreEqual(1096, s.Id);
            Assert.AreEqual(new Guid("9d67c718-a703-4958-8e2d-271670faf207"), s.Key);
            Assert.AreEqual(10, s.StoreRootNode);
            Assert.AreEqual(true, s.VatIncludedInPrice);
        }
    }
}
