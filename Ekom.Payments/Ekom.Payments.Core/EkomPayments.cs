using System;
using System.Collections.Generic;
using System.Web;
using Ekom.Payments.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Payments;

/// <summary>
/// The NetPayment API, access payment providers and get orders from request data.
/// </summary>
public class EkomPayments
{
    private readonly ILogger<EkomPayments> _logger;
    private readonly PaymentsConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 
    /// </summary>
    public EkomPayments(
        ILogger<EkomPayments> logger,
        PaymentsConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }
    
    /// <summary>
    /// Attempt to retrieve order using reference from http request.
    /// Loops over all registered <see cref="IOrderRetriever"/> to attempt to find the order reference.
    /// </summary>
    /// <param name="request">Http request</param>
    /// <param name="ppNameOverride">When storing your xml configuration under an unstandard name, specify pp name override.</param>
    /// <returns></returns>
    public OrderStatus? GetOrder(HttpRequest request, string ppNameOverride = null)
    {
        foreach (var orType in orderRetrievers)
        {
            var or = ActivatorUtilities.CreateInstance(_serviceProvider, orType) as IOrderRetriever;

            if (or == null)
            {
                _logger.LogError("Failed to create instance of {0}", orType);
                continue;
            }

            var order = or.Get(request, ppNameOverride);

            if (order != null) return order;
        }

        return null;
    }

    /// <summary>
    /// Retrieve a base payment provider by name
    /// </summary>
    /// <param name="basePpName">
    /// Base payment provider name. F.x. Borgun/Valitor
    /// Use <see cref="PaymentSettings"/> to choose an overloaded PP
    /// </param>
    /// <returns></returns>
    public IPaymentProvider GetPaymentProvider(string basePpName)
    {
        if (string.IsNullOrEmpty(basePpName))
        {
            throw new ArgumentException("string.IsNullOrEmpty", nameof(basePpName));
        }

        string normalizedPPName = basePpName.ToLowerInvariant();

        if (paymentProviders.ContainsKey(normalizedPPName))
        {
            var ppType = paymentProviders[normalizedPPName];

            var pp = ActivatorUtilities.CreateInstance(_serviceProvider, ppType) as IPaymentProvider;

            return pp;
        }
        else
        {
            throw new PaymentProviderNotFoundException("Base Payment Provider not found. DLL possibly missing. Name: " + basePpName);
        }
    }

    internal static List<Type> orderRetrievers = new List<Type>();
    internal static Dictionary<string, Type> paymentProviders = new Dictionary<string, Type>();
}
