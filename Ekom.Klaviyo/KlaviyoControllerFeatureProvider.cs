using Ekom.Klaviyo.Controllers;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;

namespace Ekom.Klaviyo;

public class KlaviyoControllerFeatureProvider
    : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        return typeof(KlaviyoBackofficeController).IsAssignableTo(typeInfo);
    }
}

public class KlaviyoControllerComposer
    : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddMvcAndRazor(options =>
        {
            options.ConfigureApplicationPartManager(manager =>
            {
                manager.FeatureProviders.Add(new KlaviyoControllerFeatureProvider());
            });
        });
    }
}
