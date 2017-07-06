using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core;
using uWebshop.Interfaces;
using uWebshop.Models;

namespace uWebshop.Services
{
    public class CartService
    {
        public Guid GetIdFromCookie()
        {
            string storeAlias = string.Empty;

            var appCache = ApplicationContext.Current.ApplicationCache;
            var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

            if (r != null && r.Store != null)
            {
                storeAlias = "-" + r.Store.Alias;
            }

            if (HttpContext.Current.Request.Cookies["uwbsCart-" + storeAlias] != null)
            {
                //return the SessionGUID
                return new Guid(HttpContext.Current.Request.Cookies["uwbsCart-" + storeAlias].Value);
            }
            else//new visit
            {
                //set cookie to a new random Guid
                //var _guid = Guid.NewGuid();
                //HttpCookie guidCookie = new HttpCookie("uwbsCart" + storeAlias);
                //guidCookie.Value = _guid.ToString();
                //guidCookie.Expires = DateTime.Now.AddDays(1d);
                //HttpContext.Current.Response.Cookies.Add(guidCookie);
                //return _guid;
            }
            return Guid.Empty;
        }
        public ICart GetCart()
        {
            var httpContext = HttpContext.Current;
            var appCache = ApplicationContext.Current.ApplicationCache;

            // Get Cart UniqueId from Cookie.
            var cartId = GetIdFromCookie();

            // If Cookie Exist then return Cart
            if (cartId != Guid.Empty)
            {
                var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

                var key = "uwbsCart-" + r.Store.Alias;

                if (r != null && r.Store != null)
                {
                    
                    // If the cart is not in the session, fetch order from sql and insert to session
                    if (httpContext.Session[key] == null)
                    {
                        var cart = new Cart(cartId);
                        httpContext.Session[key] = cart;
                    }

                    return (ICart)httpContext.Session[key];
                }

                return (ICart)httpContext.Session[key];

            } else
            {
                return null;
            }

        }
        internal ICart CreateCart()
        {
            return null;
        }
    }
}
