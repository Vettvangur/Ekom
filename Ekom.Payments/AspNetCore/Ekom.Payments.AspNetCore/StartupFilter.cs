using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Ekom.Payments.AspNetCore;

class EkomPaymentsStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);

        app.UseEkomPaymentsControllers();
    };
}

