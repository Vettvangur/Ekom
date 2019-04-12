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
        public ActionResult ApplyCouponToOrder(string coupon, string storeAlias)
        {
            try
            {
                if (Order.Instance.ApplyCouponToOrder(coupon, storeAlias))
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

                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult RemoveCouponFromOrder(string storeAlias)
        {
            try
            {
                Order.Instance.RemoveCouponFromOrder(storeAlias);
                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
            catch (ArgumentException)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ActionResult ApplyCouponToOrderLine(Guid productKey, string coupon, string storeAlias)
        {
            try
            {
                if (Order.Instance.ApplyCouponToOrderLine(productKey, coupon, storeAlias))
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
        public ActionResult RemoveCouponFromOrderLine(Guid productKey, string storeAlias)
        {
            try
            {
                Order.Instance.RemoveCouponFromOrderLine(productKey, storeAlias);
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
