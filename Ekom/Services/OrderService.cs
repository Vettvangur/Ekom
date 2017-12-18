using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Data;
using Ekom.Repository;
using Umbraco.Core;
using Umbraco.Core.Cache;

namespace Ekom.Services
{
    class OrderService : IOrderService
    {
        ILog _log;
        private HttpContextBase _httpCtx
        {
            get
            {
                try
                {
                    return Configuration.container.GetInstance<HttpContextBase>();
                }
                catch (Exception ex)
                {
                    _log.Error("HttpContext not available.", ex);
                    return null;
                }
            }
        }

        ApplicationContext _appCtx;
        ICacheProvider _reqCache => _appCtx.ApplicationCache.RequestCache;

        private Store _store;
        private DateTime _date;
        private OrderRepository _orderRepository;
        private IStoreService _storeSvc;
        private ContentRequest ekmRequest;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="orderRepo"></param>
        /// <param name="logFac"></param>
        /// <param name="storeService"></param>
        /// <param name="appCtx"></param>
        public OrderService(OrderRepository orderRepo, ILogFactory logFac, IStoreService storeService, ApplicationContext appCtx)
        {
            _appCtx = appCtx;
            _orderRepository = orderRepo;
            _storeSvc = storeService;
            _log = logFac.GetLogger(typeof(OrderService));
            ekmRequest = _reqCache.GetCacheItem("ekmRequest") as ContentRequest;
        }

        public OrderInfo GetOrderInfo(Guid uniqueId)
        {
            var orderData = _orderRepository.GetOrder(uniqueId);

            return orderData != null ? CreateOrderInfoFromOrderData(orderData, false) : null;
        }

        public OrderInfo GetOrder(string storeAlias)
        {
            _log.Info("Get Order: Store: " + storeAlias);

            var store = _storeSvc.GetStoreByAlias(storeAlias);

            return GetOrder(store);
        }

        public OrderInfo GetOrder(Store store)
        {
            _store = store;

            var key = CreateKey();

            // Get Cart UniqueId from Cookie.
            var orderUniqueId = GetOrderIdFromCookie(key);

            // If Cookie Exist then return Cart
            if (orderUniqueId != Guid.Empty)
            {

                // If the cart is not in the session, fetch order from sql and insert to session
                if (_httpCtx.Session[key] == null)
                {
                    _log.Info("Order is not in the session. Creating from sql");

                    var order = GetOrderInfo(orderUniqueId);

                    _httpCtx.Session[key] = order;
                }
                else
                {
                    _log.Info("Order Found in Session!");
                }

                var orderInfo = (OrderInfo)_httpCtx.Session[key];

                if (orderInfo != null && (orderInfo.OrderStatus != OrderStatus.ReadyForDispatch || orderInfo.OrderStatus != OrderStatus.Confirmed))
                {
                    return orderInfo;
                }

            }

            return null;
        }

        public void ChangeOrderStatus(Guid uniqueId, OrderStatus status)
        {
            // Add event handler

            var order = _orderRepository.GetOrder(uniqueId);

            order.OrderStatus = status;

            // Create function for this, For completed orders
            if (status == OrderStatus.ReadyForDispatch  || status == OrderStatus.OfflinePayment)
            {
                var key = CreateKey(order.StoreAlias);

                DeleteOrderCookie(key);
                _httpCtx.Session.Remove(key);
                order.PaidDate = DateTime.Now;
            }

            _orderRepository.UpdateOrder(order);

            _log.Info("Change Order " + order.OrderNumber + " status to " + status.ToString());
        }

        public OrderInfo AddOrderLine(
            Guid productId,
            IEnumerable<Guid> variantIds,
            int quantity,
            string storeAlias,
            OrderAction? action
        )
        {
            _log.Info("Add OrderLine... ProductId: " + productId + " variantIds: " + variantIds.Any() + " qty: " + quantity + " Action: " + action);

            _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);
            _date = DateTime.Now;

            _log.Info("Add OrderLine ... Store: " + _store.Alias);

            // If cart action is null then update is the default state
            var cartAction = action != null ? action.Value : OrderAction.Update;

            _log.Info("Add OrderLine ...  Get Order.. Action: " + cartAction);

            var orderInfo = GetOrder(storeAlias);

            if (orderInfo == null)
            {
                _log.Info("Add OrderLine ...  Order not found..");
                orderInfo = CreateEmptyOrder();
            }

            //todo: fix
            if (orderInfo.CustomerInformation == null)
            {
                var c = new CustomerInfo();

                orderInfo.CustomerInformation = new CustomerInfo();
            }

            orderInfo.CustomerInformation.CustomerIpAddress = ekmRequest.IPAddress;

            if (ekmRequest.User != null)
            {
                var name = orderInfo.CustomerInformation.Customer.Name;
                var email = orderInfo.CustomerInformation.Customer.Email;

                orderInfo.CustomerInformation.Customer.UserId = ekmRequest.User.UserId;
                orderInfo.CustomerInformation.Customer.UserName = ekmRequest.User.Username;
            }

            _log.Info("Add OrderLine ...  Order: " + orderInfo.OrderNumber);

            AddOrderLineToOrderInfo(orderInfo, productId, variantIds, quantity, cartAction);

            return orderInfo;
        }

        public OrderInfo RemoveOrderLine(Guid lineId, string storeAlias)
        {
            _log.Info("Remove OrderLine... LineId: " + lineId);

            _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);

            var orderInfo = GetOrder(storeAlias);

            if (orderInfo != null)
            {
                var orderLine = orderInfo.OrderLines.FirstOrDefault(x => x.Id == lineId);

                if (orderLine != null)
                {
                    orderInfo.OrderLines.Remove(orderLine);
                }
            }

            UpdateOrderAndOrderInfo(orderInfo);

            return orderInfo;
        }

        private void AddOrderLineToOrderInfo(OrderInfo orderInfo, Guid productId, IEnumerable<Guid> variantIds, int quantity, OrderAction action)
        {

            if (quantity <= -1)
            {
                throw new Exception("Quantity can not be less then 0");
            }

            var lineId = Guid.NewGuid();

            _log.Info("AddOrderLineToOrderInfo: Order: " + orderInfo.OrderNumber + " Product Key: " + productId + " Variant: " + (variantIds.Any() ? variantIds.First() : Guid.Empty) + " Action: " + action);
            OrderLine existingOrderLine = null;

            if (orderInfo.OrderLines != null)
            {
                if (variantIds.Any())
                {
                    existingOrderLine = orderInfo.OrderLines.FirstOrDefault(x => x.Product.Key == productId && x.Product.VariantGroups.SelectMany(b => b.Variants.Select(z => z.Key).Intersect(variantIds)).Any());
                }
                else
                {
                    existingOrderLine = orderInfo.OrderLines.FirstOrDefault(x => x.Product.Key == productId);
                }
            }


            if (existingOrderLine != null)
            {
                _log.Info("AddOrderLineToOrderInfo: existingOrderLine Found");

                // Update orderline quantity with value
                if (action == OrderAction.UpdateQuantity)
                {
                    existingOrderLine.Quantity = quantity;
                } else
                {
                    existingOrderLine.Quantity = existingOrderLine.Quantity + quantity;
                }
                
            }
            else
            {
                // Update orderline when adding product to orderline

                _log.Info("AddOrderLineToOrderInfo: existingOrderLine Not Found");

                if (existingOrderLine == null)
                {
                    var orderLine = new OrderLine(productId, variantIds, quantity, lineId, _store);

                    orderInfo.OrderLines.Add(orderLine);
                }
                else
                {
                    existingOrderLine.Quantity = existingOrderLine.Quantity + quantity;
                }
            }

            UpdateOrderAndOrderInfo(orderInfo);
        }

        private void UpdateOrderAndOrderInfo(OrderInfo orderInfo)
        {
            _log.Info("Update Order with new OrderInfo");

            orderInfo.CustomerInformation.CustomerIpAddress = HttpContext.Current.Request.UserHostAddress;

            if (ekmRequest.User != null)
            {
                orderInfo.CustomerInformation.Customer.UserId = ekmRequest.User.UserId;
                orderInfo.CustomerInformation.Customer.UserName = ekmRequest.User.Username;
            }

            var serializedOrderInfo = JsonConvert.SerializeObject(orderInfo);

            var orderData = _orderRepository.GetOrder(orderInfo.UniqueId);

            // Put inside constructor ? 
            if (ekmRequest.User != null)
            {
                orderData.CustomerEmail = ekmRequest.User.Email;
                orderData.CustomerUsername = ekmRequest.User.Username;
                orderData.CustomerId = ekmRequest.User.UserId;
                orderData.CustomerName = ekmRequest.User.Name;
            }

            orderData.OrderInfo = serializedOrderInfo;
            orderData.UpdateDate = DateTime.Now;

            _orderRepository.UpdateOrder(orderData);
            UpdateOrderInfoInSession(orderInfo);
        }

        private void UpdateOrderInfoInSession(OrderInfo orderInfo)
        {
            var key = CreateKey();

            _httpCtx.Session[key] = orderInfo;
        }

        private OrderInfo CreateEmptyOrder()
        {
            _log.Info("Add OrderLine ...  Create Empty Order..");
            var key = CreateKey();

            var orderUniqueId = CreateOrderIdCookie(key);

            var orderdata = SaveOrderData(orderUniqueId);

            return CreateOrderInfoFromOrderData(orderdata, true);
        }

        private OrderInfo CreateOrderInfoFromOrderData(OrderData orderData, bool empty)
        {
            //_log.Info("Add OrderLine ...  Create OrderInfo from OrderData..");

            //if (!empty && (orderData == null || string.IsNullOrEmpty(orderData.OrderInfo)))
            //{
            //    throw new Exception("Trying to load order without data (xml), id: " + (orderData == null ? "no data!" : orderData.UniqueId.ToString()) + ", ordernumber: " + (orderData == null ? "no data!" : orderData.OrderNumber));
            //}

            // This Need Complete overhaul!!
            var orderInfo = new OrderInfo(orderData);

            return orderInfo;
        }

        private OrderData SaveOrderData(Guid uniqueId)
        {
            _log.Info("Add OrderLine ...  Create OrderData.. Store: " + _store.Alias);
            string orderNumber = string.Empty;

            GenerateOrderNumber(out int referenceId, out orderNumber);


            var orderData = new OrderData
            {
                UniqueId = uniqueId,
                CreateDate = _date,
                StoreAlias = _store.Alias,
                ReferenceId = referenceId,
                OrderNumber = orderNumber,
                OrderStatus = OrderStatus.Incomplete
            };

            if (ekmRequest.User != null)
            {
                orderData.CustomerEmail = ekmRequest.User.Email;
                orderData.CustomerUsername = ekmRequest.User.Username;
                orderData.CustomerId = ekmRequest.User.UserId;
                orderData.CustomerName = ekmRequest.User.Name;
            }

            _orderRepository.InsertOrder(orderData);

            return orderData;
        }


        public OrderInfo UpdateCustomerInformation(Dictionary<string,string> form)
        {
            _log.Info("UpdateCustomerInformation...");

            if (form.Keys.Any(x => x == "storeAlias"))
            {
                var storeAlias = form["storeAlias"];

                _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);
                _date = DateTime.Now;

                var orderInfo = GetOrder(storeAlias);

                var customerProperties = new Dictionary<string, string>();

                foreach (var key in form.Keys.Where(x => x.StartsWith("customer")))
                {
                    var value = form[key];

                    customerProperties.Add(key, value);
                }

                orderInfo.CustomerInformation.Customer.Properties = customerProperties;

                var shippingProperties = new Dictionary<string, string>();

                foreach (var key in form.Keys.Where(x => x.StartsWith("shipping")))
                {
                    var value = form[key];

                    shippingProperties.Add(key, value);
                }

                orderInfo.CustomerInformation.Shipping.Properties = shippingProperties;

                UpdateOrderAndOrderInfo(orderInfo);

                return orderInfo;
            }

            return null;


        }

        public OrderInfo UpdateShippingInformation(Guid shippingProviderId, string storeAlias)
        {
            _log.Info("UpdateShippingInformation...");

            _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);
            _date = DateTime.Now;

            var orderInfo = GetOrder(storeAlias);

            if (shippingProviderId != Guid.Empty)
            {
                var provider = API.Providers.Current.GetShippingProvider(shippingProviderId, _store);

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
            _log.Info("UpdatePaymentInformation...");

            _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);
            _date = DateTime.Now;

            var orderInfo = GetOrder(storeAlias);

            if (paymentProviderId != Guid.Empty)
            {
                var provider = API.Providers.Current.GetPaymentProvider(paymentProviderId, _store);

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
            
            var orders = _orderRepository.GetCompleteOrderByCustomerId(customerId);

            foreach (var o in orders)
            {
                list.Add(CreateOrderInfoFromOrderData(o, false));
            }

            return list;
        }

        public string CreateKey(string storeAlias = null)
        {
            var key = "ekmOrder";

            storeAlias = string.IsNullOrEmpty(storeAlias) ? _store.Alias : storeAlias;

            key += "-" + storeAlias;

            return key;
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
            HttpCookie cookie = new HttpCookie(key)
            {
                Expires = DateTime.Now.AddDays(-1)
            };

            _httpCtx.Response.Cookies.Set(cookie);

        } 

        private void GenerateOrderNumber(out int referenceId, out string orderNumber)
        {
            var lastOrderNumber = _orderRepository.GetHighestOrderNumber(_store.Alias);
            referenceId = lastOrderNumber + 1;
            orderNumber = GenerateOrderNumberTemplate(referenceId);
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
    }
}
