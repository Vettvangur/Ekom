using Ekom.Models;
using Ekom.Utilities;

namespace Ekom.Events;

public static class ProviderEvents
{
    // ============================
    // Payment providers
    // ============================
    public static event EventHandler<PaymentProvidersEventArgs>? BeforeReturnPaymentProviders;

    public static event Func<object, PaymentProvidersEventArgs, CancellationToken, Task>? BeforeReturnPaymentProvidersAsync;

    public static IEnumerable<IPaymentProvider> RaiseOnBeforeReturnPaymentProviders(
        IEnumerable<IPaymentProvider> providers,
        string storeAlias)
    {
        if (BeforeReturnPaymentProviders is null)
            return providers;

        var args = new PaymentProvidersEventArgs(providers, storeAlias);
        BeforeReturnPaymentProviders.Invoke(null, args);
        return args.Providers;
    }

    /// <summary>
    /// Async pipeline (preferred).
    /// </summary>
    public static async Task<IEnumerable<IPaymentProvider>> RaiseOnBeforeReturnPaymentProvidersAsync(
        IEnumerable<IPaymentProvider> providers,
        string storeAlias,
        CancellationToken ct = default)
    {
        // No handlers at all -> return fast
        if (BeforeReturnPaymentProvidersAsync is null && BeforeReturnPaymentProviders is null)
            return providers;

        var args = new PaymentProvidersEventArgs(providers, storeAlias);

        // 1) Run legacy sync handlers first (backwards compatibility)
        BeforeReturnPaymentProviders?.Invoke(null, args);

        // 2) Run async handlers
        await AsyncEventInvoker.InvokeAsync(BeforeReturnPaymentProvidersAsync, sender: null!, args, ct)
            .ConfigureAwait(false);

        return args.Providers;
    }

    public sealed class PaymentProvidersEventArgs : EventArgs
    {
        public PaymentProvidersEventArgs(IEnumerable<IPaymentProvider> providers, string storeAlias)
        {
            Providers = providers;
            StoreAlias = storeAlias;
        }

        /// <summary>Can be replaced or filtered by event handlers.</summary>
        public IEnumerable<IPaymentProvider> Providers { get; set; }

        public string StoreAlias { get; set; }
    }

    // ============================
    // Shipping providers
    // ============================

    public static event EventHandler<ShippingProvidersEventArgs>? BeforeReturnShippingProviders;

    public static event Func<object, ShippingProvidersEventArgs, CancellationToken, Task>? BeforeReturnShippingProvidersAsync;

    public static IEnumerable<IShippingProvider> RaiseOnBeforeReturnShippingProviders(
        IEnumerable<IShippingProvider> providers,
        string storeAlias)
    {
        if (BeforeReturnShippingProviders is null)
            return providers;

        var args = new ShippingProvidersEventArgs(providers, storeAlias);
        BeforeReturnShippingProviders.Invoke(null, args);
        return args.Providers;
    }

    /// <summary>
    /// Async pipeline (preferred).
    /// </summary>
    public static async Task<IEnumerable<IShippingProvider>> RaiseOnBeforeReturnShippingProvidersAsync(
        IEnumerable<IShippingProvider> providers,
        string storeAlias,
        CancellationToken ct = default)
    {
        // No handlers at all -> return fast
        if (BeforeReturnShippingProvidersAsync is null && BeforeReturnShippingProviders is null)
            return providers;

        var args = new ShippingProvidersEventArgs(providers, storeAlias);

        // 1) Run legacy sync handlers first (backwards compatibility)
        BeforeReturnShippingProviders?.Invoke(null, args);

        // 2) Run async handlers
        await AsyncEventInvoker.InvokeAsync(BeforeReturnShippingProvidersAsync, sender: null!, args, ct)
            .ConfigureAwait(false);

        return args.Providers;
    }

    public sealed class ShippingProvidersEventArgs : EventArgs
    {
        public ShippingProvidersEventArgs(IEnumerable<IShippingProvider> providers, string storeAlias)
        {
            Providers = providers;
            StoreAlias = storeAlias;
        }

        /// <summary>Can be replaced or filtered by event handlers.</summary>
        public IEnumerable<IShippingProvider> Providers { get; set; }

        public string StoreAlias { get; set; }
    }
}
