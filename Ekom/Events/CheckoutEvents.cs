using Ekom.Models;
using Ekom.Payments;

namespace Ekom.Events;

public static class CheckoutEvents
{
    public static event EventHandler<PayEventArgs> Pay;
    internal static void OnPay(object sender, PayEventArgs args)
        => Pay?.Invoke(sender, args);

    public static event Func<object, PayEventArgs, Task>? PayAsync;
    public static async Task OnPayAsync(object sender, PayEventArgs args)
    {
        if (PayAsync is null) return;

        foreach (var handler in PayAsync.GetInvocationList()
                 .Cast<Func<object, PayEventArgs, Task>>())
        {
            await handler(sender, args);
        }
    }

    public static event EventHandler<ProcessingEventArgs> Processing;
    internal static void OnProcessing(object sender, ProcessingEventArgs args)
        => Processing?.Invoke(sender, args);

    public static event Func<object, ProcessingEventArgs, Task>? ProcessingAsync;
    public static async Task OnProcessingAsync(object sender, ProcessingEventArgs args)
    {
        if (ProcessingAsync is null) return;

        foreach (var handler in ProcessingAsync.GetInvocationList()
                 .Cast<Func<object, ProcessingEventArgs, Task>>())
        {
            await handler(sender, args);
        }
    }

    public static event EventHandler<CompleteCheckoutEventArgs> CompleteCheckout;

    internal static void OnCompleteCheckout(object sender, CompleteCheckoutEventArgs args)
        => CompleteCheckout?.Invoke(sender, args);

    public static event Func<object, CompleteCheckoutEventArgs, Task> CompleteCheckoutAsync;

    internal static async Task OnCompleteCheckoutAsync(object sender, CompleteCheckoutEventArgs args)
    {
        if (CompleteCheckoutAsync != null)
        {
            foreach (var handler in CompleteCheckoutAsync.GetInvocationList().Cast<Func<object, CompleteCheckoutEventArgs, Task>>())
            {
                await handler(sender, args).ConfigureAwait(false);
            }
        }
    }

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
    public bool StockValidation { get; set; } = true;
}

public class CompleteCheckoutEventArgs : EventArgs
{
    public required OrderData OrderData { get; set; }
    public required IOrderInfo OrderInfo { get; set; }
    public bool StockValidation { get; set; } = true;
    public bool UpdateOrderStatus { get; set; } = true;
}
