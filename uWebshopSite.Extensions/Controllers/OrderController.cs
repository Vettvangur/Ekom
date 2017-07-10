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
    public class OrderController : SurfaceController
    {
        public JsonResult AddToOrder(Guid productId, string storeAlias, int quantity, Guid? variantId = null, OrderAction? action = null)
        {

            try
            {
                var variantIds = new List<Guid>();

                if (variantId != null && variantId != Guid.Empty)
                {
                    variantIds.Add(variantId.Value);
                }

                var orderInfo = uWebshop.API.Order.AddOrderLine(productId, variantIds, storeAlias, quantity, action);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });

            } catch(Exception ex)
            {

                Log.Error("Add to order Failed!",ex);
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }

        }

        public JsonResult UpdateOrder(Guid lineId, string storeAlias, int quantity)
        {

            try
            {
                var orderInfo = uWebshop.API.Order.AddOrderLine(lineId, null, storeAlias, quantity, null);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });

            }
            catch (Exception ex)
            {

                Log.Error("Add to order Failed!", ex);
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }

        }

        public JsonResult RemoveOrderLine(Guid lineId, string storeAlias)
        {

            try
            {

                var orderInfo = uWebshop.API.Order.RemoveOrderLine(lineId,storeAlias);

                return Json(new
                {
                    success = true,
                    orderInfo = orderInfo
                });

            }
            catch (Exception ex)
            {

                Log.Error("Remove orderline from order Failed!", ex);
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
