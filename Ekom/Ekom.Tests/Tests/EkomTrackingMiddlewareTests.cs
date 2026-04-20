using Ekom.Models;
using Ekom.Tracking;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class EkomTrackingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_Writes_Cookie_OnStarting_For_Html_Response()
    {
        var trackingCookieService = new TestTrackingCookieService(new OrderTracking
        {
            Source = "google"
        });
        var responseFeature = new TestHttpResponseFeature();
        var httpContext = CreateHttpContext(responseFeature);

        var sut = new EkomTrackingMiddleware(
            async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await responseFeature.FireOnStartingAsync();
                responseFeature.HasStarted = true;
            },
            CreateOptions(),
            trackingCookieService);

        await sut.InvokeAsync(httpContext);

        Assert.NotNull(trackingCookieService.LastWrittenTracking);
        Assert.Equal("google", trackingCookieService.LastWrittenTracking!.Source);
        Assert.Contains(httpContext.Response.Headers.SetCookie, x => x.StartsWith("EkomTracking=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvokeAsync_Does_Not_Write_Cookie_For_NonHtml_Response()
    {
        var trackingCookieService = new TestTrackingCookieService(new OrderTracking
        {
            Source = "google"
        });
        var responseFeature = new TestHttpResponseFeature();
        var httpContext = CreateHttpContext(responseFeature);

        var sut = new EkomTrackingMiddleware(
            async context =>
            {
                context.Response.ContentType = "application/json";
                await responseFeature.FireOnStartingAsync();
                responseFeature.HasStarted = true;
            },
            CreateOptions(),
            trackingCookieService);

        await sut.InvokeAsync(httpContext);

        Assert.Empty(httpContext.Response.Headers.SetCookie);
        Assert.Null(trackingCookieService.LastWrittenTracking);
    }

    private static IOptions<TrackingOptions> CreateOptions()
        => Options.Create(new TrackingOptions
        {
            Enabled = true,
            CaptureEnabled = true
        });

    private static DefaultHttpContext CreateHttpContext(TestHttpResponseFeature responseFeature)
    {
        var features = new FeatureCollection();
        features.Set<IHttpResponseFeature>(responseFeature);

        var httpContext = new DefaultHttpContext(features);
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/";
        return httpContext;
    }

    private sealed class TestTrackingCookieService : ITrackingCookieService
    {
        private readonly OrderTracking? _captured;

        public TestTrackingCookieService(OrderTracking? captured)
        {
            _captured = captured;
        }

        public OrderTracking? LastWrittenTracking { get; private set; }

        public OrderTracking? ReadCookie(Microsoft.AspNetCore.Http.HttpContext httpContext)
            => null;

        public void WriteCookie(Microsoft.AspNetCore.Http.HttpContext httpContext, OrderTracking tracking)
        {
            LastWrittenTracking = tracking;
            httpContext.Response.Cookies.Append("EkomTracking", "written");
        }

        public OrderTracking? CaptureFromRequest(Microsoft.AspNetCore.Http.HttpContext httpContext)
            => _captured;
    }

    private sealed class TestHttpResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted { get; set; }

        public void OnStarting(Func<object, Task> callback, object state)
            => _onStarting.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task FireOnStartingAsync()
        {
            for (var i = _onStarting.Count - 1; i >= 0; i--)
            {
                var (callback, state) = _onStarting[i];
                await callback(state);
            }
        }
    }
}
