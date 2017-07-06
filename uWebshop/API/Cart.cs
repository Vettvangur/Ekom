using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.API
{
    public class Cart
    {

        public static OrderInfo GetCart()
        {
            var orderService = new OrderService();

            return orderService.GetOrder();
        }

        public static OrderInfo AddOrderLine()
        {
            var orderService = new OrderService();

            var variants = new List<Guid>();

            variants.Add(new Guid("7e81bcf4-8bd8-47cd-83a0-9c05e3ed6bad"));

            return orderService.AddOrderLine(new Guid("584f7b36-b87d-4605-8169-254da1f66dca"),variants,2,"IS");
        }

        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );
    }
}
