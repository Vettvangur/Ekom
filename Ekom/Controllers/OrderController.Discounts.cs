using Ekom.API;
using Ekom.Exceptions;
using System;
using System.Net;
using System.Web.Mvc;
using Umbraco.Web.Mvc;

namespace Ekom.Controllers
{
    /// <summary>
    /// Handles order/cart creation, updates and removals
    /// </summary>
    public partial class OrderController : SurfaceController
    {
        /// <summary>
        /// Http status code custom response
        /// </summary>
        public const int NoChangeResponse = 450;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult ApplyDiscountToOrder(Guid discountKey, string storeAlias)
        {
            try
            {
                if (Order.Current.ApplyDiscountToOrder(discountKey, storeAlias))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.OK);
                }
                else
                {
                    return new HttpStatusCodeResult(NoChangeResponse, "Discount not modified, better discount found");
                }
            }
            catch (Exception ex)
            {
                if (ex is DiscountNotFoundException
                || ex is ArgumentException)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                _log.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public void RemoveDiscountFromOrder(string storeAlias)
            => Order.Current.RemoveDiscountFromOrder(storeAlias);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult ApplyDiscountToOrderLine(Guid productKey, Guid discountKey, string storeAlias)
        {
            try
            {
                if (Order.Current.ApplyDiscountToOrderLine(productKey, discountKey, storeAlias))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.OK);
                }
                else
                {
                    return new HttpStatusCodeResult(NoChangeResponse, "Discount not modified, better discount found");
                }
            }
            catch (Exception ex)
            {
                if (ex is OrderLineNotFoundException
                || ex is DiscountNotFoundException
                || ex is ArgumentException)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public ActionResult RemoveDiscountFromOrderLine(Guid productKey, string storeAlias)
        {
            try
            {
                Order.Current.RemoveDiscountFromOrderLine(productKey, storeAlias);
                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                if (ex is OrderLineNotFoundException
                || ex is ArgumentException)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                throw;
            }
        }
    }
}
