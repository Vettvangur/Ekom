using Ekom.Models;

namespace Ekom.Events;

public static class ProviderEvents
{
    public static event EventHandler<PaymentProvidersEventArgs>? BeforeReturnPaymentProviders;

    public static IEnumerable<IPaymentProvider> RaiseOnBeforeReturnPaymentProviders(IEnumerable<IPaymentProvider> providers)
    {
        if (BeforeReturnPaymentProviders == null)
        {
            return providers;
        }

        var args = new PaymentProvidersEventArgs(providers);
        BeforeReturnPaymentProviders.Invoke(null, args);
        return args.Providers;
    }

    public class PaymentProvidersEventArgs : EventArgs
    {
        public PaymentProvidersEventArgs(IEnumerable<IPaymentProvider> providers)
        {
            Providers = providers;
        }

        /// <summary>
        /// Can be replaced or filtered by event handlers.
        /// </summary>
        public IEnumerable<IPaymentProvider> Providers { get; set; }
    }

    public static event EventHandler<ShippingProvidersEventArgs>? BeforeReturnShippingProviders;

    public static IEnumerable<IShippingProvider> RaiseOnBeforeReturnShippingProviders(IEnumerable<IShippingProvider> providers)
    {
        if (BeforeReturnShippingProviders == null)
        {
            return providers;
        }

        var args = new ShippingProvidersEventArgs(providers);
        BeforeReturnShippingProviders.Invoke(null, args);
        return args.Providers;
    }

    public class ShippingProvidersEventArgs : EventArgs
    {
        public ShippingProvidersEventArgs(IEnumerable<IShippingProvider> providers)
        {
            Providers = providers;
        }

        /// <summary>
        /// Can be replaced or filtered by event handlers.
        /// </summary>
        public IEnumerable<IShippingProvider> Providers { get; set; }
    }
}
