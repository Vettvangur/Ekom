using Ekom.Models;

namespace Ekom.Events;

public static class ProviderEvents
{
    public static event EventHandler<PaymentProvidersEventArgs>? BeforeReturnPaymentProviders;

    public static IEnumerable<IPaymentProvider> RaiseOnBeforeReturnPaymentProviders(IEnumerable<IPaymentProvider> providers, string storeAlias)
    {
        if (BeforeReturnPaymentProviders == null)
        {
            return providers;
        }

        var args = new PaymentProvidersEventArgs(providers, storeAlias);
        BeforeReturnPaymentProviders.Invoke(null, args);
        return args.Providers;
    }

    public class PaymentProvidersEventArgs : EventArgs
    {
        public PaymentProvidersEventArgs(IEnumerable<IPaymentProvider> providers, string storeAlias)
        {
            Providers = providers;
            StoreAlias = storeAlias;
        }

        /// <summary>
        /// Can be replaced or filtered by event handlers.
        /// </summary>
        public IEnumerable<IPaymentProvider> Providers { get; set; }
        public string StoreAlias { get; set; }
    }

    public static event EventHandler<ShippingProvidersEventArgs>? BeforeReturnShippingProviders;

    public static IEnumerable<IShippingProvider> RaiseOnBeforeReturnShippingProviders(IEnumerable<IShippingProvider> providers, string storeAlias)
    {
        if (BeforeReturnShippingProviders == null)
        {
            return providers;
        }

        var args = new ShippingProvidersEventArgs(providers, storeAlias);
        BeforeReturnShippingProviders.Invoke(null, args);
        return args.Providers;
    }

    public class ShippingProvidersEventArgs : EventArgs
    {
        public ShippingProvidersEventArgs(IEnumerable<IShippingProvider> providers, string storeAlias )
        {
            Providers = providers;
            StoreAlias = storeAlias;
        }

        /// <summary>
        /// Can be replaced or filtered by event handlers.
        /// </summary>
        public IEnumerable<IShippingProvider> Providers { get; set; }
        public string StoreAlias { get; set; }
    }
}
