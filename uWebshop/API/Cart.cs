using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Helpers;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.API
{
    public class Cart
    {

        public static OrderInfo GetCart(string storeAlias)
        {
            var orderService = new OrderService();

            return orderService.GetOrder(storeAlias);
        }

        public static OrderInfo AddOrderLine(Guid productId, IEnumerable<Guid> variantIds, string storeAlias, int quantity, CartAction? action)
        {
            var orderService = new OrderService();

            return orderService.AddOrderLine(productId, variantIds, quantity, storeAlias, action);
        }

        public static OrderInfo RemoveOrderLine(Guid lineId, string storeAlias)
        {
            var orderService = new OrderService();

            return orderService.RemoveOrderLine(lineId, storeAlias);
        }

        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );
    }
}
