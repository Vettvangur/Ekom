using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ekom.Umb;

internal sealed class EkomMalformedFormGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EkomMalformedFormGuardMiddleware> _logger;

    public EkomMalformedFormGuardMiddleware(
        RequestDelegate next,
        ILogger<EkomMalformedFormGuardMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;

        if (IsEkomFormRequest(request))
        {
            try
            {
                await request.ReadFormAsync(context.RequestAborted);

                if (request.Body.CanSeek)
                    request.Body.Position = 0;
            }
            catch (InvalidDataException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Rejected malformed form request for {Path}. Method={Method} ContentType={ContentType}",
                    request.Path,
                    request.Method,
                    request.ContentType);

                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Invalid form payload.", context.RequestAborted);
                }

                return;
            }
        }

        await _next(context);
    }

    private static bool IsEkomFormRequest(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method) &&
            !HttpMethods.IsPut(request.Method) &&
            !HttpMethods.IsPatch(request.Method))
        {
            return false;
        }

        return request.Path.StartsWithSegments("/ekom", StringComparison.OrdinalIgnoreCase)
            && request.HasFormContentType;
    }
}
