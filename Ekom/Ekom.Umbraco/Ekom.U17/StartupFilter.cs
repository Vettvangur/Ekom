using Ekom.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Ekom.Umb;

internal sealed class StartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        // Must run before anything below reads or buffers the request body.
        app.UseEkomRequestSizeLimit();

        app.Use(async (context, nextMiddleware) =>
        {
            context.Request.EnableBuffering();
            await nextMiddleware();
        });

        app.UseEkomMalformedFormGuard();
        app.UseSession();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEkomMiddleware();
        app.UseEkomTrackingMiddleware();

        next(app);
    };
}
