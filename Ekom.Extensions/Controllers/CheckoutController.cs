using Ekom.API;
using Ekom.Models.Data;
using log4net;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Umbraco.NetPayment;
using Umbraco.NetPayment.API;
using Umbraco.Web.Mvc;
using ILogFactory = Ekom.Services.ILogFactory;

namespace Ekom.Extensions.Controllers
{
    /// <summary>
    /// Offers a default way to complete checkout using Ekom
    /// </summary>
    [PluginController("Ekom")]
    public class CheckoutController : SurfaceController
    {
        ILog _log;
        Configuration _config;
        //IDatabaseFactory _dbFac;

        /// <summary>
        /// ctor
        /// </summary>
        public CheckoutController()
        {
            var logFac = Configuration.container.GetInstance<ILogFactory>();
            _log = logFac.GetLogger(typeof(CheckoutController));

            _config = Configuration.container.GetInstance<Configuration>();
            //_dbFac = Configuration.container.GetInstance<IDatabaseFactory>();
        }

        /// <summary>
        /// Complete payment using the Standard Ekom checkout controller
        /// </summary>
        /// <param name="paymentRequest"></param>
        /// <param name="form"></param>
        /// <returns></returns>
        public async Task<string> Pay(PaymentRequest paymentRequest, FormCollection form)
        {
            var order = Order.Current.GetOrder();
            var ekomPP = Providers.Current.GetPaymentProvider(paymentRequest.PaymentProvider);

            var pp = NetPayment.Current.GetPaymentProvider(ekomPP.Name);

            if (_config.StoreCustomerData)
            {
                using (var db = DatabaseContext.Database)
                {
                    db.Insert(new CustomerData
                    {
                        // Unfinished
                    });
                }
            }

            var orderItems = order.OrderLines.Select(x => new OrderItem
            {
                GrandTotal = x.Amount.Value,
                Price = x.Product.OriginalPrice,
                Title = x.Product.Title,
                Quantity = x.Quantity,
            });

            if (paymentRequest.ShippingProvider != Guid.Empty)
            {
                var ekomSP = Providers.Current.GetShippingProvider(paymentRequest.ShippingProvider);
                var orderItemsList = orderItems.ToList();
                orderItemsList.Add(new OrderItem
                {
                    GrandTotal = ekomSP.Price.Value,
                    Price = ekomSP.Price.Value,
                    Title = ekomSP.Title,
                    Quantity = 1,
                });

                orderItems = orderItemsList;
            }

            return await pp.RequestAsync(
                order.ChargedAmount.Value,
                orderItems,
                skipReceipt: true,
                culture: order.StoreInfo.Alias,
                member: Umbraco.MembershipHelper.GetCurrentMemberId(),
                orderCustomString: order.UniqueId.ToString()
            );
        }
    }

    /// <summary>
    /// Unfinished
    /// </summary>
    public class PaymentRequest
    {
        public Guid PaymentProvider { get; set; }

        public Guid ShippingProvider { get; set; }
    }
}
