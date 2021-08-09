using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Security;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Dictionary;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.Security;

namespace Ekom.Tests.MockClasses
{
    class UmbracoHelperCreator
    {
        public Mock<IPublishedMemberCache> PublishedMemberCache = new Mock<IPublishedMemberCache>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<MembershipProvider> MembershipProvider = new Mock<MembershipProvider>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<RoleProvider> RoleProvider = new Mock<RoleProvider>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<IMemberService> MemberService = new Mock<IMemberService>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<IMemberTypeService> MemberTypeService = new Mock<IMemberTypeService>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<IUserService> UserService = new Mock<IUserService>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<IPublicAccessService> PublicAccessService = new Mock<IPublicAccessService>
        {
            DefaultValue = DefaultValue.Mock,
        };

        public Mock<IPublishedContent> PublishedContent = new Mock<IPublishedContent>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<ITagQuery> TagQuery = new Mock<ITagQuery>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<ICultureDictionaryFactory> CultureDictionaryFactory = new Mock<ICultureDictionaryFactory>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<IUmbracoComponentRenderer> UmbracoComponentRenderer = new Mock<IUmbracoComponentRenderer>
        {
            DefaultValue = DefaultValue.Mock,
        };
        public Mock<IPublishedContentQuery> PublishedContentQuery = new Mock<IPublishedContentQuery>
        {
            DefaultValue = DefaultValue.Mock,
        };

        public UmbracoHelperCreator(IRegister register, IFactory factory)
        {
            var membershipHelper = new MembershipHelper(
                factory.GetInstance<HttpContextBase>(),
                PublishedMemberCache.Object,
                MembershipProvider.Object,
                RoleProvider.Object,
                MemberService.Object,
                MemberTypeService.Object,
                UserService.Object,
                PublicAccessService.Object,
                new AppCaches(),
                Mock.Of<ILogger>());
            var umbHelper = new UmbracoHelper(
                PublishedContent.Object,
                TagQuery.Object,
                CultureDictionaryFactory.Object,
                UmbracoComponentRenderer.Object,
                PublishedContentQuery.Object,
                membershipHelper);
            register.Register(umbHelper);
        }
    }
}
