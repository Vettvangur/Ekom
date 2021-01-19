using Ekom.API;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    [SuppressMessage(
        "Reliability",
        "CA2007:Consider calling ConfigureAwait on the awaited task",
        Justification = "Async controller actions don't need ConfigureAwait")]
    [SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Async controller action")]
    public class CheckoutController : SurfaceController
    {
        /// <summary>
        /// Appended after error redirect
        /// </summary>
        protected virtual string ErrorQueryString { get; set; } = "?errorStatus=serverError";

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
        public virtual async Task<ActionResult> Pay(PaymentRequest paymentRequest)
        {
            try
            {
                // ToDo: Lock order throughout request
                var order = await Order.Instance.GetOrderAsync();
                var store = Store.Instance.GetStore();
                var storeAlias = order.StoreInfo.Alias;

                var res = await ValidationAndOrderUpdates(paymentRequest, order);
                if (res != null)
                {
                    return res;
                }

                // Reset hangfire jobs in cases were user cancels on payment page and changes cart f.x.
                if (order.HangfireJobs.Any())
                {
                    foreach (var job in order.HangfireJobs)
                    {
                        await Stock.Instance.RollbackJobAsync(job);
                    }

                    await Order.Instance.RemoveHangfireJobsFromOrderAsync(storeAlias);
                }

                var hangfireJobs = new List<string>();
                res = await ProcessOrderLines(paymentRequest, order, hangfireJobs);
                if (res != null)
                {
                    return res;
                }

                res = await ProcessCoupons(paymentRequest, order);
                if (res != null)
                {
                    return res;
                }

                // save job ids to sql for retrieval after checkout completion
                await Order.Instance.AddHangfireJobsToOrderAsync(hangfireJobs);

                var orderTitle = await CreateOrderTitle(paymentRequest, order, store);

                return await ProcessPayment(paymentRequest, order, orderTitle);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.Error<CheckoutController>(ex, "Checkout payment failed!");
                return RedirectToCurrentUmbracoPage(ErrorQueryString);
            }
        }

        protected virtual async Task<ActionResult> ValidationAndOrderUpdates(PaymentRequest paymentRequest, IOrderInfo order)
        {
            if (paymentRequest == null)
            {
                return RedirectToCurrentUmbracoPage("?errorStatus=invalidData");
            }

            if (Request.Form.AllKeys.Contains("ekomUpdateInformation"))
            {
                await Order.Instance.UpdateCustomerInformationAsync(
                    Request.Form.AllKeys.ToDictionary(
                        k => k,
                        v => AntiXssEncoder.HtmlEncode(Request.Form.Get(v), false)
                    )).ConfigureAwait(false);
            }

            if (order.PaymentProvider == null)
            {
                await Order.Instance.UpdatePaymentInformationAsync(
                    paymentRequest.PaymentProvider,
                    order.StoreInfo.Alias);
            }

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

            if (string.IsNullOrEmpty(order.CustomerInformation.Customer.Name)
            || string.IsNullOrEmpty(order.CustomerInformation.Customer.Email))
            {
                return RedirectToCurrentUmbracoPage("?errorStatus=invalidData");
            }

            return null;
        }

        /// <summary>
        /// Optionally return an ActionResult to immediately return a specified response
        /// </summary>
        /// <returns>Optionally return an ActionResult to immediately return a specified response</returns>
        protected virtual async Task<ActionResult> ProcessOrderLines(PaymentRequest paymentRequest, IOrderInfo order, List<string> hangfireJobs)
        {
            #region Stock

            try
            {
                Stock.Instance.ValidateOrderStock(order);

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
            catch (NotEnoughLineStockException ex)
            {
                _logger.Error<CheckoutController>(ex, "Not Enough Stock Exception");
                if (ex.Variant.HasValue && ex.OrderLineKey != default)
                {
                    var type = ex.Variant.Value ? "variant" : "product";
                    return RedirectToCurrentUmbracoPage(
                        $"?errorStatus=stockError&errorType={type}&orderline=" + ex.OrderLineKey);
                }

                return RedirectToCurrentUmbracoPage("?errorStatus=stockError&errorType=" + ex.Message);
            }
            catch (NotEnoughStockException ex)
            {
                _logger.Error<CheckoutController>(ex, "Not Enough Stock Exception");
                return RedirectToCurrentUmbracoPage("?errorStatus=stockError&errorType=" + ex.Message);
            }

            #endregion

            return null;
        }

        /// <summary>
        /// Not yet implemented by default
        /// </summary>
        /// <returns>Optionally return an ActionResult to immediately return a specified response</returns>
        protected virtual Task<ActionResult> ProcessCoupons(PaymentRequest paymentRequest, IOrderInfo orderInfo)
        {
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

            return Task.FromResult<ActionResult>(null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected virtual Task<string> CreateOrderTitle(PaymentRequest paymentRequest, IOrderInfo order, IStore store)
        {
            string orderTitle = "Pöntun";

            if (store != null)
            {
                var paymentOrderTitle = store.GetPropertyValue("paymentOrderTitle");

                if (!string.IsNullOrEmpty(paymentOrderTitle))
                {
                    if (paymentOrderTitle.Substring(0, 1) == "#")
                    {
                        var dictionaryValue = Umbraco.GetDictionaryValue(paymentOrderTitle.Substring(1));

                        if (!string.IsNullOrEmpty(dictionaryValue))
                        {
                            orderTitle = dictionaryValue;
                        }
                    }
                    else
                    {
                        orderTitle = paymentOrderTitle;
                    }
                }
            }

            return Task.FromResult(orderTitle += " - " + order.OrderNumber);
        }

        /// <summary>
        /// Optionally return an ActionResult to immediately return a specified response.
        /// </summary>
        /// <returns>Optionally return an ActionResult to immediately return a specified response</returns>
        protected async virtual Task<ActionResult> ProcessPayment(PaymentRequest paymentRequest, IOrderInfo order, string orderTitle)
        {
            var storeAlias = order.StoreInfo.Alias;

            var ekomPP = Providers.Instance.GetPaymentProvider(paymentRequest.PaymentProvider);

            var isOfflinePayment = ekomPP.GetPropertyValue("offlinePayment", storeAlias).IsBoolean();

            var orderItems = new List<OrderItem>();
            orderItems.Add(new OrderItem
            {
                GrandTotal = order.ChargedAmount.Value,
                Price = order.ChargedAmount.Value,
                Title = orderTitle,
                Quantity = 1,
            });

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
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
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
                await Order.Instance.UpdateStatusAsync(
                    Ekom.Utilities.OrderStatus.WaitingForPayment,
                    order.UniqueId);

                var pp = NetPayment.Instance.GetPaymentProvider(ekomPP.Name);

                var language = !string.IsNullOrEmpty(ekomPP.GetPropertyValue("language", order.StoreInfo.Alias)) ? ekomPP.GetPropertyValue("language", order.StoreInfo.Alias) : "IS";

                return Content(await pp.RequestAsync(new PaymentSettings
                {
                    CustomerInfo = new CustomerInfo()
                    {
                        Address = order.CustomerInformation.Customer.Address,
                        City = order.CustomerInformation.Customer.City,
                        Email = order.CustomerInformation.Customer.Email,
                        Name = order.CustomerInformation.Customer.Name,
                        NationalRegistryId = order.CustomerInformation.Customer.Properties.GetPropertyValue("customerSsn"),
                        PhoneNumber = order.CustomerInformation.Customer.Phone,
                        PostalCode = order.CustomerInformation.Customer.ZipCode
                    },
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
