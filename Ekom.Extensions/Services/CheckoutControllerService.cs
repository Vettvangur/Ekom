using Ekom.API;
using Ekom.Exceptions;
using Ekom.Extensions.Controllers;
using Ekom.Extensions.Models;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Security.AntiXss;
using Umbraco.Core.Logging;
using Umbraco.Core.Scoping;
using Umbraco.NetPayment;
using Umbraco.NetPayment.API;
using Umbraco.NetPayment.Helpers;
using Umbraco.Web;
using Umbraco.Web.Security;

namespace Ekom.Extensions.Services
{
    /// <summary>
    /// Consolidates behaviors for the standard Ekom Checkout Surface and Web Api Controllers
    /// </summary>
    public class CheckoutControllerService
    {
#pragma warning disable CA1062 // Supplied by Ekom Controller

        /// <summary>
        /// Appended after error redirect
        /// </summary>
        protected virtual string ErrorQueryString { get; set; } = "serverError";

        protected readonly ILogger Logger;
        protected readonly Configuration Config;
        protected readonly IScopeProvider ScopeProvider;
        protected readonly HttpContextBase HttpContext;
        protected readonly UmbracoHelper UmbracoHelper;
        protected readonly MembershipHelper MembershipHelper;

        protected string Culture;

        public CheckoutControllerService(
            ILogger logger,
            Configuration config,
            IScopeProvider scopeProvider,
            UmbracoHelper umbracoHelper,
            MembershipHelper membershipHelper,
            HttpContextBase httpContext)
        {
            Logger = logger;
            Config = config;
            ScopeProvider = scopeProvider;
            UmbracoHelper = umbracoHelper;
            MembershipHelper = membershipHelper;
            HttpContext = httpContext;
        }

        internal async Task<T> PayAsync<T>(Func<CheckoutResponse, T> responseHandler, PaymentRequest paymentRequest, string culture)
        {
            Logger.Debug<CheckoutControllerService>("Pay - Payment request start");

            Culture = culture;

            var res = await PrepareCheckoutAsync(paymentRequest).ConfigureAwait(false);
            if (res != null)
            {
                return responseHandler(res);
            }

            // ToDo: Lock order throughout request
            var order = await Order.Instance.GetOrderAsync().ConfigureAwait(false);
            var storeAlias = order.StoreInfo.Alias;
            IStore store = Store.Instance.GetStore(storeAlias);

            res = await ValidationAndOrderUpdatesAsync(paymentRequest, order, HttpContext.Request.Form)
                .ConfigureAwait(false);
            if (res != null)
            {
                return responseHandler(res);
            }

            // Reset hangfire jobs in cases were user cancels on payment page and changes cart f.x.
            if (order.HangfireJobs.Any())
            {
                foreach (var job in order.HangfireJobs)
                {
                    await Stock.Instance.RollbackJobAsync(job).ConfigureAwait(false);
                }

                await Order.Instance.RemoveHangfireJobsFromOrderAsync(storeAlias).ConfigureAwait(false);
            }

            var hangfireJobs = new List<string>();
            res = await ProcessOrderLinesAsync(paymentRequest, order, hangfireJobs).ConfigureAwait(false);
            if (res != null)
            {
                return responseHandler(res);
            }

            res = await ProcessCouponsAsync(paymentRequest, order, hangfireJobs).ConfigureAwait(false);
            if (res != null)
            {
                return responseHandler(res);
            }

            // save job ids to sql for retrieval after checkout completion
            await Order.Instance.AddHangfireJobsToOrderAsync(hangfireJobs).ConfigureAwait(false);

            var orderTitle = await CreateOrderTitleAsync(paymentRequest, order, store)
                .ConfigureAwait(false);

            var result = await ProcessPaymentAsync(paymentRequest, order, orderTitle)
                .ConfigureAwait(false);

            return responseHandler(result);
        }

        protected virtual Task<CheckoutResponse> PrepareCheckoutAsync(PaymentRequest paymentRequest)
        {
            return Task.FromResult<CheckoutResponse>(null);
        }

        protected virtual async Task<CheckoutResponse> ValidationAndOrderUpdatesAsync(
            PaymentRequest paymentRequest,
            IOrderInfo order,
            NameValueCollection form)
        {
            if (paymentRequest == null)
            {
                return new CheckoutResponse
                {
                    HttpStatusCode = 400,
                };
            }

            if (form.AllKeys.Contains("ekomUpdateInformation"))
            {
                var formCollection = form.AllKeys.ToDictionary(
                        k => k,
                        v => AntiXssEncoder.HtmlEncode(form.Get(v), false)
                    );

                if (!formCollection.ContainsKey("storeAlias"))
                {
                    formCollection.Add("storeAlias", order.StoreInfo.Alias);
                }

                await Order.Instance.UpdateCustomerInformationAsync(formCollection).ConfigureAwait(false);
            }

            if (order.PaymentProvider == null)
            {
                await Order.Instance.UpdatePaymentInformationAsync(
                    paymentRequest.PaymentProvider,
                    order.StoreInfo.Alias).ConfigureAwait(false);
            }

            if (Config.StoreCustomerData)
            {
                using (var db = ScopeProvider.CreateScope().Database)
                {
                    await db.InsertAsync(new CustomerData
                    {
                        // Unfinished
                    }).ConfigureAwait(false);
                }
            }

            if (string.IsNullOrEmpty(order.CustomerInformation.Customer.Name)
            || string.IsNullOrEmpty(order.CustomerInformation.Customer.Email))
            {
                return new CheckoutResponse
                {
                    HttpStatusCode = 400,
                };
            }

            return null;
        }

        /// <summary>
        /// Optionally return an ActionResult to immediately return a specified response
        /// </summary>
        /// <returns>Optionally return an ActionResult to immediately return a specified response</returns>
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected async virtual Task<CheckoutResponse> ProcessOrderLinesAsync(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            PaymentRequest paymentRequest,
            IOrderInfo order,
            ICollection<string> hangfireJobs)
        {
            #region Stock

            try
            {
                // Only validate, remove stock in CheckoutService
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
                Logger.Error<CheckoutControllerService>(ex, "Not Enough Stock Exception");
                if (ex.Variant.HasValue && ex.OrderLineKey != default)
                {
                    var type = ex.Variant.Value ? "variant" : "product";
                    return new CheckoutResponse
                    {
                        ResponseBody = new StockError
                        {
                            IsVariant = ex.Variant.Value,
                            OrderLineKey = ex.OrderLineKey,
                        },
                        HttpStatusCode = 530,
                    };
                }

                return new CheckoutResponse
                {
                    ResponseBody = new StockError
                    {
                    },
                    HttpStatusCode = 530,
                };
            }
            catch (NotEnoughStockException ex)
            {
                Logger.Error<CheckoutControllerService>(ex, "Not Enough Stock Exception");
                return new CheckoutResponse
                {
                    ResponseBody = new StockError
                    {
                    },
                    HttpStatusCode = 530,
                };
            }

            #endregion

            return null;
        }

        /// <summary>
        /// Not yet implemented by default
        /// </summary>
        /// <returns>Optionally return an ActionResult to immediately return a specified response</returns>
        protected virtual Task<CheckoutResponse> ProcessCouponsAsync(
            PaymentRequest paymentRequest,
            IOrderInfo orderInfo,
            ICollection<string> hangfireJobs)
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

            return Task.FromResult<CheckoutResponse>(null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected virtual Task<string> CreateOrderTitleAsync(PaymentRequest paymentRequest, IOrderInfo order, IStore store)
        {
            string orderTitle = "Pöntun";

            if (store != null)
            {
                var paymentOrderTitle = store.GetPropertyValue("paymentOrderTitle");

                if (!string.IsNullOrEmpty(paymentOrderTitle))
                {
                    if (paymentOrderTitle.Substring(0, 1) == "#")
                    {
                        var dictionaryValue
                            = UmbracoHelper.GetDictionaryValue(paymentOrderTitle.Substring(1));

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
        protected async virtual Task<CheckoutResponse> ProcessPaymentAsync(
            PaymentRequest paymentRequest,
            IOrderInfo order,
            string orderTitle)
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

            Logger.Info<CheckoutControllerService>(
                "Payment Provider: {PaymentProvider} offline: {isOfflinePayment}",
                paymentRequest.PaymentProvider,
                isOfflinePayment);

            if (isOfflinePayment)
            {
                try
                {
                    var successUrl = URIHelper.EnsureFullUri(
                        ekomPP.GetPropertyValue("successUrl", storeAlias),
                        HttpContext.Request)
                        + "?orderId=" + order.UniqueId;

                    await Order.Instance.UpdateStatusAsync(
                        Ekom.Utilities.OrderStatus.OfflinePayment,
                        order.UniqueId).ConfigureAwait(false);

                    LocalCallback.OnSuccess(new Umbraco.NetPayment.OrderStatus()
                    {
                        Member = MembershipHelper.GetCurrentMemberId(),
                        PaymentProviderKey = ekomPP.Key,
                        PaymentProvider = ekomPP.Name,
                        Custom = order.UniqueId.ToString()
                    });

                    return new CheckoutResponse
                    {
                        ResponseBody = successUrl,
                        HttpStatusCode = 300,
                    };
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Logger.Error<CheckoutControllerService>(
                        ex,
                        "Offline Payment Failed. Order: {UniqueId}",
                        order.UniqueId);

                    var errorUrl = URIHelper.EnsureFullUri(
                        ekomPP.GetPropertyValue("errorUrl", storeAlias),
                        HttpContext.Request);

                    return new CheckoutResponse
                    {
                        ResponseBody = errorUrl,
                        HttpStatusCode = 300,
                    };
                }
            }
            else
            {
                await Order.Instance.UpdateStatusAsync(
                    Ekom.Utilities.OrderStatus.WaitingForPayment,
                    order.UniqueId).ConfigureAwait(false);

                var pp = NetPayment.Instance.GetPaymentProvider(ekomPP.Name);

                var language = !string.IsNullOrEmpty(ekomPP.GetPropertyValue("language", order.StoreInfo.Alias)) ? ekomPP.GetPropertyValue("language", order.StoreInfo.Alias) : "IS";

                var content = await pp.RequestAsync(new PaymentSettings
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
                    Member = MembershipHelper.GetCurrentMemberId(),
                    OrderCustomString = order.UniqueId.ToString(),
                    //paymentProviderId: paymentRequest.PaymentProvider.ToString()
                }).ConfigureAwait(false);

                return new CheckoutResponse
                {
                    ResponseBody = content,
                    HttpStatusCode = 230,
                };
            }
        }
#pragma warning restore CA1062 // Validate arguments of public methods
    }
}
