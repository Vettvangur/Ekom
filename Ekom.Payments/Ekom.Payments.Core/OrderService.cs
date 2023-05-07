using System;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Ekom.Payments.Helpers;
using Microsoft.AspNetCore.Http;
using Azure.Core;
using LinqToDB;
using Newtonsoft.Json;

namespace Ekom.Payments;

public interface IOrderService
{
    Task<OrderStatus?> GetAsync(Guid id);
    Task<OrderStatus> InsertAsync(decimal total,
        PaymentSettings paymentSettings,
        object paymentProviderSettings,
        string? netPaymentData,
        HttpContext httpContext);
    Task UpdateAsync(OrderStatus orderStatus);
}

/// <summary>
/// Utility functions for handling <see cref="OrderStatus"/> objects
/// </summary>
class OrderService : IOrderService
{
    readonly PaymentsConfiguration _settings;
    readonly IDatabaseFactory _dbFac;

    /// <summary>
    /// ctor
    /// </summary>
    public OrderService(
        PaymentsConfiguration settings,
        IDatabaseFactory dbFac
    )
    {
        _settings = settings;
        _dbFac = dbFac;
    }

    /// <summary>
    /// Get order with the given unique id
    /// </summary>
    /// <param name="id">Order id</param>
    public async Task<OrderStatus?> GetAsync(Guid id)
    {
        using (var db = _dbFac.GetDatabase())
        {
            return await db.OrderStatus
                .Where(x => x.UniqueId == id)
                .FirstOrDefaultAsync();
        }
    }

    /// <summary>
    /// Persist in database and retrieve unique order id
    /// </summary>
    /// <returns>Order Id</returns>
    public async Task<OrderStatus> InsertAsync(
        decimal total,
        PaymentSettings paymentSettings,
        object paymentProviderSettings,
        string? netPaymentData,
        HttpContext httpContext
    )
    {
        var orderid = Guid.NewGuid();

        var orderStatus = new OrderStatus
        {
            OrderName = paymentSettings.OrderName,
            UniqueId = orderid,
            Member = paymentSettings.Member,
            Amount = total,
            Date = DateTime.Now,
            IPAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "",
            UserAgent = httpContext.Request.Headers["User-Agent"]
                .ToString()
                .Substring(0, Math.Min(httpContext.Request.Headers["User-Agent"].Count, 4000)),
            EkomPaymentSettings = paymentSettings,
            EkomPaymentProviderData = JsonConvert.SerializeObject(paymentProviderSettings),
            CustomData = netPaymentData
        };

        using (var db = _dbFac.GetDatabase())
        {
            // Return order id
            await db.InsertAsync(orderStatus).ConfigureAwait(false);
        }

        return orderStatus;
    }

    public async Task UpdateAsync(OrderStatus orderStatus)
    {
        using (var db = _dbFac.GetDatabase())
        {
            await db.UpdateAsync(orderStatus).ConfigureAwait(false);
        }
    }
}
