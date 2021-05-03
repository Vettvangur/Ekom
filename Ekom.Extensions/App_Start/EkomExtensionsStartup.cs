using Ekom.Extensions.Services;
using Ekom.Services;
using System;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.NetPayment;

namespace Ekom
{
    /// <summary>
    /// Here we hook into the umbraco lifecycle methods to configure Ekom.Extensions
    /// </summary>
    public class EkomExtensionsStartup : IUserComposer
    {
        /// <summary>
        /// Umbraco startup lifecycle method
        /// </summary>
        public void Compose(Composition composition)
        {
            // Hook into NetPayment completion event

            LocalCallbacks.Success += CompleteCheckout;
        }

        private void CompleteCheckout(OrderStatus o)
        {
            var checkoutSvc = Current.Factory.GetInstance<CheckoutService>();

            if (Guid.TryParse(o.Custom, out var orderId))
            {
                checkoutSvc.CompleteAsync(orderId).Wait();
            }
        }
    }
}
