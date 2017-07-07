using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Umbraco.Web.Mvc;
using uWebshop.Helpers;

namespace uWebshopSite.Extensions.Controllers
{
    public class CartController : SurfaceController
    {
        public JsonResult AddToCart(Guid productId, string storeAlias, int quantity, Guid? variantId = null, CartAction? action = null)
        {

            try
            {

                var variantIds = new List<Guid>();

                if (variantId != null && variantId != Guid.Empty)
                {
                    variantIds.Add(variantId.Value);
                }

                var orderInfo = uWebshop.API.Cart.AddOrderLine(productId, variantIds, storeAlias, quantity, action);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });

            } catch(Exception ex)
            {

                Log.Error("Add to cart Failed!",ex);
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }

        }

        public JsonResult RemoveCartLine(Guid lineId, string storeAlias)
        {

            try
            {

                var orderInfo = uWebshop.API.Cart.RemoveOrderLine(lineId,storeAlias);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });

            }
            catch (Exception ex)
            {

                Log.Error("Remove orderline from cart Failed!", ex);
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }

        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
