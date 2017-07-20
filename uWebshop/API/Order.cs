using log4net;
using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using uWebshop.App_Start;
using uWebshop.Helpers;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.API
{
    public class Order
    {
        static IUnityContainer container
        {
            get { return UnityConfig.GetConfiguredContainer(); }
        }

        public static OrderInfo GetOrder(string storeAlias)
        {
            var orderService = container.Resolve<OrderService>();

            return orderService.GetOrder(storeAlias);
        }

        public static OrderInfo AddOrderLine(Guid productId, IEnumerable<Guid> variantIds, string storeAlias, int quantity, OrderAction? action)
        {
            var orderService = container.Resolve<OrderService>();

            return orderService.AddOrderLine(productId, variantIds, quantity, storeAlias, action);
        }

        public static OrderInfo RemoveOrderLine(Guid lineId, string storeAlias)
        {
            var orderService = container.Resolve<OrderService>();

            return orderService.RemoveOrderLine(lineId, storeAlias);
        }

        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );
    }
}
