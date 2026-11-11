using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Ekom.Utilities;

/// <summary>
/// Caps the request body of Ekom's public storefront routes. These routes are anonymous and
/// carry small JSON or form payloads, so anything larger is rejected before it is buffered,
/// parsed or stored on an order.
/// </summary>
public sealed class EkomRequestSizeLimitMiddleware
{
    // Public storefront route groups. Backoffice, manager, printer and payment-provider
    // callback routes are left to the host's own limits.
    private static readonly PathString[] LimitedPaths =
    [
        new("/ekom/order"),
        new("/ekom/checkout"),
        new("/ekom/mvcCheckout"),
        new("/ekom/catalog"),
        new("/ekom/provider"),
        new("/ekom/api"),
        new("/ekom/order-discounts"),
    ];

    private readonly RequestDelegate _next;
    private readonly Configuration _config;

    public EkomRequestSizeLimitMiddleware(RequestDelegate next, Configuration config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        long limit = _config.MaxPublicRequestBodySize;

        if (limit > 0 && IsLimited(context.Request.Path))
        {
            if (context.Request.ContentLength > limit)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }

            // Covers bodies sent without a Content-Length: the server stops reading at the limit.
            var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodySizeFeature is { IsReadOnly: false }
                && (bodySizeFeature.MaxRequestBodySize == null || bodySizeFeature.MaxRequestBodySize > limit))
            {
                bodySizeFeature.MaxRequestBodySize = limit;
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    internal static bool IsLimited(PathString path)
    {
        foreach (var limited in LimitedPaths)
        {
            if (path.StartsWithSegments(limited, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public static class EkomRequestSizeLimitExtensions
{
    /// <summary>
    /// Adds the body-size cap for Ekom's public routes. Must run before anything reads the request body.
    /// </summary>
    public static IApplicationBuilder UseEkomRequestSizeLimit(this IApplicationBuilder app)
        => app.UseMiddleware<EkomRequestSizeLimitMiddleware>();
}
