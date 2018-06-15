using Ekom.API;
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
        Stock _stock = Stock.Instance;

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
        /// <returns></returns>
        public async Task<ActionResult> Pay(PaymentRequest paymentRequest)
        {
            var hangfireJobs = new List<string>();

            var order = Order.Instance.GetOrder();
            var ekomPP = Providers.Instance.GetPaymentProvider(paymentRequest.PaymentProvider);

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
                    if (!line.Product.Backorder)
                    {
                        if (line.Product.VariantGroups.Any())
                        {
                            foreach (var variant in line.Product.VariantGroups.SelectMany(x => x.Variants))
                            {
                                var variantStock = Stock.Instance.GetStock(variant.Key);

                                if (variantStock >= line.Quantity)
                                {
                                    hangfireJobs.Add(_stock.ReserveStock(variant.Key, line.Quantity));
                                }
                                else
                                {
                                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Not enough stock available");
                                }
                            }

                        }
                        else
                        {
                            var productStock = Stock.Instance.GetStock(line.ProductKey);

                            if (productStock >= line.Quantity)
                            {
                                hangfireJobs.Add(_stock.ReserveStock(line.ProductKey, line.Quantity));
                            }
                            else
                            {
                                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Not enough stock available");
                            }
                        }
                    }


                    if (line.Discount != null)
                    {
                        hangfireJobs.Add(_stock.ReserveDiscountStock(line.Discount.Key, 1, line.Coupon));

                        if (line.Discount.HasMasterStock)
                        {
                            hangfireJobs.Add(_stock.ReserveDiscountStock(line.Discount.Key, 1));
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
                    Price = line.Product.Price.BeforeDiscount.Value,
                    Title = line.Product.Title,
                    Quantity = line.Quantity,
                });

                if (line.Discount != null)
                {
                    orderItems.Add(new OrderItem
                    {
                        Title = "Line discount " + line.Discount.Amount.Type,
                        Price = -line.Discount.Amount.Amount,
                        Quantity = 1,
                        GrandTotal = -line.Discount.Amount.Amount,
                    });
                }
            }

            if (order.Discount != null)
            {
                try
                {
                    hangfireJobs.Add(_stock.ReserveDiscountStock(order.Discount.Key, 1, order.Coupon));

                    if (order.Discount.HasMasterStock)
                    {
                        hangfireJobs.Add(_stock.ReserveDiscountStock(order.Discount.Key, 1));
                    }
                }
                catch (StockException)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Not enough discount stock available");
                }
            }

            if (paymentRequest.ShippingProvider != Guid.Empty)
            {
                var ekomSP = Providers.Instance.GetShippingProvider(paymentRequest.ShippingProvider);
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

            if (order.Discount != null)
            {
                orderItems.Add(new OrderItem
                {
                    Title = "Order discount " + order.Discount.Amount.Type,
                    Quantity = 1,
                    Price = order.Discount.Amount.Amount,
                    GrandTotal = order.Discount.Amount.Amount,
                });
            }

            // save job ids to sql for retrieval after checkout completion
            Order.Instance.AddHangfireJobsToOrder(hangfireJobs);

            return Content(await pp.RequestAsync(
                order.ChargedAmount.Value,
                orderItems,
                skipReceipt: true,
                vortoLanguage: order.StoreInfo.Alias,
                language: "IS", //TODO needs to come from Store, but we can not use culture
                member: Umbraco.MembershipHelper.GetCurrentMemberId(),
                orderCustomString: order.UniqueId.ToString(),
                paymentProviderId: paymentRequest.PaymentProvider.ToString()
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
