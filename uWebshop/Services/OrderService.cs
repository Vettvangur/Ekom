using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Models;
using uWebshop.Models.Data;
using uWebshop.Repository;

namespace uWebshop.Services
{
    public class OrderService
    {
        private Store _store;
        private DateTime _date;
        private OrderRepository _orderRepository;
        public OrderService(OrderRepository orderRepo)
        {
            _orderRepository = orderRepo;
        }

        public OrderInfo GetOrderInfo(Guid uniqueId)
        {
            var orderData = _orderRepository.GetOrder(uniqueId);

            return orderData != null ? CreateOrderInfoFromOrderData(orderData, false) : null;
        }

        public OrderInfo GetOrder(string storeAlias)
        {
            var httpContext = HttpContext.Current;

            _store = _store == null ? API.Store.GetStore(storeAlias) : _store;

            var key = CreateKey();

            // Get Cart UniqueId from Cookie.
            var orderUniqueId = GetOrderIdFromCookie(key);

            // If Cookie Exist then return Cart
            if (orderUniqueId != Guid.Empty)
            {
                Log.Info("Get Order: " + orderUniqueId);

                // If the cart is not in the session, fetch order from sql and insert to session
                if (httpContext.Session[key] == null)
                {
                    Log.Info("Order is not in the session. Creating from sql");

                    var order = GetOrderInfo(orderUniqueId);

                    httpContext.Session[key] = order;
                } else
                {
                    Log.Info("Order Found in Session!");
                }

                return (OrderInfo)httpContext.Session[key];

            }
            else
            {
                return null;
            }

        }

        public OrderInfo AddOrderLine(Guid productId, IEnumerable<Guid> variantIds, int quantity, string storeAlias, OrderAction? action)
        {
            Log.Info("Add OrderLine...");
            _store = _store == null ? API.Store.GetStore(storeAlias) : _store;
            _date = DateTime.Now;

            Log.Info("Add OrderLine ...  Get Order.. Store: " + _store.Alias);

            // If cart action is null then update is the default state
            var cartAction = action != null ? action.Value : OrderAction.Update;

            Log.Info("Add OrderLine ...  Get Order..");
            var orderInfo = GetOrder(storeAlias);

            if (orderInfo == null)
            {
                Log.Info("Add OrderLine ...  Order not found..");
                orderInfo = CreateEmptyOrder();
            }

            AddOrderLineToOrderInfo(orderInfo, productId, variantIds, quantity, cartAction);

            return orderInfo;
        }

        public OrderInfo RemoveOrderLine(Guid lineId, string storeAlias)
        {
            Log.Info("Remove OrderLine... LineId: " + lineId);

            _store = _store == null ? API.Store.GetStore(storeAlias) : _store;

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

        public void AddOrderLineToOrderInfo(OrderInfo orderInfo, Guid productId, IEnumerable<Guid> variantIds, int quantity, OrderAction action)
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
            } else
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

        public void UpdateOrderAndOrderInfo(OrderInfo orderInfo)
        {
            Log.Info("Update Order with new OrderInfo");

            var serializedOrderInfo = JsonConvert.SerializeObject(orderInfo);

            var orderData = _orderRepository.GetOrder(orderInfo.UniqueId);

            orderData.OrderInfo = serializedOrderInfo;
            orderData.UpdateDate = DateTime.Now;

            _orderRepository.UpdateOrder(orderData);
            UpdateOrderInfoInSession(orderInfo);
        }

        public void UpdateOrderInfoInSession(OrderInfo orderInfo)
        {
            var httpContext = HttpContext.Current;

            var key = CreateKey();

            httpContext.Session[key] = orderInfo;
        }

        public OrderInfo CreateEmptyOrder()
        {
            Log.Info("Add OrderLine ...  Create Empty Order..");
            var key = CreateKey();

            var orderUniqueId = CreateOrderIdCookie(key);

            var orderdata = CreateOrderData(orderUniqueId);
             
            return CreateOrderInfoFromOrderData(orderdata, true);
        }

        public OrderInfo CreateOrderInfoFromOrderData(OrderData orderData, bool empty)
        {
            Log.Info("Add OrderLine ...  Create OrderInfo from OrderData..");

            //if (!empty && (orderData == null || string.IsNullOrEmpty(orderData.OrderInfo)))
            //{
            //    throw new Exception("Trying to load order without data (xml), id: " + (orderData == null ? "no data!" : orderData.UniqueId.ToString()) + ", ordernumber: " + (orderData == null ? "no data!" : orderData.OrderNumber));
            //}

            var orderInfo = new OrderInfo(orderData,_store);

            orderInfo.OrderLines = CreateOrderLinesFromJson(orderData.OrderInfo);

            return orderInfo;
        }

        public OrderData CreateOrderData(Guid uniqueId)
        {
            Log.Info("Add OrderLine ...  Create OrderData.. Store: " + _store.Alias);
            int referenceId = 0;
            string orderNumber = string.Empty;

            GenerateOrderNumber(out referenceId, out orderNumber);

            var orderData = new OrderData()
            {
                UniqueId = uniqueId,
                CreateDate = _date,
                StoreAlias = _store.Alias,
                ReferenceId = referenceId,
                OrderNumber = orderNumber,
                OrderStatus = OrderStatus.Incomplete.ToString()
            };

            _orderRepository.InsertOrder(orderData);

            return orderData;
        }

        public List<OrderLine> CreateOrderLinesFromJson(string json)
        {
            var orderLines = new List<OrderLine>();

            Log.Info("Add OrderLine ...  Create OrderLines from Json.. Json: " + json);

            if (!string.IsNullOrEmpty(json))
            {
                Log.Info("Add OrderLine ...  Create OrderLines from Json.. Creating...");

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

        public string CreateKey()
        {
            var key = "uwbsOrder";

            if (_store != null)
            {
                key += "-" + _store.Alias;
            }

            return key;
        }

        public Guid GetOrderIdFromCookie(string key)
        {
            if (HttpContext.Current.Request.Cookies[key] != null)
            {
                return new Guid(HttpContext.Current.Request.Cookies[key].Value);
            }

            return Guid.Empty;
        }

        public Guid CreateOrderIdCookie(string key)
        {
            var _guid = Guid.NewGuid();
            HttpCookie guidCookie = new HttpCookie(key);
            guidCookie.Value = _guid.ToString();
            guidCookie.Expires = DateTime.Now.AddDays(1d);
            HttpContext.Current.Response.Cookies.Add(guidCookie);
            return _guid;
        }

        public void GenerateOrderNumber(out int referenceId, out string orderNumber)
        {
            var lastOrderNumber = _orderRepository.GetHighestOrderNumber(_store.Alias);
            referenceId = lastOrderNumber + 1;
            orderNumber = GenerateOrderNumberTemplate(referenceId);
        }

        public string GenerateOrderNumberTemplate(int referenceId)
        {
            var _referenceId = referenceId.ToString();

            if (string.IsNullOrEmpty(_store.OrderNumberTemplate))
            {
                return string.Format("{0}{1}", _store.OrderNumberPrefix, referenceId.ToString("0000"));
            }

            var template = _store.OrderNumberTemplate;
            
            return template.Replace("#orderId#", _referenceId).Replace("#orderIdPadded#", referenceId.ToString("0000")).Replace("#storeAlias#", _store.Alias).Replace("#day#", _date.Day.ToString()).Replace("#month#", _date.Month.ToString()).Replace("#year#", _date.Year.ToString());
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
