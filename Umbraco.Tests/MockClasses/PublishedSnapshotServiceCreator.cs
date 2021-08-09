using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Web.PublishedCache;

namespace Ekom.Tests.MockClasses
{
    class PublishedSnapshotServiceCreator
    {
        public Mock<IPublishedSnapshotService> PublishedSnapshotServiceMock = new Mock<IPublishedSnapshotService>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<IPublishedSnapshot> PublishedSnapshotMock = new Mock<IPublishedSnapshot>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<IPublishedContentCache> PublishedContentCacheMock = new Mock<IPublishedContentCache>
        {
            DefaultValue = DefaultValue.Mock,
        };

        public PublishedSnapshotServiceCreator()
        {
            PublishedSnapshotServiceMock.Setup(
                x => x.CreatePublishedSnapshot(It.IsAny<string>()))
                .Returns(PublishedSnapshotMock.Object);

            PublishedSnapshotMock
                .SetupGet(x => x.Content)
                .Returns(PublishedContentCacheMock.Object);
        }
    }
}
