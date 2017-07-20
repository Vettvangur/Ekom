using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using uWebshop.Helpers;
using uWebshop.Interfaces;
using uWebshop.Models.Data;

namespace uWebshop.Repository
{
    public class OrderRepository
    {
        Configuration _config;
        public OrderRepository(Configuration config)
        {
            _config = config;
        }

        public OrderData GetOrder(Guid uniqueId)
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                return db.FirstOrDefault<OrderData>("SELECT * FROM uWebshopOrders WHERE UniqueId = @0", uniqueId);
            }
        }

        public void InsertOrder(OrderData orderData)
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                db.Insert(orderData);
            }
        }

        public void UpdateOrder(OrderData orderData)
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                db.Update(orderData);
            }
        }

        public int GetHighestOrderNumber(string storeAlias = null)
        {
            int orderNumber = 1;

            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                var _orderNumber = "1";

                if (_config.ShareBasketBetweenStores || string.IsNullOrEmpty(storeAlias))
                {
                    _orderNumber = db.FirstOrDefault<string>("SELECT ReferenceId FROM uWebshopOrders ORDER BY CreateDate DESC");
                } else
                {
                    _orderNumber = db.FirstOrDefault<string>("SELECT ReferenceId FROM uWebshopOrders WHERE StoreAlias = @0 ORDER BY CreateDate DESC", storeAlias);
                }

                int.TryParse(_orderNumber, out orderNumber);

                return orderNumber;
            }
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

    }
}
