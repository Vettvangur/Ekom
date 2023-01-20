using Ekom.Extensions.Services;
using Ekom.Interfaces;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Core;
using Umbraco.NetPayment;

namespace Ekom
{
    /// <summary>
    /// Here we hook into the umbraco lifecycle methods to configure Ekom.Extensions
    /// </summary>
    public class EkomExtensionsStartup : IComposer
    {
        /// <summary>
        /// Umbraco startup lifecycle method
        /// </summary>
        public void Compose(IUmbracoBuilder builder)
        {
            // Hook into NetPayment completion event

            LocalCallbacks.Success += CompleteCheckout;
            builder.Services.AddScoped<INetPaymentService, NetPaymentService>();
        }

        private void CompleteCheckout(Umbraco.NetPayment.OrderStatus o)
        {
            var checkoutSvc = Configuration.Resolver.GetService<CheckoutService>();

            if (Guid.TryParse(o.Custom, out var orderId))
            {
                checkoutSvc.CompleteAsync(orderId).Wait();
            }
        }
    }
}
