using Ekom.Models;
using Ekom.Payments;

namespace Ekom.Events
{
    public static class CheckoutEvents
    {
        public static event EventHandler<PayEventArgs> Pay;
        internal static void OnPay(object sender, PayEventArgs args)
            => Pay?.Invoke(sender, args);

        public static event EventHandler<ProcessingEventArgs> Processing;
        internal static void OnProcessing(object sender, ProcessingEventArgs args)
            => Processing?.Invoke(sender, args);

        public static event EventHandler<CompleteCheckoutEventArgs> CompleteCheckout;

        internal static void OnCompleteCheckout(object sender, CompleteCheckoutEventArgs args)
            => CompleteCheckout?.Invoke(sender, args);

    }

    public class PayEventArgs : EventArgs
    {
        public IOrderInfo OrderInfo { get; set; }
        public PaymentSettings PaymentSettings { get; set; }

        public Dictionary<string, string> CustomData { get; set; }
    }

    public class ProcessingEventArgs : EventArgs
    {
        public IOrderInfo OrderInfo { get; set; }
    }

    public class CompleteCheckoutEventArgs : EventArgs
    {
        public OrderData OrderData { get; set; }
        public IOrderInfo OrderInfo { get; set; }
        public bool StockValidation { get; set; } = true;
        public bool UpdateOrderStatus { get; set; } = true;
    }

}
