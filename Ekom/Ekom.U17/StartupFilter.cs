using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Ekom.Umb;

internal sealed class StartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
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
