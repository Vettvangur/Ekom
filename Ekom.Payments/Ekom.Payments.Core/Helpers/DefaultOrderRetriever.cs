using System;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ekom.Payments.Helpers;

// This is an adequate template to use for custom order retrievers created under a payment provider
/// <summary>
/// Get a Borgun order
/// </summary>
class DefaultOrderRetriever : IOrderRetriever
{
    readonly ILogger<DefaultOrderRetriever> _logger;
    readonly IOrderService _orderSvc;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="orderSvc"></param>
    public DefaultOrderRetriever(ILogger<DefaultOrderRetriever> logger, IOrderService orderSvc)
    {
        _logger = logger;
        _orderSvc = orderSvc;
    }

    /// <summary>
    /// Attempts to retrieve an order using data from the request
    /// </summary>
    /// <returns>Returns the referenced order</returns>
    public OrderStatus? Get(HttpRequest request, string ppNameOverride = null)
    {
        // These two work for Borgun, BorgunLoans, BorgunGateway w/ 3dsecure
        string reference = request.Form.ContainsKey("reference")
            ? request.Form["reference"]
            // Valitor, Netgiro
            : request.Query["ReferenceNumber"];

        if (Guid.TryParse(reference, out var guid))
        {
            var order = _orderSvc.GetAsync(guid).Result;

            if (order != null)
            {
                _logger.LogDebug("Found order using reference {reference}", reference);
            }

            return order;
        }
        else
        {
            return null;
        }
    }
}
