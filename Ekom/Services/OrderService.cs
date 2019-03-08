using Ekom.API;
using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.JsonDotNet;
using Ekom.Models;
using Ekom.Models.Data;
using Ekom.Models.OrderedObjects;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.Security;

namespace Ekom.Services
{
    partial class OrderService
    {
        ILog _log;

        HttpContextBase _httpCtx;
        ApplicationContext _appCtx;
        ICacheProvider _reqCache => _appCtx.ApplicationCache.RequestCache;
        DiscountCache _discountCache;

        IActivityLogRepository _activityLogRepository;
        IOrderRepository _orderRepository;
        IStoreService _storeSvc;
        ContentRequest _ekmRequest;

        IStore _store;
        DateTime _date;

        /// <summary>
        /// W/o HttpCtx, for usage in Hangfire f.x. ?
        /// </summary>
        public OrderService(
            IOrderRepository orderRepo,
            IActivityLogRepository activityLogRepository,
            ILogFactory logFac,
            IStoreService storeService,
            ApplicationContext appCtx,
            DiscountCache discountCache)
        {
            _log = logFac.GetLogger<OrderService>();

            _appCtx = appCtx;
            _orderRepository = orderRepo;
            _activityLogRepository = activityLogRepository;
            _storeSvc = storeService;
            _discountCache = discountCache;
        }

        /// <summary>
        /// ctor
        /// </summary>
        public OrderService(
            IOrderRepository orderRepo,
            IActivityLogRepository activityLogRepository,
            ILogFactory logFac,
            IStoreService storeService,
            ApplicationContext appCtx,
            DiscountCache discountCache,
            HttpContextBase httpCtx)
            : this(orderRepo, activityLogRepository, logFac, storeService, appCtx, discountCache)
        {
            _httpCtx = httpCtx;
            _ekmRequest = _reqCache.GetCacheItem("ekmRequest") as ContentRequest;
        }

        public OrderInfo GetOrder(string storeAlias)
        {
            var store = _storeSvc.GetStoreByAlias(storeAlias);

            return GetOrder(store);
        }

        public OrderInfo GetOrder(IStore store)
        {
            if (Configuration.Current.UserBasket)
            {
                if (_ekmRequest.User != null)
                {
                    var orderInfo = GetOrder(_ekmRequest.User.OrderId);
                    if (orderInfo?.OrderStatus != OrderStatus.ReadyForDispatch
                    && orderInfo?.OrderStatus != OrderStatus.Dispatched)
                    {
                        return orderInfo;
                    }
                }
            } else
            {
                _store = store;

                var key = CreateKey();
                // Get Cart UniqueId from Cookie.
                var orderUniqueId = GetOrderIdFromCookie(key);
 
                // If Cookie Exist then return Cart
                if (orderUniqueId != Guid.Empty)
                {

                    var orderInfo = ApplicationContext.Current.ApplicationCache.RuntimeCache
                                .GetCacheItem<OrderInfo>(orderUniqueId.ToString(),
                                    () => GetOrder(orderUniqueId), TimeSpan.FromDays(1));

                    //// If the cart is not in the session, fetch order from sql and insert to session
                    //if (ApplicationContext.Current.ApplicationCache.RuntimeCache.GetCacheItem(key) == null)
                    //{
                    //    _log.Debug("Order is not in the session. Creating from sql");

                    //    var order = GetOrder(orderUniqueId);

                    //    ApplicationContext.Current.ApplicationCache.RuntimeCache.InsertCacheItem<OrderInfo>(key,) = order;
                    //}

                    //var orderInfo = (OrderInfo)_httpCtx.Session[key];

                    if (orderInfo?.OrderStatus != OrderStatus.ReadyForDispatch
                    && orderInfo?.OrderStatus != OrderStatus.Dispatched)
                    {
                        return orderInfo;
                    }
                }

            }

            return null;
        }

        public OrderInfo GetCompletedOrder(string storeAlias)
        {
            // Add timelimit to get the order ? Maybe 1-2 hours ?

            var store = _storeSvc.GetStoreByAlias(storeAlias);

            if (Configuration.Current.UserBasket)
            {
                if (_ekmRequest.User != null)
                {
                    var orderInfo = GetOrder(_ekmRequest.User.OrderId);
          
                    if (orderInfo?.OrderStatus == OrderStatus.ReadyForDispatch
                    || orderInfo?.OrderStatus == OrderStatus.Dispatched
                    || orderInfo?.OrderStatus == OrderStatus.OfflinePayment
                    || orderInfo?.OrderStatus == OrderStatus.Pending)
                    {
                        return orderInfo;
                    }
                }
            }
            else
            {
                _store = store;

                var key = CreateKey();
                // Get Cart UniqueId from Cookie.
                var orderUniqueId = GetOrderIdFromCookie(key);

                // If Cookie Exist then return Cart
                if (orderUniqueId != Guid.Empty)
                {

                    var orderInfo = ApplicationContext.Current.ApplicationCache.RuntimeCache
                                .GetCacheItem<OrderInfo>(orderUniqueId.ToString(),
                                    () => GetOrder(orderUniqueId), TimeSpan.FromDays(1));

                    if (orderInfo?.OrderStatus == OrderStatus.ReadyForDispatch
                    || orderInfo?.OrderStatus == OrderStatus.Dispatched
                    || orderInfo?.OrderStatus == OrderStatus.OfflinePayment
                    || orderInfo?.OrderStatus == OrderStatus.Pending)
                    {
                        return orderInfo;
                    }
                }

            }

            return null;
        }

        public OrderInfo GetOrder(Guid uniqueId)
        {
            // Chekk for cache ?

            return GetOrderInfo(uniqueId);
        }

        public OrderInfo GetOrderInfo(Guid uniqueId)
        {
            var orderData = _orderRepository.GetOrder(uniqueId);

            return orderData != null ? new OrderInfo(orderData) : null;
        }


        public void ChangeOrderStatus(Guid uniqueId, OrderStatus status, string userName = null)
        {
            // Add event handler

            var order = _orderRepository.GetOrder(uniqueId);

            var oldStatus = order.OrderStatus;

            order.OrderStatus = status;

            var key = CreateKey(order.StoreAlias);

            // Create function for this, For completed orders
            if (status == OrderStatus.ReadyForDispatch || status == OrderStatus.OfflinePayment)
            {
                if (status == OrderStatus.ReadyForDispatch && !order.PaidDate.HasValue) {
                    order.PaidDate = DateTime.Now;
                }

                if (Configuration.Current.UserBasket)
                {
                    var ms = _appCtx.Services.MemberService;

                    var member = ms.GetByUsername(_httpCtx.User.Identity.Name);
                    if (member.HasProperty("orderId"))
                    {
                        member.SetValue("orderId", "");
                    }
                    ms.Save(member);
                } else
                {
                    ApplicationContext.Current.ApplicationCache.RuntimeCache.ClearCacheItem(uniqueId.ToString());
                }

            }

            _orderRepository.UpdateOrder(order);

            ApplicationContext.Current.ApplicationCache.RuntimeCache
                        .GetCacheItem<OrderInfo>(uniqueId.ToString(),
                            () => new OrderInfo(order), TimeSpan.FromDays(1));


            _activityLogRepository.Insert(uniqueId, "Order status changed. From: " + oldStatus.ToString() + " To: " + status.ToString(), string.IsNullOrEmpty(userName) ? "Customer" : userName);

            _log.Debug("Change Order " + order.OrderNumber + " status to " + status.ToString());
        }

        public void UpdatePaidDate(Guid uniqueId)
        {

            var order = _orderRepository.GetOrder(uniqueId);

            order.PaidDate = DateTime.Now;

            _orderRepository.UpdateOrder(order);

            _log.Debug("Update Paid Date " + order.OrderNumber);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OrderException"></exception>
        /// <exception cref="ProductNotFoundException"></exception>
        /// <exception cref="VariantNotFoundException"></exception>
        public OrderInfo AddOrderLine(
            Guid productKey,
            int quantity,
            string storeAlias,
            OrderAction? action = null,
            Guid? variantKey = null
        )
        {
            if (productKey == Guid.Empty)
            {
                throw new ArgumentException(nameof(productKey));
            }

            var product = Catalog.Instance.GetProduct(productKey);

            if (product == null)
            {
                throw new ProductNotFoundException("Unable to find product with key " + productKey);
            } else
            {
                if (variantKey == null && !product.Backorder && product.Stock < quantity)
                {
                    throw new StockException("Stock not available for product " + variantKey);
                }
            }

            IVariant variant = null;
            if (variantKey != null)
            {
                variant = Catalog.Instance.GetVariant(variantKey.Value);

                if (variant == null)
                {
                    throw new VariantNotFoundException("Unable to find variant with key " + variantKey);
                } else
                {
                    if (!product.Backorder && variant.Stock < quantity)
                    {
                        throw new StockException("Stock not available for variant " + variantKey);
                    }
                }
            }


            var store = _storeSvc.GetStoreByAlias(storeAlias);

            return AddOrderLine(
                product,
                quantity,
                store,
                action,
                variant
            );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OrderException"></exception>
        public OrderInfo AddOrderLine(
            IProduct product,
            int quantity,
            IStore store,
            OrderAction? action = null,
            IVariant variant = null
        )
        {
            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            _store = store ?? throw new ArgumentNullException(nameof(store));
            _date = DateTime.Now;

            // If cart action is null then update is the default state
            var cartAction = action != null ? action.Value : OrderAction.AddOrUpdate;

            var orderInfo = GetOrder(_store);

            if (orderInfo == null)
            {
                orderInfo = CreateEmptyOrder();
            }

            _log.Debug("ProductId: " + product.Id +
                " variantId: " + variant?.Key +
                " qty: " + quantity +
                " Action: " + action +
                " Order: " + orderInfo.OrderNumber +
                " Store: " + _store.Alias +
                " Cart action " + cartAction
            );

            AddOrderLineToOrderInfo(
                orderInfo,
                product,
                quantity,
                cartAction,
                variant);

            return orderInfo;
        }

        public OrderInfo RemoveOrderLine(Guid lineId, string storeAlias)
        {
            _log.Debug("Remove OrderLine... LineId: " + lineId);

            _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);

            var orderInfo = GetOrder(storeAlias);

            var orderLine = orderInfo.OrderLines.FirstOrDefault(x => x.Key == lineId);

            if (orderLine != null)
            {
                RemoveOrderLine(orderInfo, orderLine as OrderLine);
            }
            else
            {
                throw new OrderLineNotFoundException("Could not find order line with key: " + lineId);
            }

            UpdateOrderAndOrderInfo(orderInfo);

            return orderInfo;
        }

        private void RemoveOrderLine(OrderInfo orderInfo, OrderLine orderLine)
        {
            orderInfo.orderLines.Remove(orderLine);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="OrderException"></exception>
        private void AddOrderLineToOrderInfo(
            OrderInfo orderInfo,
            IProduct product,
            int quantity,
            OrderAction action,
            IVariant variant
        )
        {
            if (quantity == 0)
            {
                // Use remove orderline instead
                throw new ArgumentException("Quantity can not be 0", nameof(quantity));
            }
            if (action == OrderAction.Set && quantity <= 0)
            {
                throw new ArgumentException("Quantity can not be set to 0 or less", nameof(quantity));
            }

            var lineId = Guid.NewGuid();

            _log.Debug(
                "Order: " + orderInfo.OrderNumber +
                " Product Key: " + product.Key +
                " Variant: " + variant?.Key +
                " Action: " + action);

            OrderLine existingOrderLine = null;

            if (orderInfo.OrderLines != null)
            {
                if (variant != null)
                {
                    existingOrderLine
                        = orderInfo.OrderLines
                            .FirstOrDefault(
                                x => x.Product.Key == product.Key
                                && x.Product.VariantGroups
                                    .Any(b => b.Variants.Any(z => z.Key == variant.Key)))
                            as OrderLine;
                }
                else
                {
                    existingOrderLine
                        = orderInfo.OrderLines.FirstOrDefault(x => x.Product.Key == product.Key)
                        as OrderLine;
                }
            }

            if (existingOrderLine != null)
            {
                _log.Debug("AddOrderLineToOrderInfo: existingOrderLine Found");

                // Update orderline quantity with value
                if (action == OrderAction.Set)
                {
                    existingOrderLine.Quantity = quantity;
                }
                else
                {
                    if (existingOrderLine.Quantity + quantity < 0)
                    {
                        throw new OrderException("OrderLines cannot be updated to negative quantity");
                    }

                    existingOrderLine.Quantity += quantity;

                    // If the update action ends up setting quantity to zero we remove the order line
                    if (existingOrderLine.Quantity == 0)
                    {
                        RemoveOrderLine(orderInfo, existingOrderLine);
                    }
                }
            }
            else
            {
                if (quantity < 0)
                {
                    throw new OrderException("OrderLines cannot be created with negative quantity");
                }

                // Update orderline when adding product to orderline

                _log.Debug("AddOrderLineToOrderInfo: existingOrderLine Not Found");

                var orderLine = new OrderLine(
                    product,
                    quantity,
                    lineId,
                    orderInfo,
                    variant
                );

                orderInfo.orderLines.Add(orderLine);

                if (product.Discount != null)
                {
                    _log.Debug($"Discount {product.Discount.Key} found on product, applying to OrderLine");

                    ApplyDiscountToOrderLine(
                        orderLine,
                        product.Discount,
                        orderInfo);
                }
            }

            UpdateOrderAndOrderInfo(orderInfo);
        }

        private void UpdateOrderAndOrderInfo(OrderInfo orderInfo)
        {
            _log.Debug("Update Order with new OrderInfo");

            VerifyDiscounts(orderInfo);

            orderInfo.CustomerInformation.CustomerIpAddress = _ekmRequest.IPAddress;

            var serializedOrderInfo = JsonConvert.SerializeObject(orderInfo, EkomJsonDotNet.settings);

            var orderData = _orderRepository.GetOrder(orderInfo.UniqueId);

            if (_ekmRequest.User != null)
            {
                orderInfo.CustomerInformation.Customer.UserId = _ekmRequest.User.UserId;
                orderInfo.CustomerInformation.Customer.UserName = _ekmRequest.User.Username;
                orderData.CustomerUsername = _ekmRequest.User.Username;
                orderData.CustomerId = _ekmRequest.User.UserId;
            }

            orderData.CustomerEmail = orderInfo.CustomerInformation.Customer.Email;
            orderData.CustomerName = orderInfo.CustomerInformation.Customer.Name;

            orderData.ShippingCountry = orderInfo.CustomerInformation.Shipping.Country;

            orderData.OrderInfo = serializedOrderInfo;
            orderData.UpdateDate = DateTime.Now;
            orderData.TotalAmount = orderInfo.ChargedAmount.Value;

            //Backwards compatability for old currency storeinfo 
            try
            {
                var ri = new RegionInfo(orderInfo.StoreInfo.Currency.FirstOrDefault().Currency.LCID);
                orderData.Currency = ri.ISOCurrencySymbol;
            }
            catch (ArgumentException)
            {
                orderData.Currency = orderInfo.StoreInfo.Culture;
            }

            _orderRepository.UpdateOrder(orderData);
            UpdateOrderInfoInCache(orderInfo);
        }

        /// <summary>
        /// Is this necessary?
        /// </summary>
        /// <param name="orderInfo"></param>
        private void UpdateOrderInfoInCache(OrderInfo orderInfo)
        {
            var key = CreateKey(orderInfo.StoreInfo.Alias);

            ApplicationContext.Current.ApplicationCache.RuntimeCache
                        .InsertCacheItem<OrderInfo>(orderInfo.UniqueId.ToString(),
                            () => orderInfo, TimeSpan.FromDays(1));
        }

        public void AddHangfireJobsToOrder(string storeAlias, IEnumerable<string> hangfireJobs)
        {
            var o = GetOrder(storeAlias);

            o._hangfireJobs.AddRange(hangfireJobs);

            UpdateOrderAndOrderInfo(o);
        }

        private OrderInfo CreateEmptyOrder()
        {
            _log.Debug("Add OrderLine ...  Create Empty Order..");

            Guid orderUniqueId;
            if (Configuration.Current.UserBasket)
            {
                orderUniqueId = Guid.NewGuid();

                var ms = _appCtx.Services.MemberService;

                var member = ms.GetByUsername(_httpCtx.User.Identity.Name);
                if (member.HasProperty("orderId"))
                {
                    member.SetValue("orderId", orderUniqueId.ToString());
                }
                ms.Save(member);
            } else
            {
                var key = CreateKey();
                orderUniqueId = CreateOrderIdCookie(key);
            }

            var orderdata = SaveOrderData(orderUniqueId);

            return new OrderInfo(orderdata, _store);
        }

        private OrderData SaveOrderData(Guid uniqueId)
        {
            _log.Debug("Add OrderLine ...  Create OrderData.. Store: " + _store.Alias);

            var orderData = new OrderData
            {
                UniqueId = uniqueId,
                CreateDate = _date,
                StoreAlias = _store.Alias,
                OrderStatus = OrderStatus.Incomplete
            };

            if (_ekmRequest.User != null)
            {
                orderData.CustomerEmail = _ekmRequest.User.Email;
                orderData.CustomerUsername = _ekmRequest.User.Username;
                orderData.CustomerId = _ekmRequest.User.UserId;
                orderData.CustomerName = _ekmRequest.User.Name;
            }

            _orderRepository.InsertOrder(orderData);
            orderData.OrderNumber = GenerateOrderNumberTemplate(orderData.ReferenceId);
            _orderRepository.UpdateOrder(orderData);

            return orderData;
        }

        public OrderInfo UpdateCustomerInformation(Dictionary<string, string> form)
        {
            _log.Debug("UpdateCustomerInformation...");

            if (form.Keys.Any(x => x == "storeAlias"))
            {
                var storeAlias = form["storeAlias"];

                _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);
                _date = DateTime.Now;

                var orderInfo = GetOrder(storeAlias);

                foreach (var key in form.Keys.Where(x => x.StartsWith("customer")))
                {
                    var value = form[key];
                    orderInfo.CustomerInformation.Customer.Properties[key] = value;
                }

                foreach (var key in form.Keys.Where(x => x.StartsWith("shipping")))
                {
                    var value = form[key];

                    orderInfo.CustomerInformation.Shipping.Properties[key] = value;
                }

                UpdateOrderAndOrderInfo(orderInfo);

                return orderInfo;
            }

            return null;
        }

        public OrderInfo UpdateShippingInformation(Guid shippingProviderId, string storeAlias)
        {
            _log.Debug("UpdateShippingInformation...");

            _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);
            _date = DateTime.Now;

            var orderInfo = GetOrder(storeAlias);

            if (shippingProviderId != Guid.Empty)
            {
                var provider = API.Providers.Instance.GetShippingProvider(shippingProviderId, _store);

                if (provider != null)
                {
                    var orderedShippingProvider = new OrderedShippingProvider(provider);

                    orderInfo.ShippingProvider = orderedShippingProvider;

                    UpdateOrderAndOrderInfo(orderInfo);
                }

            }

            return orderInfo;
        }

        public OrderInfo UpdatePaymentInformation(Guid paymentProviderId, string storeAlias)
        {
            _log.Debug("UpdatePaymentInformation...");

            _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);
            _date = DateTime.Now;

            var orderInfo = GetOrder(storeAlias);

            if (paymentProviderId != Guid.Empty)
            {
                var provider = API.Providers.Instance.GetPaymentProvider(paymentProviderId, _store);

                if (provider != null)
                {
                    var orderedPaymentProvider = new OrderedPaymentProvider(provider);

                    orderInfo.PaymentProvider = orderedPaymentProvider;

                    UpdateOrderAndOrderInfo(orderInfo);
                }
            }

            return orderInfo;
        }

        public IEnumerable<OrderInfo> GetCompleteCustomerOrders(int customerId)
        {
            var list = new List<OrderInfo>();

            var orders = _orderRepository.GetCompletedOrdersByCustomerId(customerId);

            foreach (var o in orders)
            {
                list.Add(new OrderInfo(o));
            }

            return list;
        }


        public bool CheckStockAvailability(IOrderInfo orderInfo)
        {

            foreach (var line in orderInfo.OrderLines)
            {

                if (!line.Product.Backorder)
                {
                    if (line.Product.VariantGroups.Any())
                    {
                        foreach (var variant in line.Product.VariantGroups.SelectMany(x => x.Variants))
                        {
                            var variantStock = Stock.Instance.GetStock(variant.Key);

                            if (variantStock < line.Quantity)
                            {
                                return false;
                            }
                        }

                    }
                    else
                    {
                        var productStock = Stock.Instance.GetStock(line.ProductKey);

                        if (productStock < line.Quantity)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }


        public Guid GetOrderIdFromCookie(string key)
        {
            var cookie = _httpCtx.Request.Cookies[key];
            if (cookie != null)
            {
                return new Guid(cookie.Value);
            }

            return Guid.Empty;
        }

        public Guid CreateOrderIdCookie(string key)
        {
            var _guid = Guid.NewGuid();
            var guidCookie = new HttpCookie(key)
            {
                Value = _guid.ToString(),
                Expires = DateTime.Now.AddDays(1d)
            };

            _httpCtx.Response.Cookies.Add(guidCookie);
            return _guid;
        }

        private void DeleteOrderCookie(string key)
        {
            var cookie = _httpCtx.Request.Cookies[key];

            if (cookie != null)
            {
                cookie.Expires = DateTime.Now.AddDays(-1);
                cookie.Value = null;

                _httpCtx.Response.SetCookie(cookie);
            }
            else
            {
                _log.Warn("Could not delete order cookie. Cookie not found. Key: " + key);
            }

        }

        private string GenerateOrderNumberTemplate(int referenceId)
        {
            var _referenceId = referenceId.ToString();

            if (string.IsNullOrEmpty(_store.OrderNumberTemplate))
            {
                return string.Format("{0}{1}", _store.OrderNumberPrefix, referenceId.ToString("0000"));
            }

            var template = _store.OrderNumberTemplate;

            return template.Replace("#orderId#", _referenceId).Replace("#orderIdPadded#", referenceId.ToString("0000")).Replace("#storeAlias#", _store.Alias).Replace("#day#", _date.Day.ToString()).Replace("#month#", _date.Month.ToString()).Replace("#year#", _date.Year.ToString());
        }

        private string CreateKey(string storeAlias = null)
        {
            var key = "ekmOrder";

            storeAlias = string.IsNullOrEmpty(storeAlias) ? _store.Alias : storeAlias;
            key += "-" + storeAlias;

            return key;
        }
    }
}
