using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Payments;

/// <summary>
/// Lets a payment provider package register its own services. Ekom finds every implementation in the
/// assemblies that reference Ekom and calls it during startup, so installing the package is all a site needs to do.
/// Implementations need a public parameterless constructor.
/// </summary>
public interface IPaymentModule
{
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}
