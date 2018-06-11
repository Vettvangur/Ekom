using Ekom.Services;
using System;
using Umbraco.Core;
using Umbraco.NetPayment;

namespace Ekom
{
    /// <summary>
    /// Here we hook into the umbraco lifecycle methods to configure Ekom.Extensions
    /// We use ApplicationEventHandler so that these lifecycle methods are only run
    /// when umbraco is in a stable condition.
    /// </summary>
    public class EkomExtensionsStartup : ApplicationEventHandler
    {
        /// <summary>
        /// Umbraco startup lifecycle method
        /// </summary>
        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            // Hook into NetPayment completion event

            LocalCallbacks.Success += new LocalCallbacks.successCallback(CompleteCheckout);
        }

        private void CompleteCheckout(OrderStatus o)
        {
            var checkoutSvc = Configuration.container.GetInstance<CheckoutService>();

            if (Guid.TryParse(o.Custom, out var orderId))
            {
                checkoutSvc.Complete(orderId);
            }

            
        }
    }
}
