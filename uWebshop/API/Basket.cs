using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.API
{
    public static class Basket
    {
        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        public static BasketService GetBasket()
        {
            var basket = BasketService.GetBasket();

            return basket;
        }

        public static void AddToBasket(int productId, int[] variantIds, int quantity)
        {
            var basket = GetBasket();

            basket.AddItem(productId, quantity, variantIds);

        }

        public static void AddToBasket(int productId, int variantId, int quantity)
        {
            var basket = GetBasket();

            basket.AddItem(productId, quantity, variantId);

        }
    }
}
