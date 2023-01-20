using Microsoft.AspNetCore.Builder;

namespace Ekom.AspNetCore
{
    static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseEkomControllers(this IApplicationBuilder app)
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            return app;
        }
    }
}
