using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;

namespace Ekom.Klaviyo;

public sealed class KlaviyoMvcComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddMvcAndRazor(options =>
        {
            options.ConfigureApplicationPartManager(manager =>
            {
                // Ensure this assembly is scanned for controllers
                var assembly = typeof(KlaviyoMvcComposer).Assembly;

                if (!manager.ApplicationParts.OfType<AssemblyPart>().Any(p => p.Assembly == assembly))
                    manager.ApplicationParts.Add(new AssemblyPart(assembly));

                // Add your controller filter (optional if you trust naming conventions)
                if (!manager.FeatureProviders.OfType<KlaviyoControllerFeatureProvider>().Any())
                    manager.FeatureProviders.Add(new KlaviyoControllerFeatureProvider());
            });
        });
    }
}
