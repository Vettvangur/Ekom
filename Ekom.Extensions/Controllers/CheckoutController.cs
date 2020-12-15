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
using System.Web.Security.AntiXss;
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
                if (Request.Form.AllKeys.Contains("ekomUpdateInformation")) {
                    await Order.Instance.UpdateCustomerInformationAsync(Request.Form.AllKeys.ToDictionary(k => k, v => AntiXssEncoder.HtmlEncode(Request.Form.Get(v), false))).ConfigureAwait(false);
                }

                var hangfireJobs = new List<string>();

                var order = Order.Instance.GetOrder();
                var store = Store.Instance.GetStore();
                
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

                if (string.IsNullOrEmpty(order.CustomerInformation.Customer.Name) || string.IsNullOrEmpty(order.CustomerInformation.Customer.Email))
                {
                    return RedirectToCurrentUmbracoPage("?errorStatus=invalidData");
                }

                //if (order.HangfireJobs.Any())
                //{
                //    foreach (var job in order.HangfireJobs)
                //    {
                //        await Stock.Instance.RollbackJob(job).ConfigureAwait(false);
                //    }

                //    await Order.Instance.RemoveHangfireJobsToOrderAsync(storeAlias);
                //}

                var orderItems = new List<OrderItem>();
                foreach (var line in order.OrderLines)
                {
                    #region Stock

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

                                        //hangfireJobs.Add(await Stock.Instance.ReserveStockAsync(variant.Key, (line.Quantity * -1)));

                                        await Stock.Instance.IncrementStockAsync(variant.Key, (line.Quantity * -1)).ConfigureAwait(false);

                                    }
                                    else
                                    {
                                        return RedirectToCurrentUmbracoPage("?errorStatus=stockError&errorType=variant&orderline=" + line.Key);
                                    }
                                }

                            }
                            else
                            {
                                var productStock = Stock.Instance.GetStock(line.ProductKey);

                                if (productStock >= line.Quantity)
                                {
                                    //hangfireJobs.Add(await Stock.Instance.ReserveStockAsync(line.ProductKey, (line.Quantity * -1)));

                                    await Stock.Instance.IncrementStockAsync(line.ProductKey, (line.Quantity * -1)).ConfigureAwait(false);
                                }
                                else
                                {
                                    return RedirectToCurrentUmbracoPage("?errorStatus=stockError&errorType=product&orderline=" + line.Key);
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
                    catch (NotEnoughStockException ex)
                    {
                        _logger.Error<CheckoutController>(ex, "Not Enough Stock Exception");
                        return RedirectToCurrentUmbracoPage("?errorStatus=stockError&errorType=" + ex.Message);
                    }
                    

                    #endregion

                    //orderItems.Add(new OrderItem
                    //{
                    //    GrandTotal = line.Amount.WithVat.Value,
                    //    Price = line.Product.Price.WithVat.Value,
                    //    Title = line.Product.Title,
                    //    Quantity = line.Quantity,
                    //});
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

                //if (paymentRequest.ShippingProvider != Guid.Empty)
                //{
                //    var ekomSP = Providers.Instance.GetShippingProvider(paymentRequest.ShippingProvider);

                //    if (ekomSP.Price.Value > 0)
                //    {
                //        orderItems.Add(new OrderItem
                //        {
                //            GrandTotal = ekomSP.Price.Value,
                //            Price = ekomSP.Price.Value,
                //            Title = ekomSP.Title,
                //            Quantity = 1,
                //        });
                //    }

                //}

                //if (order.Discount != null)
                //{
                //    orderItems.Add(new OrderItem
                //    {
                //        Title = "Afsláttur",
                //        Quantity = 1,
                //        Price = order.DiscountAmount.Value * -1,
                //        GrandTotal = order.DiscountAmount.Value * -1,
                //    });
                //}

                string orderTitle = "Pöntun";

                if (store != null)
                {
                    var paymentOrderTitle = store.GetPropertyValue("paymentOrderTitle");

                    if (!string.IsNullOrEmpty(paymentOrderTitle))
                    {
                        if (paymentOrderTitle.Substring(0,1) == "#")
                        {
                            var dictionaryValue = Umbraco.GetDictionaryValue(paymentOrderTitle.Substring(1));

                            if (!string.IsNullOrEmpty(dictionaryValue))
                            {
                                orderTitle = dictionaryValue;
                            }
                        } else
                        {
                            orderTitle = paymentOrderTitle;
                        }
                    }
                }

                orderTitle += " - " + order.OrderNumber;

                orderItems.Add(new OrderItem
                {
                    GrandTotal = order.ChargedAmount.Value,
                    Price = order.ChargedAmount.Value,
                    Title = orderTitle,
                    Quantity = 1,
                });

                // save job ids to sql for retrieval after checkout completion
                await Order.Instance.AddHangfireJobsToOrderAsync(hangfireJobs);

                _logger.Info<CheckoutController>(
                    "Payment Provider: {PaymentProvider} offline: {isOfflinePayment}",
                    paymentRequest.PaymentProvider,
                    isOfflinePayment);

                if (isOfflinePayment)
                {
                    try
                    {
                        var successUrl = URIHelper.EnsureFullUri(ekomPP.GetPropertyValue("successUrl", storeAlias), Request) + "?orderId=" + order.UniqueId;

                        await Order.Instance.UpdateStatusAsync(
                            Ekom.Utilities.OrderStatus.OfflinePayment,
                            order.UniqueId);

                        LocalCallback.OnSuccess(new Umbraco.NetPayment.OrderStatus()
                        {
                            Member = Members.GetCurrentMemberId(),
                            PaymentProviderKey = ekomPP.Key,
                            PaymentProvider = ekomPP.Name,
                            Custom = order.UniqueId.ToString()
                        });

                        return Redirect(successUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error<CheckoutController>(
                            ex,
                            "Offline Payment Failed. Order: {UniqueId}",
                            order.UniqueId);

                        var errorUrl = URIHelper.EnsureFullUri(ekomPP.GetPropertyValue("errorUrl", storeAlias), Request);

                        return Redirect(errorUrl);
                    }
                }
                else
                {
                    var pp = NetPayment.Instance.GetPaymentProvider(ekomPP.Name);

                    var language = !string.IsNullOrEmpty(ekomPP.GetPropertyValue("language", order.StoreInfo.Alias)) ? ekomPP.GetPropertyValue("language", order.StoreInfo.Alias) : "IS";

                    return Content(await pp.RequestAsync(new PaymentSettings
                    {
                        Orders = orderItems,
                        SkipReceipt = true,
                        VortoLanguage = order.StoreInfo.Alias,
                        Language = language,
                        Member = Members.GetCurrentMemberId(),
                        OrderCustomString = order.UniqueId.ToString(),
                        //paymentProviderId: paymentRequest.PaymentProvider.ToString()
                    }));
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
