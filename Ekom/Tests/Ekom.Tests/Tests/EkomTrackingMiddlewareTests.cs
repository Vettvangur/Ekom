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
        var trackingCookieService = new TestTrackingCookieService(captured: new OrderTracking
        {
            Source = "google"
        });
        var preConsentTrackingSessionService = new TestPreConsentTrackingSessionService();
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
            new TestTrackingConsentService(analytics: true, marketing: true),
            trackingCookieService,
            preConsentTrackingSessionService);

        await sut.InvokeAsync(httpContext);

        Assert.NotNull(trackingCookieService.LastWrittenTracking);
        Assert.Equal("google", trackingCookieService.LastWrittenTracking!.Source);
        Assert.Contains(httpContext.Response.Headers.SetCookie.ToArray(), x => x.StartsWith("EkomTracking=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvokeAsync_Does_Not_Write_Cookie_For_NonHtml_Response()
    {
        var trackingCookieService = new TestTrackingCookieService(captured: new OrderTracking
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
            new TestTrackingConsentService(analytics: true, marketing: true),
            trackingCookieService,
            new TestPreConsentTrackingSessionService());

        await sut.InvokeAsync(httpContext);

        Assert.Equal(0, httpContext.Response.Headers.SetCookie.Count);
        Assert.Null(trackingCookieService.LastWrittenTracking);
    }

    [Fact]
    public async Task InvokeAsync_Stores_PreConsent_Tracking_In_Session_When_Consent_Is_Missing()
    {
        var trackingCookieService = new TestTrackingCookieService(preConsentCaptured: new OrderTracking
        {
            Source = "google",
            Campaign = "spring-sale"
        });
        var preConsentTrackingSessionService = new TestPreConsentTrackingSessionService();
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
            new TestTrackingConsentService(analytics: false, marketing: false),
            trackingCookieService,
            preConsentTrackingSessionService);

        await sut.InvokeAsync(httpContext);

        Assert.NotNull(preConsentTrackingSessionService.StoredTracking);
        Assert.Equal("google", preConsentTrackingSessionService.StoredTracking!.Source);
        Assert.Equal("spring-sale", preConsentTrackingSessionService.StoredTracking.Campaign);
        Assert.Null(trackingCookieService.LastWrittenTracking);
    }

    [Fact]
    public async Task InvokeAsync_Promotes_PreConsent_Tracking_When_Consent_Is_Granted_Later()
    {
        var trackingCookieService = new TestTrackingCookieService(captured: new OrderTracking
        {
            Ga4 = new Ga4OrderTracking
            {
                ClientId = "123.456"
            }
        });
        var preConsentTrackingSessionService = new TestPreConsentTrackingSessionService
        {
            StoredTracking = new OrderTracking
            {
                Source = "google",
                Campaign = "spring-sale",
                Mailchimp = new MailchimpOrderTracking
                {
                    CampaignId = "mailchimp-campaign",
                    TrackingCode = "prec"
                }
            }
        };
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
            new TestTrackingConsentService(analytics: true, marketing: true),
            trackingCookieService,
            preConsentTrackingSessionService);

        await sut.InvokeAsync(httpContext);

        Assert.NotNull(trackingCookieService.LastWrittenTracking);
        Assert.Equal("google", trackingCookieService.LastWrittenTracking!.Source);
        Assert.Equal("spring-sale", trackingCookieService.LastWrittenTracking.Campaign);
        Assert.Equal("123.456", trackingCookieService.LastWrittenTracking.Ga4.ClientId);
        Assert.Equal("mailchimp-campaign", trackingCookieService.LastWrittenTracking.Mailchimp.CampaignId);
        Assert.Equal("prec", trackingCookieService.LastWrittenTracking.Mailchimp.TrackingCode);
        Assert.True(preConsentTrackingSessionService.ClearCalled);
        Assert.Null(preConsentTrackingSessionService.StoredTracking);
    }

    [Fact]
    public async Task InvokeAsync_Retains_Mailchimp_Tracking_Until_Marketing_Consent_Is_Granted()
    {
        var trackingCookieService = new TestTrackingCookieService(captured: new OrderTracking
        {
            Ga4 = new Ga4OrderTracking
            {
                ClientId = "123.456"
            }
        });
        var preConsentTrackingSessionService = new TestPreConsentTrackingSessionService
        {
            StoredTracking = new OrderTracking
            {
                Source = "mailchimp",
                Mailchimp = new MailchimpOrderTracking
                {
                    CampaignId = "mailchimp-campaign",
                    TrackingCode = "prec"
                }
            }
        };
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
            new TestTrackingConsentService(analytics: true, marketing: false),
            trackingCookieService,
            preConsentTrackingSessionService);

        await sut.InvokeAsync(httpContext);

        Assert.NotNull(trackingCookieService.LastWrittenTracking);
        Assert.Equal("mailchimp", trackingCookieService.LastWrittenTracking!.Source);
        Assert.False(trackingCookieService.LastWrittenTracking.Mailchimp.HasData());
        Assert.NotNull(preConsentTrackingSessionService.StoredTracking);
        Assert.Null(preConsentTrackingSessionService.StoredTracking!.Source);
        Assert.Equal("mailchimp-campaign", preConsentTrackingSessionService.StoredTracking.Mailchimp.CampaignId);
        Assert.Equal("prec", preConsentTrackingSessionService.StoredTracking.Mailchimp.TrackingCode);
    }

    [Fact]
    public async Task InvokeAsync_Preserves_First_Mailchimp_Campaign_After_Marketing_Consent()
    {
        var trackingCookieService = new TestTrackingCookieService(
            captured: new OrderTracking
            {
                Mailchimp = new MailchimpOrderTracking
                {
                    CampaignId = "later-campaign",
                    TrackingCode = "prec"
                }
            },
            existing: new OrderTracking
            {
                Mailchimp = new MailchimpOrderTracking
                {
                    CampaignId = "first-campaign",
                    TrackingCode = "prec"
                }
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
            new TestTrackingConsentService(analytics: false, marketing: true),
            trackingCookieService,
            new TestPreConsentTrackingSessionService());

        await sut.InvokeAsync(httpContext);

        Assert.NotNull(trackingCookieService.LastWrittenTracking);
        Assert.Equal("first-campaign", trackingCookieService.LastWrittenTracking!.Mailchimp.CampaignId);
    }

    [Fact]
    public async Task InvokeAsync_Removes_Mailchimp_Tracking_From_Cookie_When_Marketing_Consent_Is_Revoked()
    {
        var trackingCookieService = new TestTrackingCookieService(
            preConsentCaptured: new OrderTracking
            {
                Mailchimp = new MailchimpOrderTracking
                {
                    CampaignId = "later-campaign",
                    TrackingCode = "prec"
                }
            },
            existing: new OrderTracking
            {
                Mailchimp = new MailchimpOrderTracking
                {
                    CampaignId = "first-campaign",
                    TrackingCode = "prec"
                }
            });
        var preConsentTrackingSessionService = new TestPreConsentTrackingSessionService();
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
            new TestTrackingConsentService(analytics: false, marketing: false),
            trackingCookieService,
            preConsentTrackingSessionService);

        await sut.InvokeAsync(httpContext);

        Assert.NotNull(trackingCookieService.LastWrittenTracking);
        Assert.False(trackingCookieService.LastWrittenTracking!.Mailchimp.HasData());
        Assert.NotNull(preConsentTrackingSessionService.StoredTracking);
        Assert.Equal("first-campaign", preConsentTrackingSessionService.StoredTracking!.Mailchimp.CampaignId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("later-campaign")]
    public async Task InvokeAsync_Retains_Existing_Mailchimp_Tracking_During_Analytics_Only_Consent(
        string? preConsentCampaignId)
    {
        var trackingCookieService = new TestTrackingCookieService(
            captured: new OrderTracking
            {
                Ga4 = new Ga4OrderTracking { ClientId = "123.456" }
            },
            existing: new OrderTracking
            {
                Mailchimp = new MailchimpOrderTracking
                {
                    CampaignId = "first-campaign",
                    TrackingCode = "prec"
                }
            });
        var preConsentTrackingSessionService = new TestPreConsentTrackingSessionService();
        if (preConsentCampaignId is not null)
        {
            preConsentTrackingSessionService.StoredTracking = new OrderTracking
            {
                Mailchimp = new MailchimpOrderTracking
                {
                    CampaignId = preConsentCampaignId,
                    TrackingCode = "prec"
                }
            };
        }
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
            new TestTrackingConsentService(analytics: true, marketing: false),
            trackingCookieService,
            preConsentTrackingSessionService);

        await sut.InvokeAsync(httpContext);

        Assert.NotNull(trackingCookieService.LastWrittenTracking);
        Assert.False(trackingCookieService.LastWrittenTracking!.Mailchimp.HasData());
        Assert.NotNull(preConsentTrackingSessionService.StoredTracking);
        Assert.Equal("first-campaign", preConsentTrackingSessionService.StoredTracking!.Mailchimp.CampaignId);
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
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(responseFeature);

        var httpContext = new DefaultHttpContext(features);
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/";
        return httpContext;
    }

    private sealed class TestTrackingCookieService : ITrackingCookieService
    {
        private readonly OrderTracking? _captured;
        private readonly OrderTracking? _existing;
        private readonly OrderTracking? _preConsentCaptured;

        public TestTrackingCookieService(
            OrderTracking? captured = null,
            OrderTracking? preConsentCaptured = null,
            OrderTracking? existing = null)
        {
            _captured = captured;
            _preConsentCaptured = preConsentCaptured;
            _existing = existing;
        }

        public OrderTracking? LastWrittenTracking { get; private set; }

        public OrderTracking? ReadCookie(HttpContext httpContext)
            => _existing;

        public void WriteCookie(HttpContext httpContext, OrderTracking tracking)
        {
            LastWrittenTracking = tracking;
            httpContext.Response.Cookies.Append("EkomTracking", "written");
        }

        public OrderTracking? CaptureFromRequest(HttpContext httpContext)
            => _captured;

        public OrderTracking? CaptureAttributionFromRequest(HttpContext httpContext)
            => _preConsentCaptured;
    }

    private sealed class TestTrackingConsentService : ITrackingConsentService
    {
        private readonly bool _analytics;
        private readonly bool _marketing;

        public TestTrackingConsentService(bool analytics, bool marketing)
        {
            _analytics = analytics;
            _marketing = marketing;
        }

        public OrderConsent GetConsent(HttpContext httpContext, string? storeAlias = null)
            => new()
            {
                Analytics = _analytics,
                Marketing = _marketing
            };

        public bool CanCaptureAnalytics(OrderConsent? consent)
            => consent?.Analytics == true;

        public bool CanCaptureMarketing(OrderConsent? consent)
            => consent?.Marketing == true;
    }

    private sealed class TestPreConsentTrackingSessionService : IPreConsentTrackingSessionService
    {
        public OrderTracking? StoredTracking { get; set; }
        public bool ClearCalled { get; private set; }

        public OrderTracking? Read(HttpContext httpContext)
            => StoredTracking;

        public void WriteFirstTouch(HttpContext httpContext, OrderTracking tracking)
            => StoredTracking ??= tracking.Clone();

        public void Clear(HttpContext httpContext)
        {
            ClearCalled = true;
            StoredTracking = null;
        }
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
