using Ekom.API;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace Ekom.Services
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

        protected readonly DatabaseFactory DatabaseFactory;
        protected readonly IUmbracoService UmbracoService;
        protected readonly IMemberService MemberService;
        protected readonly ILogger Logger;
        protected readonly Configuration Config;
        protected readonly INetPaymentService netPaymentService;
        readonly HttpContext _httpCtx;
        protected string Culture;

        public CheckoutControllerService(
            ILogger logger,
            Configuration config,
            DatabaseFactory databaseFactory,
            IUmbracoService umbracoService,
            IMemberService memberService,
            IHttpContextAccessor httpContextAccessor,
            INetPaymentService netPaymentService)
        {
            _httpCtx = httpContextAccessor.HttpContext;

            Logger = logger;
            Config = config;
            DatabaseFactory = databaseFactory;
            UmbracoService = umbracoService;
            MemberService = memberService;
            this.netPaymentService = netPaymentService;
            //HttpContext = httpContext;
        }

        internal async Task<T> PayAsync<T>(Func<CheckoutResponse, T> responseHandler, PaymentRequest paymentRequest, string culture)
        {
            Logger.LogInformation("Checkout Pay - Payment request start ");

            Culture = culture;

            var res = await PrepareCheckoutAsync(paymentRequest).ConfigureAwait(false);
            if (res != null)
            {
                return responseHandler(res);
            }

            // ToDo: Lock order throughout request
            var order = await Order.Instance.GetOrderAsync().ConfigureAwait(false);

            Logger.LogInformation("Checkout Pay - Order:  " + order.UniqueId + " Customer: " + +order.CustomerInformation.Customer.UserId 
                + " ," + order.CustomerInformation.Customer.UserName + " Payment Provider: " + paymentRequest.PaymentProvider);

            var storeAlias = order.StoreInfo.Alias;
            IStore store = API.Store.Instance.GetStore(storeAlias);

            res = await ValidationAndOrderUpdatesAsync(
                paymentRequest,
                order,
                _httpCtx.Request.Form)
                .ConfigureAwait(false);
            if (res != null)
            {
                return responseHandler(res);
            }

            // Reset hangfire jobs in cases where user cancels on payment page and changes cart f.x.
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
            IFormCollection form)
        {
            if (paymentRequest == null)
            {

                Logger.LogError("ValidationAndOrderUpdatesAsync Failed. PaymentRequest is Null. " + (order != null ? order.UniqueId.ToString() : ""));

                return new CheckoutResponse
                {
                    HttpStatusCode = 400,
                };
            }

            var keys = form.Keys;

            if (keys.Contains("ekomUpdateInformation"))
            {
                var saveCustomerData = false;
                var formCollection = keys.ToDictionary(
                        k => k,
                        v => System.Text.Encodings.Web.HtmlEncoder.Default.Encode(form[v])
                    );
                
                if (!formCollection.ContainsKey("storeAlias"))
                {
                    formCollection.Add("storeAlias", order.StoreInfo.Alias);
                    saveCustomerData = true;
                }

                if (((!formCollection.ContainsKey("customerName") || !formCollection.ContainsKey("customerEmail"))) && order.CustomerInformation.Customer.UserId != 0)
                {
                    var member = MemberService.GetByUsername(order.CustomerInformation.Customer.UserName);

                    if (member != null)
                    {
                        if (!formCollection.ContainsKey("customerName") && !string.IsNullOrEmpty(member.Name))
                        {
                            formCollection.Add("customerName", member.Name);
                        }
                        if (!formCollection.ContainsKey("customerEmail") && !string.IsNullOrEmpty(member.Email))
                        {
                            formCollection.Add("customerEmail", member.Email);
                        }

                    }
                }

                if (formCollection.Any(x => x.Key.StartsWith("customer") || x.Key.StartsWith("shipping")))
                {
                    saveCustomerData = true;
                }

                if (saveCustomerData || formCollection.ContainsKey("ekomUpdateInformation"))
                {
                    order = await Order.Instance.UpdateCustomerInformationAsync(formCollection).ConfigureAwait(false);
                }
            }

            if (order.PaymentProvider == null || (order.PaymentProvider != null && order.PaymentProvider.Key != paymentRequest.PaymentProvider))
            {
                await Order.Instance.UpdatePaymentInformationAsync(
                    paymentRequest.PaymentProvider,
                    order.StoreInfo.Alias).ConfigureAwait(false);
            }

            if (order.ShippingProvider == null || (order.ShippingProvider != null && order.ShippingProvider.Key != paymentRequest.ShippingProvider))
            {
                await Order.Instance.UpdateShippingInformationAsync(
                    paymentRequest.ShippingProvider,
                    order.StoreInfo.Alias).ConfigureAwait(false);
            }

            if (Config.StoreCustomerData)
            {
                using (var db = DatabaseFactory.GetDatabase())
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
                Logger.LogWarning("ValidationAndOrderUpdatesAsync Failed. Name or Email is empty. " + (order != null ? order.UniqueId.ToString() : ""));

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

            CheckoutEvents.OnProcessing(this, new ProcessingEventArgs
            {
                OrderInfo = order
            });

            try
            {
                // Only validate, remove stock in CheckoutService
                Stock.Instance.ValidateOrderStock(order);
            }
            catch (NotEnoughLineStockException ex)
            {
                Logger.LogError(ex, "Not Enough Stock Exception. Orderline: " + ex.OrderLineKey + " Variant: " + ex.Variant);

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
                Logger.LogError(ex, "Not Enough Stock Exception");
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
                var paymentOrderTitle = store.GetValue("paymentOrderTitle");

                if (!string.IsNullOrEmpty(paymentOrderTitle))
                {
                    if (paymentOrderTitle.Substring(0, 1) == "#")
                    {
                        var dictionaryValue
                            = UmbracoService.GetDictionaryValue(paymentOrderTitle.Substring(1));

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

            Logger.LogInformation("ProcessPaymentAsync: 1");

            if (order == null)
            {
                throw new ArgumentNullException("Order is missing from ProcessPaymentAsync. " + orderTitle);
            }
            Logger.LogInformation("ProcessPaymentAsync: 2");
            if (_httpCtx == null)
            {
                throw new ArgumentNullException("Httpcontext is missing from ProcessPaymentAsync. " + order.UniqueId);
            }
            Logger.LogInformation("ProcessPaymentAsync: 3");
            var storeAlias = order.StoreInfo.Alias;
            Logger.LogInformation("ProcessPaymentAsync: 4");
            var ekomPP = Providers.Instance.GetPaymentProvider(paymentRequest.PaymentProvider);
            Logger.LogInformation("ProcessPaymentAsync: 5");
            
            if (ekomPP == null)
            {
                throw new ArgumentNullException("Payment provider is missing from ProcessPaymentAsync. " + order.UniqueId + " Provider: " + paymentRequest.PaymentProvider);
            }
            Logger.LogInformation("ProcessPaymentAsync: 6");
            var isOfflinePayment = ekomPP.GetValue("offlinePayment", storeAlias).IsBoolean();
            Logger.LogInformation("ProcessPaymentAsync: 7");
            var orderItems = new List<OrderInfo>();
            
            //orderItems.Add(new OrderInfo
            //{
            //    GrandTotal = order.ChargedAmount.Value,
            //    Price = order.ChargedAmount.Value,
            //    Title = orderTitle,
            //    Quantity = 1,
            //});

            Logger.LogInformation(
                "Payment Provider: {PaymentProvider}, {Name} offline: {isOfflinePayment}",
                paymentRequest.PaymentProvider, 
                ekomPP.Name,
                isOfflinePayment);

            var paymentErrorUrl = ekomPP.GetValue("errorUrl", storeAlias);
            
            Logger.LogInformation("ProcessPaymentAsync: 8");

            var paymentSuccessUrl = ekomPP.GetValue("successUrl", storeAlias);

            Logger.LogInformation("ProcessPaymentAsync: 9");

            var GetEncodedUrl = _httpCtx.Request.GetEncodedUrl();

            Logger.LogInformation("ProcessPaymentAsync: 10");

            var errorUrl = Utilities.UriHelper.EnsureFullUri(
            paymentErrorUrl,
            new Uri(GetEncodedUrl));

            Logger.LogInformation("ProcessPaymentAsync: 11");

            if (isOfflinePayment)
            {
                try
                {
                    Logger.LogInformation("ProcessPaymentAsync: 12");
                    var successUrl = Utilities.UriHelper.EnsureFullUri(
                        paymentSuccessUrl,
                        new Uri(GetEncodedUrl))
                    + "?orderId=" + order.UniqueId;
                    Logger.LogInformation("ProcessPaymentAsync: 13");
                    await Order.Instance.UpdateStatusAsync(
                        OrderStatus.OfflinePayment,
                        order.UniqueId).ConfigureAwait(false);
                    Logger.LogInformation("ProcessPaymentAsync: 14");
                    var memberKey = _httpCtx.User.Identity != null ? _httpCtx.User.Identity.IsAuthenticated ? MemberService.GetCurrentMember()?.Key.ToString() : "" : "";
                    Logger.LogInformation("ProcessPaymentAsync: 15");
                    try
                    {
                        Logger.LogInformation("ProcessPaymentAsync: 16");
                        netPaymentService.OnSuccess(
                            ekomPP.Key,
                            ekomPP.Name,
                            memberKey,
                            order.UniqueId.ToString());
                    }
                    catch
                    {
                        //TODO: We need to get the error url from the OnSuccess event to override it
                        //errorUrl = status.ErrorUrl;
                        throw;
                    }
                    Logger.LogInformation("ProcessPaymentAsync: 17");
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

                    Logger.LogInformation("ProcessPaymentAsync: 18");
                    Logger.LogError(
                        ex,
                        "Offline Payment Failed. Order: {UniqueId}",
                        order.UniqueId);
                    Logger.LogInformation("ProcessPaymentAsync: 19");
                    await Order.Instance.UpdateStatusAsync(
                        OrderStatus.PaymentFailed,
                        order.UniqueId).ConfigureAwait(false);
                    Logger.LogInformation("ProcessPaymentAsync: 20");
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
                    OrderStatus.WaitingForPayment,
                    order.UniqueId).ConfigureAwait(false);

                //var successUrl = Utilities.UriHelper.EnsureFullUri(
                //    ekomPP.GetPropertyValue("successUrl", storeAlias),
                //    HttpContext.Request)
                //    + "?orderId=" + order.UniqueId;

                //var pp = NetPayment.Instance.GetPaymentProvider(PublishedPaymentProviderHelper.GetName(UmbracoHelper.Content(ekomPP.Id)));

                //var language = !string.IsNullOrEmpty(ekomPP.GetPropertyValue("language", order.StoreInfo.Alias)) ? ekomPP.GetPropertyValue("language", order.StoreInfo.Alias) : "IS";

                //if (!Enum.TryParse(order.StoreInfo.Currency.ISOCurrencySymbol, out Currency currency))
                //{
                //    Logger.LogError("Could not parse currency to Enum. Currency not found in Umbraco.NetPayment.Currency. " + order.StoreInfo.Currency.ISOCurrencySymbol);
                //}
                //int loanTypeValue = 0;
                //var loanType = ekomPP.GetPropertyValue("loanType");
                //if (loanType != null)
                //{
                //    int.TryParse(loanType, out loanTypeValue);
                //}
                //string merchantName = ekomPP.GetPropertyValue("merchantName");
                //var paymentSettings = new PaymentSettings
                //{
                //    CustomerInfo = new CustomerInfo()
                //    {
                //        Address = order.CustomerInformation.Customer.Address,
                //        City = order.CustomerInformation.Customer.City,
                //        Email = order.CustomerInformation.Customer.Email,
                //        Name = order.CustomerInformation.Customer.Name,
                //        NationalRegistryId = order.CustomerInformation.Customer.Properties.GetPropertyValue("customerSsn"),
                //        PhoneNumber = order.CustomerInformation.Customer.Phone,
                //        PostalCode = order.CustomerInformation.Customer.ZipCode
                //    },
                //    CardNumber = paymentRequest.CardNumber,
                //    Expiry = paymentRequest.Expiry,
                //    CVV = paymentRequest.CVV,
                //    SuccessUrl = successUrl,
                //    Currency = currency,
                //    Orders = orderItems,
                //    SkipReceipt = true,
                //    VortoLanguage = order.StoreInfo.Alias,
                //    Language = language,
                //    Member = MembershipHelper.GetCurrentMemberId(),
                //    OrderCustomString = order.UniqueId.ToString(),
                //    LoanType = loanTypeValue,
                //    MerchantName = merchantName ?? "",
                //    ReferenceId = order.ReferenceId.ToString()
                //    //paymentProviderId: paymentRequest.PaymentProvider.ToString()
                //};

                //CheckoutEvents.OnPay(this, new PayEventArgs
                //{
                //    OrderInfo = order,
                //    PaymentSettings = paymentSettings
                //});

                //var content = await pp.RequestAsync(paymentSettings).ConfigureAwait(false);

                return new CheckoutResponse
                {
                    //ResponseBody = content,
                    HttpStatusCode = 230,
                };
            }
        }
#pragma warning restore CA1062 // Validate arguments of public methods
    }
}
