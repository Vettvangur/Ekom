using Ekom.API;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Payments;
using Ekom.Payments.Helpers;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using OrderStatus = Ekom.Utilities.OrderStatus;

namespace Ekom.Services;

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
    protected readonly EkomPayments ekomPayments;

    readonly HttpContext _httpCtx;
    protected string Culture;
    readonly IServiceProvider _factory;

    public CheckoutControllerService(
        ILogger logger,
        Configuration config,
        DatabaseFactory databaseFactory,
        IUmbracoService umbracoService,
        IMemberService memberService,
        IHttpContextAccessor httpContextAccessor,
        EkomPayments ekomPayments,

        IServiceProvider factory)
    {
        _httpCtx = httpContextAccessor.HttpContext;

        Logger = logger;
        Config = config;
        DatabaseFactory = databaseFactory;
        UmbracoService = umbracoService;
        MemberService = memberService;
        this.ekomPayments = ekomPayments;
        _factory = factory;
        //HttpContext = httpContext;
    }

    internal async Task<T> PayAsync<T>(Func<CheckoutResponse, T> responseHandler, PaymentRequest paymentRequest, string culture, CancellationToken ct)
    {
        Logger.LogInformation("Checkout Pay - Payment request start ");

        Culture = culture;

        if (!string.IsNullOrEmpty(Culture))
        {
            var cultureInfo = new CultureInfo(Culture);

            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
        }

        if (!string.IsNullOrEmpty(paymentRequest.Culture))
        {
            var cultureInfo = new CultureInfo(paymentRequest.Culture);

            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
        }

        // ToDo: Lock order throughout request
        var order = await Order.Instance.GetOrderAsync(paymentRequest.StoreAlias, ct).ConfigureAwait(false);

        if (order == null)
        {
            throw new ArgumentNullException($"Order could not be found in store {paymentRequest.StoreAlias}");
        }

        order = await UpdateOrderDateAsync(paymentRequest.AdditionalData, order, paymentRequest.PaymentProvider, paymentRequest.ShippingProvider).ConfigureAwait(false);

        var res = await PrepareCheckoutAsync(paymentRequest, order, ct: ct).ConfigureAwait(false);

        if (res != null)
        {
            return responseHandler(res);
        }

        Logger.LogInformation("Checkout Pay - Order:  " + order.UniqueId + " Customer: " + +order.CustomerInformation.Customer.UserId
            + " ," + order.CustomerInformation.Customer.UserName + " Payment Provider: " + paymentRequest.PaymentProvider);

        string storeAlias = order.StoreInfo.Alias;
        var store = API.Store.Instance.GetStore(storeAlias);

        res = await ValidationAndOrderUpdatesAsync(
            paymentRequest,
            order,
            ct)
            .ConfigureAwait(false);

        if (res != null)
        {
            return responseHandler(res);
        }

        // Reset hangfire jobs in cases where user cancels on payment page and changes cart f.x.
        if (order.HangfireJobs.Any())
        {
            foreach (string job in order.HangfireJobs)
            {
                await Stock.Instance.RollbackJobAsync(job, ct).ConfigureAwait(false);
            }

            await Order.Instance.RemoveHangfireJobsFromOrderAsync(storeAlias).ConfigureAwait(false);
        }

        List<string> hangfireJobs = new List<string>();
        res = await ProcessOrderLinesAsync(paymentRequest, order, hangfireJobs, ct).ConfigureAwait(false);

        if (res != null)
        {
            return responseHandler(res);
        }

        res = await ProcessCouponsAsync(paymentRequest, order, hangfireJobs, ct).ConfigureAwait(false);

        if (res != null)
        {
            return responseHandler(res);
        }

        // save job ids to sql for retrieval after checkout completion
        await Order.Instance.AddHangfireJobsToOrderAsync(hangfireJobs, order, order.StoreInfo.Alias, ct:ct).ConfigureAwait(false);

        string orderTitle = await CreateOrderTitleAsync(paymentRequest, order, store, ct)
            .ConfigureAwait(false);
        CheckoutResponse result = await ProcessPaymentAsync(paymentRequest, order, orderTitle, ct)
            .ConfigureAwait(false);
        return responseHandler(result);
    }


    public async Task<CheckoutResponse> PayAsync(PaymentRequest paymentRequest, string culture, Guid orderId, CancellationToken ct = default)
    {

        var order = await Order.Instance.GetOrderAsync(orderId, ct).ConfigureAwait(false);

        if (order == null)
        {
            throw new ArgumentNullException($"Order could not be found in store {paymentRequest.StoreAlias}");
        }

        return await PayAsync(paymentRequest, culture, order, ct: ct).ConfigureAwait(false);
    }

    public async Task<CheckoutResponse> PayAsync(PaymentRequest paymentRequest, string culture, IOrderInfo order, CancellationToken ct = default)
    {
        Logger.LogInformation("Checkout Pay - Payment request start ");

        if (!string.IsNullOrEmpty(Culture))
        {
            CultureInfo cultureInfo = new CultureInfo(Culture);

            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
        }

        if (!string.IsNullOrEmpty(paymentRequest.Culture))
        {
            CultureInfo cultureInfo = new CultureInfo(paymentRequest.Culture);

            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
        }

        if (order == null)
        {
            throw new ArgumentNullException($"Order could not be found in store {paymentRequest.StoreAlias}");
        }

        order = await UpdateOrderDateAsync(paymentRequest.AdditionalData, order, ct: ct).ConfigureAwait(false);

        CheckoutResponse? res = await PrepareCheckoutAsync(paymentRequest, order, ct).ConfigureAwait(false);

        Logger.LogInformation("Checkout Pay - Order:  " + order.UniqueId + " Customer: " + +order.CustomerInformation.Customer.UserId
            + " ," + order.CustomerInformation.Customer.UserName + " Payment Provider: " + paymentRequest.PaymentProvider);

        string storeAlias = order.StoreInfo.Alias;
        IStore? store = API.Store.Instance.GetStore(storeAlias);

        res = await ValidationAndOrderUpdatesAsync(
            paymentRequest,
            order, 
            ct)
            .ConfigureAwait(false);

        if (order.HangfireJobs.Any())
        {
            foreach (string job in order.HangfireJobs)
            {
                await Stock.Instance.RollbackJobAsync(job, ct).ConfigureAwait(false);
            }

            await Order.Instance.RemoveHangfireJobsFromOrderAsync(storeAlias, ct: ct).ConfigureAwait(false);
        }

        var hangfireJobs = new List<string>();

        res = await ProcessOrderLinesAsync(paymentRequest, order, hangfireJobs, ct: ct).ConfigureAwait(false);

        res = await ProcessCouponsAsync(paymentRequest, order, hangfireJobs, ct: ct).ConfigureAwait(false);

        // save job ids to sql for retrieval after checkout completion
        await Order.Instance.AddHangfireJobsToOrderAsync(hangfireJobs, order, storeAlias).ConfigureAwait(false);

        string orderTitle = await CreateOrderTitleAsync(paymentRequest, order, store, ct: ct)
            .ConfigureAwait(false);
        CheckoutResponse result = await ProcessPaymentAsync(paymentRequest, order, orderTitle, ct: ct)
            .ConfigureAwait(false);

        return result;
    }

    protected virtual Task<CheckoutResponse?> PrepareCheckoutAsync(PaymentRequest paymentRequest, IOrderInfo? orderInfo, CancellationToken ct)
    {
        return Task.FromResult<CheckoutResponse?>(null);
    }

    protected virtual async Task<IOrderInfo> UpdateOrderDateAsync(Dictionary<string, string> collection, IOrderInfo order, Guid? paymentProviderKey = null, Guid? shippingProviderKey = null, CancellationToken ct = default)
    {
        Dictionary<string, string> formCollection = collection;

        if (formCollection.Keys.Contains("ekomUpdateInformation", StringComparer.OrdinalIgnoreCase))
        {
            bool saveCustomerData = false;

            // Ensure storeAlias is present
            if (!formCollection.Keys.Any(k => string.Equals(k, "storeAlias", StringComparison.OrdinalIgnoreCase)))
            {
                formCollection.Add("storeAlias", order.StoreInfo.Alias);
                saveCustomerData = true;
            }

            // Try to prefill customerName and customerEmail from member if missing
            bool needsCustomerName = !formCollection.ContainsKey("customerName") && string.IsNullOrEmpty(order.CustomerInformation.Customer.Name);
            bool needsCustomerEmail = !formCollection.ContainsKey("customerEmail") && string.IsNullOrEmpty(order.CustomerInformation.Customer.Email);

            if ((needsCustomerName || needsCustomerEmail) && order.CustomerInformation.Customer.UserId != 0)
            {
                var member = MemberService.GetByUsername(order.CustomerInformation.Customer.UserName);

                if (member != null)
                {
                    if (needsCustomerName && !string.IsNullOrEmpty(member.Name))
                    {
                        formCollection.Add("customerName", member.Name);
                    }

                    if (needsCustomerEmail && !string.IsNullOrEmpty(member.Email))
                    {
                        formCollection.Add("customerEmail", member.Email);
                    }
                }
            }

            // Check if any customer or shipping fields were submitted
            if (formCollection.Keys.Any(k =>
                    k.StartsWith("customer", StringComparison.OrdinalIgnoreCase) ||
                    k.StartsWith("shipping", StringComparison.OrdinalIgnoreCase)))
            {
                saveCustomerData = true;
            }

            if (saveCustomerData)
            {
                order = await Order.Instance.UpdateCustomerInformationAsync(formCollection, ct: ct).ConfigureAwait(false);
            }
        }

        if (paymentProviderKey.HasValue)
        {

            if (order.PaymentProvider == null || (order.PaymentProvider != null && order.PaymentProvider.Key != paymentProviderKey.Value))
            {
                order = await Order.Instance.UpdatePaymentInformationAsync(
                paymentProviderKey.Value,
                order.StoreInfo.Alias, formCollection).ConfigureAwait(false);
            }
        }

        if (shippingProviderKey.HasValue)
        {
            if (order.ShippingProvider == null || (order.ShippingProvider != null && order.ShippingProvider.Key != shippingProviderKey.Value))
            {
                order = await Order.Instance.UpdateShippingInformationAsync(
                shippingProviderKey.Value,
                order.StoreInfo.Alias, formCollection).ConfigureAwait(false);
            }
        }

        return order;
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    protected virtual async Task<CheckoutResponse?> ValidationAndOrderUpdatesAsync(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    PaymentRequest paymentRequest,
    IOrderInfo order,
    CancellationToken ct)
    {
        if (paymentRequest == null)
        {

            Logger.LogError("ValidationAndOrderUpdatesAsync Failed. PaymentRequest is Null. " + (order != null ? order.UniqueId.ToString() : ""));

            return new CheckoutResponse
            {
                ResponseBody = "PaymentRequest is Null",
                HttpStatusCode = 400,
            };
        }

        //if (Config.StoreCustomerData)
        //{
        //    await using Repositories.DbContext db = DatabaseFactory.GetDatabase();

        //    await db.InsertAsync(new CustomerData
        //    {
        //        // Unfinished
        //    }).ConfigureAwait(false);
        //}

        if (!string.IsNullOrEmpty(order.CustomerInformation.Customer.Name)
            && !string.IsNullOrEmpty(order.CustomerInformation.Customer.Email)) return null;

        Logger.LogWarning("ValidationAndOrderUpdatesAsync Failed. Name or Email is empty. " + (order != null ? order.UniqueId.ToString() : ""));

        return new CheckoutResponse
        {
            ReturnUrl = paymentRequest.ReturnUrl,
            HttpStatusCode = 400,
        };

    }

    /// <summary>
    /// Optionally return an ActionResult to immediately return a specified response
    /// </summary>
    /// <returns>Optionally return an ActionResult to immediately return a specified response</returns>
    protected async virtual Task<CheckoutResponse?> ProcessOrderLinesAsync(
        PaymentRequest paymentRequest,
        IOrderInfo order,
        ICollection<string> hangfireJobs,
        CancellationToken ct = default)
    {
        #region Stock

        CheckoutEvents.OnProcessing(this, new ProcessingEventArgs
        {
            OrderInfo = order
        });

        var proccessingEventArgs = new ProcessingEventArgs
        {
            OrderInfo = order
        };

        await CheckoutEvents.OnProcessingAsync(this, proccessingEventArgs, ct);

        try
        {
            // Only validate, remove stock in CheckoutService
            if (proccessingEventArgs.StockValidation)
            {
                await Stock.Instance.ValidateOrderStockAsync(order, ct);
            }
        }
        catch (NotEnoughLineStockException ex)
        {
            Logger.LogError(ex, "Not Enough Stock Exception. Orderline: " + ex.OrderLineKey + " Variant: " + ex.Variant);

            if (ex.Variant.HasValue && ex.OrderLineKey != default)
            {
                string type = ex.Variant.Value ? "variant" : "product";
                return new CheckoutResponse
                {
                    ReturnUrl = paymentRequest.ReturnUrl,
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
                ReturnUrl = paymentRequest.ReturnUrl,
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
                ReturnUrl = paymentRequest.ReturnUrl,
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
        ICollection<string> hangfireJobs, 
        CancellationToken ct)
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
    protected virtual Task<string> CreateOrderTitleAsync(PaymentRequest paymentRequest, IOrderInfo order, IStore store, CancellationToken ct)
    {
        string orderTitle = "Pöntun";

        if (store == null) return Task.FromResult(orderTitle += " - " + order.OrderNumber);

        string paymentOrderTitle = store.GetValue("paymentOrderTitle");

        if (string.IsNullOrEmpty(paymentOrderTitle))
            return Task.FromResult(orderTitle += " - " + order.OrderNumber);

        if (paymentOrderTitle.Substring(0, 1) == "#")
        {
            string dictionaryValue
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

        return Task.FromResult(orderTitle += " - " + order.OrderNumber);
    }

    /// <summary>
    /// Optionally return an ActionResult to immediately return a specified response.
    /// </summary>
    /// <returns>Optionally return an ActionResult to immediately return a specified response</returns>
    protected async virtual Task<CheckoutResponse> ProcessPaymentAsync(
        PaymentRequest paymentRequest,
        IOrderInfo order,
        string orderTitle, 
        CancellationToken ct)
    {

        if (order == null)
        {
            throw new ArgumentNullException("Order is missing from ProcessPaymentAsync. " + orderTitle);
        }

        if (_httpCtx == null)
        {
            throw new ArgumentNullException("Httpcontext is missing from ProcessPaymentAsync. " + order.UniqueId + " Store: " + order.StoreInfo.Alias);
        }

        if (order.PaymentProvider == null)
        {
            throw new ArgumentNullException("PaymentProvider is missing from Order. " + order.UniqueId + " Store: " + order.StoreInfo.Alias);
        }

        string storeAlias = order.StoreInfo.Alias;

        var ekomPP = await Providers.Instance.GetPaymentProviderAsync(order.PaymentProvider.Key, storeAlias, ct: ct);

        if (ekomPP == null)
        {
            throw new ArgumentNullException("Payment provider is missing from ProcessPaymentAsync. " + order.UniqueId + " Provider: " + paymentRequest.PaymentProvider + " Store: " + order.StoreInfo.Alias);
        }

        bool isOfflinePayment = ekomPP.GetValue("offlinePayment", storeAlias).IsBoolean();

        var amount = order.ChargedAmount.Value;

        var orderItems = new List<OrderItem>
        {
            new OrderItem
            {
                GrandTotal = amount,
                Price = amount,
                Title = orderTitle,
                Quantity = 1,
            }
        };

        Logger.LogInformation(
            "Payment Provider: {PaymentProvider}, {Name} offline: {isOfflinePayment}",
            order.PaymentProvider.Key,
            ekomPP.Name,
            isOfflinePayment);

        var paymentErrorUrl = ResolvePaymentProviderUrl(ekomPP, "errorUrl", order, paymentRequest.Culture);

        var paymentSuccessUrl = ResolvePaymentProviderUrl(ekomPP, "successUrl", order, paymentRequest.Culture);

        var GetEncodedUrl = _httpCtx.Request.GetEncodedUrl();

        var errorUrl = Utilities.UriHelper.EnsureFullUri(
        paymentErrorUrl,
        new Uri(GetEncodedUrl));

        if (isOfflinePayment)
        {
            try
            {

                string successUrl = Utilities.UriHelper.EnsureFullUri(
                    paymentSuccessUrl,
                    new Uri(GetEncodedUrl))
                + "?orderId=" + order.UniqueId;

                await Order.Instance.UpdateStatusAsync(
                    OrderStatus.OfflinePayment,
                    order.UniqueId, ct: ct).ConfigureAwait(false);

                string? memberKey = _httpCtx.User.Identity != null ? _httpCtx.User.Identity.IsAuthenticated ? (await MemberService.GetCurrentMember())?.Key.ToString() : "" : "";

                PayEventArgs eventsArgs = new PayEventArgs
                {
                    OrderInfo = order,
                    PaymentSettings = new PaymentSettings()
                    {
                        SuccessUrl = new Uri(successUrl),
                        ErrorUrl = new Uri(errorUrl),
                        PaymentProviderKey = ekomPP.Key,
                        PaymentProviderName = ekomPP.Name,
                        OrderUniqueId = order.UniqueId,
                        Orders = orderItems,
                        OrderNumber =  order.OrderNumber
                    },
                };

                CheckoutEvents.OnPay(this, eventsArgs);
                await CheckoutEvents.OnPayAsync(this, eventsArgs, ct);

                errorUrl = eventsArgs.PaymentSettings.ErrorUrl.ToString();

                CheckoutService checkoutSvc = _factory.GetRequiredService<CheckoutService>();

                await checkoutSvc.CompleteAsync(order.UniqueId, ct);

                return new CheckoutResponse
                {
                    ResponseBody = eventsArgs.PaymentSettings.SuccessUrl.ToString(),
                    HttpStatusCode = 300,
                };
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Logger.LogError(
                    ex,
                    "Offline Payment Failed. Order: {UniqueId} Store: {Alias}",
                    order.UniqueId, order.StoreInfo.Alias);

                await Order.Instance.UpdateStatusAsync(
                    OrderStatus.PaymentFailed,
                    order.UniqueId, ct: ct).ConfigureAwait(false);

                throw;
            }
        }
        else
        {
            await Order.Instance.UpdateStatusAsync(
                OrderStatus.WaitingForPayment,
                order.UniqueId, ct: ct).ConfigureAwait(false);

            Uri paymentReturnErrorUrl = new(BuildPaymentReturnUrl(order.UniqueId, "error"));
            Uri paymentReturnCancelUrl = new(BuildPaymentReturnUrl(order.UniqueId, "cancel"));

            Uri successUrl = PaymentsUriHelper.EnsureFullUri(
                ResolvePaymentProviderUrl(ekomPP, "successUrl", order, paymentRequest.Culture),
                _httpCtx.Request);
            successUrl = PaymentsUriHelper.AddQueryString(
                successUrl,
                "?orderId=" + order.UniqueId
            );

            var basePaymentProvider = string.IsNullOrEmpty(ekomPP.GetValue("basePaymentProvider")) ? ekomPP.Name : ekomPP.GetValue("basePaymentProvider");

            Payments.IPaymentProvider pp = ekomPayments.GetPaymentProvider(basePaymentProvider);

            var language = !string.IsNullOrEmpty(ekomPP.GetValue("language", order.StoreInfo.Alias))
                ? ekomPP.GetValue("language", order.StoreInfo.Alias)
                : "is-IS";

            if (!Enum.TryParse(order.StoreInfo.Currency.ISOCurrencySymbol, out Currency currency))
            {
                Logger.LogError("Could not parse currency to Enum. Currency not found in Umbraco.NetPayment.Currency. " + order.StoreInfo.Currency.ISOCurrencySymbol);
            }

            UmbracoMember currentMember = await MemberService.GetCurrentMember();

            var paymentSettings = new PaymentSettings
            {
                CustomerInfo = new Ekom.Payments.CustomerInfo()
                {
                    Address = order.CustomerInformation.Customer.Address,
                    City = order.CustomerInformation.Customer.City,
                    Email = order.CustomerInformation.Customer.Email,
                    Name = order.CustomerInformation.Customer.Name,
                    NationalRegistryId = order.CustomerInformation.Customer.Properties.GetValue("customerSsn"),
                    PhoneNumber = order.CustomerInformation.Customer.Phone,
                    PostalCode = order.CustomerInformation.Customer.ZipCode
                },
                CardNumber = paymentRequest.CardNumber,
                CardExpirationMonth = paymentRequest.Month.HasValue ? paymentRequest.Month.Value : 0,
                CardExpirationYear = paymentRequest.Year.HasValue ? paymentRequest.Year.Value : 0,
                CardCVV = paymentRequest.CVV,
                SuccessUrl = successUrl,
                ErrorUrl = paymentReturnErrorUrl,
                CancelUrl = paymentReturnCancelUrl,
                Currency = currency.ToString(),
                Orders = orderItems,
                Language = language,
                Store = storeAlias,
                Member = currentMember?.Key,
                PaymentProviderKey = ekomPP.Key,
                OrderUniqueId = order.UniqueId,
                OrderNumber = order.OrderNumber
            };

            paymentSettings.OrderCustomData.Add("ekomOrderUniqueId", order.UniqueId.ToString());
            paymentSettings.OrderCustomData.Add("ekomOrderReferenceId", order.ReferenceId.ToString());

            CheckoutEvents.OnPay(this, new PayEventArgs
            {
                OrderInfo = order,
                PaymentSettings = paymentSettings,
            });

            await CheckoutEvents.OnPayAsync(this, new PayEventArgs
            {
                OrderInfo = order,
                PaymentSettings = paymentSettings,
            }, ct: ct);

            string content = await pp.RequestAsync(paymentSettings).ConfigureAwait(false);

            return new CheckoutResponse
            {
                ResponseBody = content,
                HttpStatusCode = 230,
            };
        }
    }

    internal virtual string ResolvePaymentProviderUrl(
        Models.IPaymentProvider paymentProvider,
        string propertyAlias,
        IOrderInfo order,
        string? culture = null)
    {
        ArgumentNullException.ThrowIfNull(paymentProvider);
        ArgumentNullException.ThrowIfNull(order);

        var resolvedCulture = NullIfWhiteSpace(culture)
            ?? NullIfWhiteSpace(order.Culture)
            ?? NullIfWhiteSpace(order.StoreInfo.Culture);
        var storeAlias = order.StoreInfo.Alias;

        var value = ResolvePaymentProviderPropertyValue(
            paymentProvider,
            propertyAlias,
            resolvedCulture,
            storeAlias);

        return ResolveContentPickerUrl(value, resolvedCulture);
    }

    private static string ResolvePaymentProviderPropertyValue(
        Models.IPaymentProvider paymentProvider,
        string propertyAlias,
        string? culture,
        string storeAlias)
    {
        var rawValue = paymentProvider.GetRawValue(propertyAlias);
        var editorType = TryGetPropertyEditorType(rawValue);

        if (editorType == PropertyEditorType.Language)
        {
            return ResolvePropertyValue(rawValue, culture)
                ?? ResolvePropertyValue(rawValue, storeAlias)
                ?? ResolvePropertyValue(rawValue, null, fallback: true)
                ?? string.Empty;
        }

        if (editorType == PropertyEditorType.Store)
        {
            return ResolvePropertyValue(rawValue, storeAlias)
                ?? ResolvePropertyValue(rawValue, culture)
                ?? ResolvePropertyValue(rawValue, null, fallback: true)
                ?? string.Empty;
        }

        return ResolvePropertyValue(rawValue, culture)
            ?? ResolvePropertyValue(rawValue, storeAlias)
            ?? ResolvePropertyValue(rawValue, null, fallback: true)
            ?? string.Empty;
    }

    private static PropertyEditorType? TryGetPropertyEditorType(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || !rawValue.IsJson())
            return null;

        try
        {
            var node = JsonNode.Parse(rawValue);
            var type = node?["type"]?.GetValue<string>();

            return Enum.TryParse<PropertyEditorType>(type, ignoreCase: true, out var editorType)
                ? editorType
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ResolvePropertyValue(string? rawValue, string? alias, bool fallback = false)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        var value = rawValue.GetEkomPropertyEditorValue(alias ?? string.Empty, fallback);

        return NullIfWhiteSpace(value);
    }

    private string ResolveContentPickerUrl(string value, string? culture)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var pickerValue = TryGetContentPickerValue(value) ?? value;

        if (!LooksLikeContentPickerValue(pickerValue))
            return value;

        var nodeService = _factory.GetService<INodeService>();
        var url = nodeService?.GetUrl(pickerValue, culture ?? string.Empty);

        return !string.IsNullOrWhiteSpace(url) && url != "#"
            ? url
            : value;
    }

    private static string? TryGetContentPickerValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.IsJson())
            return null;

        try
        {
            var node = JsonNode.Parse(value);

            if (node is JsonArray array)
                return array.Select(TryGetContentPickerValue).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            return TryGetContentPickerValue(node);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryGetContentPickerValue(JsonNode? node)
    {
        if (node is null)
            return null;

        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var value))
            return NullIfWhiteSpace(value);

        if (node is not JsonObject obj)
            return null;

        foreach (var key in new[] { "udi", "value", "key", "id" })
        {
            if (obj.TryGetPropertyValue(key, out var propertyValue))
            {
                var propertyStringValue = TryGetContentPickerValue(propertyValue);
                if (!string.IsNullOrWhiteSpace(propertyStringValue))
                    return propertyStringValue;
            }
        }

        return null;
    }

    private static bool LooksLikeContentPickerValue(string value)
    {
        return value.StartsWith("umb://document/", StringComparison.OrdinalIgnoreCase)
            || Guid.TryParse(value, out _)
            || int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string BuildPaymentReturnUrl(Guid orderId, string outcome)
    {
        string baseUrl = $"{_httpCtx.Request.Scheme}://{_httpCtx.Request.Host}{_httpCtx.Request.PathBase}/ekom/checkout/payment-return";

        return QueryHelpers.AddQueryString(baseUrl, new Dictionary<string, string?>
        {
            ["orderId"] = orderId.ToString(),
            ["outcome"] = outcome
        });
    }

#pragma warning restore CA1062 // Validate arguments of public methods
}
