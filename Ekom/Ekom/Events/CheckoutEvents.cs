using Ekom.Models;
using Ekom.Payments;
using Ekom.Utilities;

namespace Ekom.Events;

public static class CheckoutEvents
{
    // Sync events
    public static event EventHandler<PayEventArgs>? Pay;
    internal static void OnPay(object sender, PayEventArgs args) => Pay?.Invoke(sender, args);

    public static event EventHandler<ProcessingEventArgs>? Processing;
    internal static void OnProcessing(object sender, ProcessingEventArgs args) => Processing?.Invoke(sender, args);

    public static event EventHandler<CompleteCheckoutEventArgs>? CompleteCheckout;
    internal static void OnCompleteCheckout(object sender, CompleteCheckoutEventArgs args) => CompleteCheckout?.Invoke(sender, args);

    // Async events (cancellable)
    public static event Func<object, PayEventArgs, CancellationToken, Task>? PayAsync;
    public static Task OnPayAsync(object sender, PayEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(PayAsync, sender, args, ct);

    public static event Func<object, ProcessingEventArgs, CancellationToken, Task>? ProcessingAsync;
    public static Task OnProcessingAsync(object sender, ProcessingEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(ProcessingAsync, sender, args, ct);

    public static event Func<object, CompleteCheckoutEventArgs, CancellationToken, Task>? CompleteCheckoutAsync;
    internal static Task OnCompleteCheckoutAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(CompleteCheckoutAsync, sender, args, ct);
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
