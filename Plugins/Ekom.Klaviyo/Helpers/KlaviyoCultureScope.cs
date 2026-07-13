using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace Ekom.Klaviyo.Helpers;

internal sealed class KlaviyoCultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;
    private readonly HttpContext? _httpContext;
    private readonly IRequestCultureFeature? _previousRequestCultureFeature;

    private KlaviyoCultureScope(string culture)
    {
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;

        var resolvedCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentCulture = resolvedCulture;
        CultureInfo.CurrentUICulture = resolvedCulture;

        _httpContext = (Configuration.Resolver.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor)?.HttpContext;
        if (_httpContext is null)
            return;

        _previousRequestCultureFeature = _httpContext.Features.Get<IRequestCultureFeature>();
        _httpContext.Features.Set<IRequestCultureFeature>(
            new RequestCultureFeature(new RequestCulture(resolvedCulture), provider: null));
    }

    public static KlaviyoCultureScope? Apply(string? culture)
    {
        return string.IsNullOrWhiteSpace(culture) ? null : new KlaviyoCultureScope(culture);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
        _httpContext?.Features.Set(_previousRequestCultureFeature);
    }
}
