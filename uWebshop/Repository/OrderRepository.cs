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
using uWebshop.Services;

namespace uWebshop.Repository
{
    class OrderRepository
    {
        ILog _log;
        Configuration _config;
        DatabaseContext _dbCtx;
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="config"></param>
        /// <param name="dbCtx"></param>
        /// <param name="logFac"></param>
        public OrderRepository(Configuration config, DatabaseContext dbCtx, ILogFactory logFac)
        {
            _config = config;
            _dbCtx = dbCtx;
            _log = logFac.GetLogger(typeof(OrderRepository));
        }

        public OrderData GetOrder(Guid uniqueId)
        {
            using (var db = _dbCtx.Database)
            {
                return db.FirstOrDefault<OrderData>("WHERE UniqueId = @0", uniqueId);
            }
        }

        public void InsertOrder(OrderData orderData)
        {
            using (var db = _dbCtx.Database)
            {
                db.Insert(orderData);
            }
        }

        public void UpdateOrder(OrderData orderData)
        {
            using (var db = _dbCtx.Database)
            {
                db.Update(orderData);
            }
        }

        public int GetHighestOrderNumber(string storeAlias = null)
        {
            int orderNumber = 1;

            using (var db = _dbCtx.Database)
            {
                var _orderNumber = "1";

                if (_config.ShareBasketBetweenStores || string.IsNullOrEmpty(storeAlias))
                {
                    _orderNumber = db.FirstOrDefault<string>("SELECT TOP 1 ReferenceId from uWebshopOrders ORDER BY CreateDate DESC");
                } else
                {
                    _orderNumber = db.FirstOrDefault<string>("SELECT TOP 1 ReferenceId from uWebshopOrders WHERE StoreAlias = @0 ORDER BY CreateDate DESC", storeAlias);
                }

                int.TryParse(_orderNumber, out orderNumber);

                return orderNumber;
            }
        }
    }
}
