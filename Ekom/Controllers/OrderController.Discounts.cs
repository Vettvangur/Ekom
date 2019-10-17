using Ekom.API;
using Ekom.Exceptions;
using Ekom.Utilities;
using System;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Umbraco.Web.Mvc;

namespace Ekom.Controllers
{
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
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
        public async Task<ActionResult> ApplyCouponToOrder(string coupon, string storeAlias)
        {
            try
            {
                if (string.IsNullOrEmpty(coupon))
                {
                    return new HttpStatusCodeResult(400, "Coupon code can not be empty");
                }

                if (await Order.Instance.ApplyCouponToOrderAsync(coupon, storeAlias))
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
                var r = ExceptionHandler.Handle<HttpStatusCodeResult>(ex);
                if (r != null)
                {
                    return r;
                }

                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> RemoveCouponFromOrder(string storeAlias)
        {
            try
            {
                await Order.Instance.RemoveCouponFromOrderAsync(storeAlias);
                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                var r = ExceptionHandler.Handle<HttpStatusCodeResult>(ex);
                if (r != null)
                {
                    return r;
                }

                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> ApplyCouponToOrderLine(Guid productKey, string coupon, string storeAlias)
        {
            try
            {
                if (await Order.Instance.ApplyCouponToOrderLineAsync(productKey, coupon, storeAlias))
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
                var r = ExceptionHandler.Handle<HttpStatusCodeResult>(ex);
                if (r != null)
                {
                    return r;
                }

                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="OrderLineNotFoundException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public async Task<ActionResult> RemoveCouponFromOrderLine(Guid productKey, string storeAlias)
        {
            try
            {
                await Order.Instance.RemoveCouponFromOrderLineAsync(productKey, storeAlias);
                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                var r = ExceptionHandler.Handle<HttpStatusCodeResult>(ex);
                if (r != null)
                {
                    return r;
                }

                throw;
            }
        }
    }
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
}
