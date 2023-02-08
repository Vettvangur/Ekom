using Ekom.Models;

namespace Ekom.Events
{
    public static class CheckoutEvents
    {
        public static event EventHandler<PayEventArgs> PayEvent;
        internal static void OnPay(object sender, PayEventArgs args)
            => PayEvent?.Invoke(sender, args);

        public static event EventHandler<ProcessingEventArgs> ProcessingEvent;
        internal static void OnProcessing(object sender, ProcessingEventArgs args)
            => ProcessingEvent?.Invoke(sender, args);
    }

    public class PayEventArgs : EventArgs
    {
        public IOrderInfo OrderInfo { get; set; }

    }

    public class ProcessingEventArgs : EventArgs
    {
        public IOrderInfo OrderInfo { get; set; }
    }
}
