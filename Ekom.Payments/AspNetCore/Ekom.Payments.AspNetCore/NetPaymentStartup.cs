using System;
using System.Linq;
using System.Net;
using System.Reflection;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Ekom.Payments.App_Start;
using Ekom.Payments.Helpers;
using Ekom.Payments.Interfaces;
using Microsoft.Extensions.Logging;

namespace Ekom.Payments;

/// <summary>
/// Hooks into the umbraco application startup lifecycle 
/// </summary>
class Composer : IUserComposer
{
    /// <summary>
    /// Umbraco lifecycle method
    /// </summary>
    public void Compose(Composition composition)
    {
        composition.Components()
            .Append<EnsureTablesExist>()
            .Append<EnsureNodesExist>()
            .Append<NetPaymentStartup>()
            ;
    }
}

class NetPaymentStartup : IComponent
{
    readonly ILogger<NetPaymentStartup> _logger;

    public NetPaymentStartup(ILogger<NetPaymentStartup> logger)
    {
        _logger = logger;
    }

    public void Initialize()
    {
        try
        {
            _logger.LogInformation("Startup");

            RegisterPaymentProviders();
            RegisterOrderRetrievers();

            // Disable SSL and older TLS versions
            // https://docs.microsoft.com/en-us/dotnet/framework/network-programming/tls
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            _logger.LogDebug("Done");
        }
#pragma warning disable CA1031 // Don't bring down the site if NetPayment is broken
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _logger.LogError(ex, "Fatal NetPayment error, aborting");
        }
    }

    public void Terminate() { }

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
