using Microsoft.AspNetCore.Builder;

namespace Ekom.Payments.AspNetCore
{
    static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseEkomPaymentsControllers(this IApplicationBuilder app)
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            return app;
        }
    }
}
