using log4net;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Umbraco.NetPayment;
using Umbraco.NetPayment.API;
using Umbraco.Web.Mvc;
using uWebshop.API;
using uWebshop.Models.Data;
using ILogFactory = uWebshop.Services.ILogFactory;

namespace uWebshop.Extensions.Controllers
{
    /// <summary>
    /// Offers a default way to complete checkout using uWebshop v3
    /// </summary>
    [PluginController("uWebshop")]
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
        /// Complete payment using the Standard uWebshop v3 checkout controller
        /// </summary>
        /// <param name="paymentRequest"></param>
        /// <param name="form"></param>
        /// <returns></returns>
        public async Task<string> Pay(PaymentRequest paymentRequest, FormCollection form)
        {
            var order = Order.Current.GetOrder();
            var uwbsPP = Providers.Current.GetPaymentProvider(paymentRequest.PaymentProvider);

            var pp = NetPayment.Current.GetPaymentProvider(uwbsPP.Title);

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
                var uwbsSP = Providers.Current.GetShippingProvider(paymentRequest.ShippingProvider);
                var orderItemsList = orderItems.ToList();
                orderItemsList.Add(new OrderItem
                {
                    GrandTotal = uwbsSP.Price.Value,
                    Price = uwbsSP.Price.Value,
                    Title = uwbsSP.Title,
                    Quantity = 1,
                });

                orderItems = orderItemsList;
            }

            return await pp.RequestAsync(
                order.ChargedAmount.Value,
                orderItems,
                skipReceipt: true,
                culture: Umbraco.CultureDictionary.Culture.TwoLetterISOLanguageName,
                member: Umbraco.MembershipHelper.GetCurrentMemberId(),
                orderCustomString: "uWebshop v3 Store"
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
