using Moq;
using System.Web;

namespace Ekom.Tests.MockClasses
{
    class HttpContextMocks
    {
        public Mock<HttpContextBase> httpCtxMock;
        public Mock<HttpRequestBase> httpReqMock;
        public Mock<HttpResponseBase> httpRespMock;
        public Mock<HttpSessionStateBase> httpSessMock;
        public HttpContextMocks()
        {
            httpReqMock = new Mock<HttpRequestBase> { DefaultValue = DefaultValue.Mock };
            httpReqMock.Setup(req => req.Cookies).Returns(new HttpCookieCollection());
            httpRespMock = new Mock<HttpResponseBase> { DefaultValue = DefaultValue.Mock };
            httpRespMock.Setup(resp => resp.Cookies).Returns(new HttpCookieCollection());
            httpSessMock = new Mock<HttpSessionStateBase> { DefaultValue = DefaultValue.Mock };
            httpCtxMock = new Mock<HttpContextBase> { DefaultValue = DefaultValue.Mock };
            httpCtxMock.Setup(h => h.Request).Returns(httpReqMock.Object);
            httpCtxMock.Setup(h => h.Response).Returns(httpRespMock.Object);
            httpCtxMock.Setup(h => h.Session).Returns(httpSessMock.Object);
        }
    }
}
