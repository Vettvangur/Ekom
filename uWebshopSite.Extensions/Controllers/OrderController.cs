using log4net;
using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Web.Mvc;
using Umbraco.Web.Mvc;
using uWebshop.App_Start;
using uWebshop.Helpers;
using uWebshop.Services;

namespace uWebshopSite.Extensions.Controllers
{
	/// <summary>
	/// Handles order/cart creation, updates and removals
	/// </summary>
	[PluginController("uWebshop")]
	public class OrderController : SurfaceController
	{
		private ILog _log;
		private OrderService _orderSvc;
		/// <summary>
		/// ctor
		/// We can't hook into the MVC DI resolver since that would override consumer resolvers.
		/// </summary>
		public OrderController()
		{
			var container = UnityConfig.GetConfiguredContainer();

			_orderSvc = container.Resolve<OrderService>();

			var logFac = container.Resolve<ILogFactory>();
			_log = logFac.GetLogger(typeof(OrderController));
		}

		/// <summary>
		/// Add product to order
		/// </summary>
		/// <param name="productId">Guid Key of product</param>
		/// <param name="storeAlias"></param>
		/// <param name="quantity"></param>
		/// <param name="variantId"></param>
		/// <param name="action">Add/Update</param>
		/// <returns></returns>
		public JsonResult AddToOrder(Guid productId, string storeAlias, int quantity, Guid? variantId = null, OrderAction? action = null)
		{
			try
			{
				var variantIds = new List<Guid>();

				if (variantId != null && variantId != Guid.Empty)
				{
					variantIds.Add(variantId.Value);
				}

				var orderInfo = _orderSvc.AddOrderLine(productId, variantIds, quantity, storeAlias, action);

				return Json(new
				{
					success = true,
					orderInfo = orderInfo
				});
			}
			catch (Exception ex)
			{
				_log.Error("Add to order Failed!", ex);
				return Json(new
				{
					success = false,
					error = ex.Message
				});
			}
		}

		/// <summary>
		/// Update order, change quantity of line in cart/order
		/// </summary>
		/// <param name="lineId">Guid Key of line to update</param>
		/// <param name="storeAlias"></param>
		/// <param name="quantity"></param>
		/// <returns></returns>
		public JsonResult UpdateOrder(Guid lineId, string storeAlias, int quantity)
		{
			try
			{
				var orderInfo = _orderSvc.AddOrderLine(lineId, null, quantity, storeAlias, null);

				return Json(new
				{
					success = true,
					orderInfo = orderInfo
				});
			}
			catch (Exception ex)
			{
				_log.Error("Add to order Failed!", ex);
				return Json(new
				{
					success = false,
					error = ex.Message
				});
			}
		}

		/// <summary>
		/// Remove product from order/cart
		/// </summary>
		/// <param name="lineId">Guid Key of product/line</param>
		/// <param name="storeAlias"></param>
		/// <returns></returns>
		public JsonResult RemoveOrderLine(Guid lineId, string storeAlias)
		{
			try
			{
				var orderInfo = _orderSvc.RemoveOrderLine(lineId, storeAlias);

				return Json(new
				{
					success = true,
					orderInfo = orderInfo
				});
			}
			catch (Exception ex)
			{
				_log.Error("Remove orderline from order Failed!", ex);
				return Json(new
				{
					success = false,
					error = ex.Message
				});
			}
		}
	}
}
