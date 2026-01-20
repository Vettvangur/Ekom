using Microsoft.AspNetCore.Mvc.Controllers;
using System.Reflection;

namespace Ekom.Klaviyo;

public sealed class KlaviyoControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        if (!typeInfo.IsClass || typeInfo.IsAbstract)
            return false;

        if (typeInfo.Namespace is null ||
            !typeInfo.Namespace.StartsWith("Ekom.Klaviyo.Controllers", StringComparison.Ordinal))
            return false;

        return typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(typeInfo);
    }
}
