﻿using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models.Data;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Umbraco.NetPayment;
using Umbraco.NetPayment.API;
using Umbraco.Web.Mvc;
using ILogFactory = Ekom.Services.ILogFactory;

namespace Ekom.Extensions.Controllers
{
    /// <summary>
    /// Offers a default way to complete checkout using Ekom
    /// </summary>
    [PluginController("Ekom")]
    public class CheckoutController : SurfaceController
    {
        ILog _log;
        Configuration _config;
        //IDatabaseFactory _dbFac;

        /// <summary>
        /// ctor
        /// </summary>
        public CheckoutController()
        {
            var logFac = Configuration.container.GetInstance<ILogFactory>();
            _log = logFac.GetLogger(typeof(CheckoutController));

            _config = Configuration.container.GetInstance<Configuration>();
            //_dbFac = Configuration.container.GetInstance<IDatabaseFactory>();
        }

        /// <summary>
        /// Complete payment using the Standard Ekom checkout controller
        /// </summary>
        /// <param name="paymentRequest"></param>
        /// <param name="form"></param>
        /// <returns></returns>
        public async Task<ActionResult> Pay(PaymentRequest paymentRequest, FormCollection form)
        {
            var order = Order.Current.GetOrder();
            var ekomPP = Providers.Current.GetPaymentProvider(paymentRequest.PaymentProvider);

            var pp = NetPayment.Current.GetPaymentProvider(ekomPP.Name);

            if (_config.StoreCustomerData)
            {
                using (var db = DatabaseContext.Database)
                {
                    db.Insert(new CustomerData
                    {
                        // Unfinished
                    });
                }
            }

            var orderItems = new List<OrderItem>();
            foreach (var line in order.OrderLines)
            {
                try
                {
                    Stock.Current.ReserveStock(line.Id, line.Quantity);

                    if (line.Discount != null)
                    {
                        Stock.Current.ReserveDiscountStock(line.Discount.Key, 1, line.Coupon);

                        if (line.Discount.HasMasterStock)
                        {
                            Stock.Current.ReserveDiscountStock(line.Discount.Key, 1);
                        }
                    }
                }
                catch (StockException)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Not enough stock available");
                }

                orderItems.Add(new OrderItem
                {
                    GrandTotal = line.Amount.Value,
                    Price = line.Product.OriginalPrice,
                    Title = line.Product.Title,
                    Quantity = line.Quantity,
                });
            }

            if (order.Discount != null)
            {
                Stock.Current.ReserveDiscountStock(order.Discount.Key, 1, order.Coupon);

                if (order.Discount.HasMasterStock)
                {
                    Stock.Current.ReserveDiscountStock(order.Discount.Key, 1);
                }
            }

            if (paymentRequest.ShippingProvider != Guid.Empty)
            {
                var ekomSP = Providers.Current.GetShippingProvider(paymentRequest.ShippingProvider);
                var orderItemsList = orderItems.ToList();
                orderItemsList.Add(new OrderItem
                {
                    GrandTotal = ekomSP.Price.Value,
                    Price = ekomSP.Price.Value,
                    Title = ekomSP.Title,
                    Quantity = 1,
                });

                orderItems = orderItemsList;
            }

            return Content(await pp.RequestAsync(
                order.ChargedAmount.Value,
                orderItems,
                skipReceipt: true,
                culture: order.StoreInfo.Alias,
                member: Umbraco.MembershipHelper.GetCurrentMemberId(),
                orderCustomString: order.UniqueId.ToString()
            ));
        }
    }

    /// <summary>
    /// Unfinished
    /// </summary>
    public class PaymentRequest
    {
        public Guid PaymentProvider { get; set; }

        public Guid ShippingProvider { get; set; }
    }
}
