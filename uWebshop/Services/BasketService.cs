using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using uWebshop.Models;

namespace uWebshop.Services
{
    public class BasketService
    {
        public List<BasketLine> Items { get; private set; }

        public static Guid GetBasketIdFromCookie()
        {

            string storeAlias = string.Empty;

            var r = (ContentRequest)HttpContext.Current.Cache["uwbsRequest"];

            if (r != null && r.Store != null)
            {
                storeAlias = "-" + r.Store.Alias;
            }

            if (HttpContext.Current.Request.Cookies["uwbsOrder" + storeAlias] != null)
            {
                //return the SessionGUID
                return new Guid(HttpContext.Current.Request.Cookies["uwbsOrder" + storeAlias].Value);
            }
            else//new visit
            {
                //set cookie to a new random Guid
                var _guid = Guid.NewGuid();
                HttpCookie guidCookie = new HttpCookie("uwbsOrder" + storeAlias);
                guidCookie.Value = _guid.ToString();
                guidCookie.Expires = DateTime.Now.AddDays(1d);
                HttpContext.Current.Response.Cookies.Add(guidCookie);
                return _guid;
            }
        }

        public static BasketService GetBasket()
        {

            var basketId = GetBasketIdFromCookie();

            if (basketId == Guid.Empty)
            {

            }

            var r = (ContentRequest)HttpContext.Current.Cache["uwbsRequest"];

            if (r != null && r.Store != null)
            {
                var key = "uwbsBasket-" + r.Store.Alias;

                // If the cart is not in the session, create one and put it there
                if (HttpContext.Current.Session[key] == null)
                {
                    BasketService basket = new BasketService();
                    basket.Items = new List<BasketLine>();
                    HttpContext.Current.Session[key] = basket;
                }

                return (BasketService)HttpContext.Current.Session[key];
            }

            return (BasketService)HttpContext.Current.Session["uwbsBasket"];


        }

        protected BasketService() { }
        public void AddItem(int productId, int quantity, int[] variantIds = null)
        {
            // Create a new item to add to the cart
            BasketLine basketItem = new BasketLine(productId, variantIds);

            // If this item already exists in our list of items, increase the quantity
            // Otherwise, add the new item to the list
            if (Items.Contains(basketItem))
            {
                foreach (var item in Items)
                {
                    if (item.Equals(basketItem))
                    {
                        item.Quantity += quantity;
                        return;
                    }
                }
            }
            else
            {
                basketItem.Quantity = quantity;
                Items.Add(basketItem);
            }

        }

        public void AddItem(int productId, int quantity, int variantId) {

            int[] variantIds = new int[0];

            if (variantId != 0)
            {
                variantIds = new int[variantId];
            }

            // Create a new item to add to the cart
            BasketLine basketItem = new BasketLine(productId, variantIds);

            // If this item already exists in our list of items, increase the quantity
            // Otherwise, add the new item to the list
            if (Items.Contains(basketItem))
            {
                foreach (var item in Items)
                {
                    if (item.Equals(basketItem))
                    {
                        item.Quantity += quantity;
                        return;
                    }
                }
            }
            else
            {
                basketItem.Quantity = quantity;
                Items.Add(basketItem);
            }

        }
    }
}
