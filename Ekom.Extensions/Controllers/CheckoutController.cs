using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models.Data;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Umbraco.Core.Logging;
using Umbraco.Core.Scoping;
using Umbraco.NetPayment;
using Umbraco.NetPayment.API;
using Umbraco.NetPayment.Helpers;
using Umbraco.Web.Mvc;

namespace Ekom.Extensions.Controllers
{
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
    /// <summary>
    /// Offers a default way to complete checkout using Ekom
    /// </summary>
    [PluginController("Ekom")]
    public class CheckoutController : SurfaceController
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        readonly IScopeProvider _scopeProvider;

        /// <summary>
        /// ctor
        /// </summary>
        public CheckoutController(ILogger logger, Configuration config, IScopeProvider scopeProvider)
        {
            _logger = logger;
            _config = config;
            _scopeProvider = scopeProvider;
        }

        /// <summary>
        /// Complete payment using the Standard Ekom checkout controller
        /// </summary>
        /// <param name="paymentRequest"></param>
        /// <returns></returns>
        public async Task<ActionResult> Pay(PaymentRequest paymentRequest)
        {
            if (paymentRequest == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            try
            {
                var hangfireJobs = new List<string>();

                var order = Order.Instance.GetOrder();
                var ekomPP = Providers.Instance.GetPaymentProvider(paymentRequest.PaymentProvider);

                if (order.PaymentProvider == null)
                {
                    await Order.Instance.UpdatePaymentInformationAsync(
                        paymentRequest.PaymentProvider,
                        order.StoreInfo.Alias);
                }

                var storeAlias = order.StoreInfo.Alias;

                var isOfflinePayment = ekomPP.GetPropertyValue("offlinePayment", storeAlias).IsBoolean();

                if (_config.StoreCustomerData)
                {
                    using (var db = _scopeProvider.CreateScope().Database)
                    {
                        await db.InsertAsync(new CustomerData
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
                                        hangfireJobs.Add(await Stock.Instance.ReserveStockAsync(variant.Key, (line.Quantity * -1)));
                                    }
                                    else
                                    {
                                        return RedirectToCurrentUmbracoPage("?errorStatus=stockError");
                                    }
                                }

                            }
                            else
                            {
                                var productStock = Stock.Instance.GetStock(line.ProductKey);

                                if (productStock >= line.Quantity)
                                {
                                    hangfireJobs.Add(await Stock.Instance.ReserveStockAsync(line.ProductKey, (line.Quantity * -1)));
                                }
                                else
                                {
                                    return RedirectToCurrentUmbracoPage("?errorStatus=stockError");
                                }
                            }
                        }

                        // How does this work ? we dont have a coupon per orderline!
                        //if (line.Discount != null)
                        //{
                        //    hangfireJobs.Add(_stock.ReserveDiscountStock(line.Discount.Key, 1, line.Coupon));

                        //    if (line.Discount.HasMasterStock)
                        //    {
                        //        hangfireJobs.Add(_stock.ReserveDiscountStock(line.Discount.Key, 1));
                        //    }
                        //}
                    }
                    catch (StockException)
                    {
                        return RedirectToCurrentUmbracoPage("?errorStatus=stockError");
                    }

                    orderItems.Add(new OrderItem
                    {
                        GrandTotal = line.Amount.BeforeDiscount.Value,
                        Price = line.Product.Price.WithVat.Value,
                        Title = line.Product.Title,
                        Quantity = line.Quantity,
                    });
                }

                // Does not work with Coupon codes
                //if (order.Discount != null)
                //{
                //    try
                //    {
                //        hangfireJobs.Add(_stock.ReserveDiscountStock(order.Discount.Key, -1, order.Coupon));

                //        if (order.Discount.HasMasterStock)
                //        {
                //            hangfireJobs.Add(_stock.ReserveDiscountStock(order.Discount.Key, -1));
                //        }
                //    }
                //    catch (StockException)
                //    {
                //        return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Not enough discount stock available");
                //    }
                //}

                if (paymentRequest.ShippingProvider != Guid.Empty)
                {
                    var ekomSP = Providers.Instance.GetShippingProvider(paymentRequest.ShippingProvider);

                    if (ekomSP.Price.Value > 0)
                    {
                        orderItems.Add(new OrderItem
                        {
                            GrandTotal = ekomSP.Price.Value,
                            Price = ekomSP.Price.Value,
                            Title = ekomSP.Title,
                            Quantity = 1,
                        });
                    }

                }

                if (order.Discount != null)
                {
                    orderItems.Add(new OrderItem
                    {
                        Title = "Afsláttur",
                        Quantity = 1,
                        Price = order.DiscountAmount.Value * -1,
                        GrandTotal = order.DiscountAmount.Value * -1,
                    });
                }

                // save job ids to sql for retrieval after checkout completion
                await Order.Instance.AddHangfireJobsToOrderAsync(hangfireJobs);

                _logger.Info<CheckoutController>("Payment Provider: " + paymentRequest.PaymentProvider + " offline: " + isOfflinePayment);

                if (isOfflinePayment)
                {
                    try
                    {
                        var successUrl = URIHelper.EnsureFullUri(ekomPP.GetPropertyValue("successUrl", storeAlias), Request) + "?orderId=" + order.UniqueId;

                        await Order.Instance.UpdateStatusAsync(
                            Ekom.Utilities.OrderStatus.OfflinePayment,
                            order.UniqueId);

                        return Redirect(successUrl);

                    }
                    catch (Exception ex)
                    {
                        _logger.Error<CheckoutController>(ex, "Offline Payment Failed. Order: " + order.UniqueId);

                        var errorUrl = URIHelper.EnsureFullUri(ekomPP.GetPropertyValue("errorUrl", storeAlias), Request);

                        return Redirect(errorUrl);
                    }

                }
                else
                {
                    var pp = NetPayment.Current.GetPaymentProvider(ekomPP.Name);

                    return Content(await pp.RequestAsync(
                        order.ChargedAmount.Value,
                        orderItems,
                        skipReceipt: true,
                        vortoLanguage: order.StoreInfo.Alias,
                        language: "IS", //TODO needs to come from Store, but we can not use culture
                        member: Umbraco.MembershipHelper.GetCurrentMemberId(),
                        orderCustomString: order.UniqueId.ToString()
                    //paymentProviderId: paymentRequest.PaymentProvider.ToString()
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger.Error<CheckoutController>(ex, "Checkout payment failed!");
                return RedirectToCurrentUmbracoPage("?errorStatus=serverError");
            }
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
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
}
