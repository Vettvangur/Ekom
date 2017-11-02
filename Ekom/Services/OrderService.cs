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
        private Store _store;
        private DateTime _date;
        private OrderRepository _orderRepository;
        private IStoreService _storeSvc;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="orderRepo"></param>
        /// <param name="logFac"></param>
        /// <param name="storeService"></param>
        public OrderService(OrderRepository orderRepo, ILogFactory logFac, IStoreService storeService)
        {
            _orderRepository = orderRepo;
            _storeSvc = storeService;
            _log = logFac.GetLogger(typeof(OrderService));
        }

        public OrderInfo GetOrderInfo(Guid uniqueId)
        {
            var orderData = _orderRepository.GetOrder(uniqueId);

            return orderData != null ? CreateOrderInfoFromOrderData(orderData, false) : null;
        }

        public OrderInfo GetOrder(string storeAlias)
        {
            _store = _store ?? _storeSvc.GetStoreByAlias(storeAlias);

            _log.Info("Get Order: Store: " + _store.Alias);

            var key = CreateKey();

            _log.Info("Get Order: Key: " + key);

            // Get Cart UniqueId from Cookie.
            var orderUniqueId = GetOrderIdFromCookie(key);

            // If Cookie Exist then return Cart
            if (orderUniqueId != Guid.Empty)
            {
                _log.Info("Get Order: " + orderUniqueId);

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

                return (OrderInfo)_httpCtx.Session[key];
            }
            else
            {
                return null;
            }
        }

        public void ChangeOrderStatus(OrderData order, OrderStatus status)
        {
            // Add event handler

            order.OrderStatus = status;

            // Create function for this, For completed orders
            if (status == OrderStatus.ReadyForDispatch  || status == OrderStatus.OfflinePayment)
            {
                // clean cookie state
            }

            _orderRepository.UpdateOrder(order);
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

            _log.Info("Add OrderLine ...  Get Order.. Store: " + _store.Alias);

            // If cart action is null then update is the default state
            var cartAction = action != null ? action.Value : OrderAction.Update;

            _log.Info("Add OrderLine ...  Get Order..");
            var orderInfo = GetOrder(storeAlias);

            if (orderInfo == null)
            {
                _log.Info("Add OrderLine ...  Order not found..");
                orderInfo = CreateEmptyOrder();
            }

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

            OrderLine existingOrderLine = orderInfo.OrderLines.FirstOrDefault(x => x.Id == productId);

            if (existingOrderLine != null)
            {
                // Update orderline quantity with value
                existingOrderLine.Quantity = quantity;
            }
            else
            {
                // Update orderline when adding product to orderline

                if (action == OrderAction.Update)
                {
                    // Need to check for variant also.
                    existingOrderLine = orderInfo.OrderLines.FirstOrDefault(x => x.Product.Key == productId);
                }

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

            var serializedOrderInfo = JsonConvert.SerializeObject(orderInfo);

            var orderData = _orderRepository.GetOrder(orderInfo.UniqueId);

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
            _log.Info("Add OrderLine ...  Create OrderInfo from OrderData..");

            //if (!empty && (orderData == null || string.IsNullOrEmpty(orderData.OrderInfo)))
            //{
            //    throw new Exception("Trying to load order without data (xml), id: " + (orderData == null ? "no data!" : orderData.UniqueId.ToString()) + ", ordernumber: " + (orderData == null ? "no data!" : orderData.OrderNumber));
            //}

            var orderInfo = new OrderInfo(orderData, _store)
            {
                OrderLines = CreateOrderLinesFromJson(orderData.OrderInfo)
            };

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
                OrderStatus = OrderStatus.Incomplete,
            };

            _orderRepository.InsertOrder(orderData);

            return orderData;
        }

        private List<OrderLine> CreateOrderLinesFromJson(string json)
        {
            var orderLines = new List<OrderLine>();

            _log.Info("Add OrderLine ...  Create OrderLines from Json.. Json: " + json);

            if (!string.IsNullOrEmpty(json))
            {
                _log.Info("Add OrderLine ...  Create OrderLines from Json.. Creating...");

                var orderInfoJObject = JObject.Parse(json);

                var orderLinesArray = (JArray)orderInfoJObject["OrderLines"];

                var storeJson = orderInfoJObject["StoreInfo"].ToString();

                var storeInfo = JsonConvert.DeserializeObject<StoreInfo>(storeJson);

                foreach (var line in orderLinesArray)
                {
                    var lineId = (Guid)line["Id"];
                    var quantity = (int)line["Quantity"];
                    var productJson = line["Product"].ToString();

                    var orderLine = new OrderLine(lineId, quantity, productJson, storeInfo);

                    orderLines.Add(orderLine);
                }
            }

            return orderLines;
        }

        private string CreateKey()
        {
            var key = "ekmOrder";

            if (_store != null)
            {
                key += "-" + _store.Alias;
            }

            return key;
        }

        private Guid GetOrderIdFromCookie(string key)
        {
            var cookie = _httpCtx.Request.Cookies[key];
            if (cookie != null)
            {
                return new Guid(cookie.Value);
            }

            return Guid.Empty;
        }

        private Guid CreateOrderIdCookie(string key)
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
