using Ekom.Payments.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Ekom.Payments.AspNetCore;

class EkomPaymentsStartupFilter : IStartupFilter
{
    readonly ILogger<EkomPaymentsStartupFilter> _logger;
    readonly EnsureTablesExist _ensureTablesExist;

    public EkomPaymentsStartupFilter(EnsureTablesExist ensureTablesExist, ILogger<EkomPaymentsStartupFilter> logger)
    {
        _ensureTablesExist = ensureTablesExist;
        _logger = logger;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        RegisterPaymentProviders();
        RegisterOrderRetrievers();
        
        _ensureTablesExist.Create();
        
        next(app);

        app.UseEkomPaymentsControllers();
    };

    /// <summary>
    /// Find and register all <see cref="IPaymentProvider"/> with reflection.
    /// </summary>
    private void RegisterPaymentProviders()
    {
        _logger.LogDebug("Registering NetPayment Providers");

        var ppType = typeof(IPaymentProvider);
        var paymentProviders = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => TypeHelper.GetConcreteTypesWithInterface(x, ppType));

        _logger.LogDebug("Found {PaymentProvidersCount} payment providers", paymentProviders.Count());

        foreach (var pp in paymentProviders)
        {
            // Get value of "_ppNodeName" constant
            var fi = pp.GetField("_ppNodeName", BindingFlags.Static | BindingFlags.NonPublic);

            if (fi != null)
            {
                var dta = (string)fi.GetRawConstantValue();
                EkomPayments.paymentProviders[dta.ToLower()] = pp;
            }
        }

        _logger.LogDebug($"Registering NetPayment Providers - Done");
    }

    /// <summary>
    /// Find and register all <see cref="IOrderRetriever"/> with reflection.
    /// </summary>
    private void RegisterOrderRetrievers()
    {
        _logger.LogDebug($"Registering NetPayment Order Retrievers");

        var ppType = typeof(IOrderRetriever);
        var orderRetrievers = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => TypeHelper.GetConcreteTypesWithInterface(x, ppType));

        _logger.LogDebug("Found {OrderRetrieversCount} Order Retrievers", orderRetrievers.Count());

        foreach (var or in orderRetrievers)
        {
            EkomPayments.orderRetrievers.Add(or);
        }

        _logger.LogDebug($"Registering NetPayment Order Retrievers - Done");
    }
}

