using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ekom.Shipping;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the carrier registries and the selection validator. Ekom calls this itself; carrier
    /// packages call it too, so their registration also works without the rest of Ekom, as in tests.
    /// </summary>
    public static IServiceCollection AddEkomShippingCore(this IServiceCollection services)
    {
        services.TryAddScoped<IShippingCarrierRegistry, ShippingCarrierRegistry>();
        services.TryAddScoped<IShippingSelectionValidator, ShippingSelectionValidator>();
        services.TryAddScoped<IShippingFulfillmentCarrierRegistry, ShippingFulfillmentCarrierRegistry>();
        return services;
    }

    /// <summary>
    /// Registers the shipping framework. Carrier packages add their carriers with their own
    /// registration method, such as <c>AddDroppShipping</c>.
    /// </summary>
    internal static IServiceCollection AddEkomShipping(this IServiceCollection services)
    {
        services.AddEkomShippingCore();
        services.TryAddScoped<IEkomShippingCheckoutService, EkomShippingCheckoutService>();
        services.TryAddScoped<IEkomPickupLocationQueryService, EkomPickupLocationQueryService>();
        services.TryAddScoped<IShippingOrderMapper, ShippingOrderMapper>();
        services.TryAddScoped<IShippingOrderReader, ShippingOrderReader>();
        services.TryAddScoped<IShippingFulfillmentService, ShippingFulfillmentService>();
        services.AddTransient<IOrderManagerActionProvider, ShippingOrderManagerActionProvider>();
        services.AddHostedService<ShippingAutomationHostedService>();
        return services;
    }
}
