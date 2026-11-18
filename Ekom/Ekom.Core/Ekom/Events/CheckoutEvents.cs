using Ekom.Models;
using Ekom.Payments;
using Ekom.Utilities;
using System.Collections.ObjectModel;

namespace Ekom.Events;

public static class CheckoutEvents
{
    // Sync events
    public static event EventHandler<PayEventArgs>? Pay;
    internal static void OnPay(object sender, PayEventArgs args) => Pay?.Invoke(sender, args);

    public static event EventHandler<ProcessingEventArgs>? Processing;
    internal static void OnProcessing(object sender, ProcessingEventArgs args) => Processing?.Invoke(sender, args);

    /// <summary>
    /// Raised before a paid order is completed. Use it to change how the order completes, through
    /// <see cref="CompletingCheckoutEventArgs.StockValidation"/> and <see cref="CompletingCheckoutEventArgs.UpdateOrderStatus"/>.
    /// The order can still fail to complete after this event, so do not send it to another system from here.
    /// </summary>
    public static event EventHandler<CompletingCheckoutEventArgs>? CompletingCheckout;

    /// <summary>
    /// Raised after an order has been completed: its stock is taken, its coupon is counted and its status is set.
    /// It is not raised when completion fails. Use it to send the order to another system or to send emails.
    /// A handler that throws does not undo the order. Ekom logs the failure and runs the other handlers.
    /// </summary>
    public static event EventHandler<CompleteCheckoutEventArgs>? CompleteCheckout;

    public static event EventHandler<PaymentOrderItemsPreparingEventArgs>? PaymentOrderItemsPreparing;
    internal static void OnPaymentOrderItemsPreparing(object sender, PaymentOrderItemsPreparingEventArgs args) => PaymentOrderItemsPreparing?.Invoke(sender, args);

    // Async events (cancellable)
    public static event Func<object, PayEventArgs, CancellationToken, Task>? PayAsync;
    public static Task OnPayAsync(object sender, PayEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(PayAsync, sender, args, ct);

    public static event Func<object, ProcessingEventArgs, CancellationToken, Task>? ProcessingAsync;
    public static Task OnProcessingAsync(object sender, ProcessingEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(ProcessingAsync, sender, args, ct);

    /// <inheritdoc cref="CompletingCheckout"/>
    public static event Func<object, CompletingCheckoutEventArgs, CancellationToken, Task>? CompletingCheckoutAsync;

    /// <inheritdoc cref="CompleteCheckout"/>
    public static event Func<object, CompleteCheckoutEventArgs, CancellationToken, Task>? CompleteCheckoutAsync;

    /// <summary>
    /// Raises <see cref="CompletingCheckout"/> and <see cref="CompletingCheckoutAsync"/>. See <see cref="RaiseIsolatedAsync{T}"/>.
    /// Cancellation passes through: the order is not completed yet.
    /// </summary>
    internal static Task<IReadOnlyList<Exception>> RaiseCompletingCheckoutIsolatedAsync(object sender, CompletingCheckoutEventArgs args, CancellationToken ct = default)
        => RaiseIsolatedAsync(CompletingCheckout, CompletingCheckoutAsync, sender, args, cancellationPassesThrough: true, ct);

    /// <summary>
    /// Raises <see cref="CompleteCheckout"/> and <see cref="CompleteCheckoutAsync"/>. See <see cref="RaiseIsolatedAsync{T}"/>.
    /// Nothing a handler throws passes through: the order is already completed.
    /// </summary>
    internal static Task<IReadOnlyList<Exception>> RaiseCompleteCheckoutIsolatedAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct = default)
        => RaiseIsolatedAsync(CompleteCheckout, CompleteCheckoutAsync, sender, args, cancellationPassesThrough: false, ct);

    /// <summary>
    /// Raises an event one handler at a time, so that a handler that throws stops neither the other handlers nor
    /// the completion of a paid order. Returns what the handlers threw.
    /// </summary>
    private static async Task<IReadOnlyList<Exception>> RaiseIsolatedAsync<T>(
        EventHandler<T>? sync,
        Func<object, T, CancellationToken, Task>? async,
        object sender,
        T args,
        bool cancellationPassesThrough,
        CancellationToken ct)
        where T : EventArgs
    {
        var failures = new List<Exception>();

        foreach (var handler in sync?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try
            {
                ((EventHandler<T>)handler)(sender, args);
            }
#pragma warning disable CA1031 // A subscriber's failure must not fail the checkout
            catch (Exception ex) when (!cancellationPassesThrough || ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                failures.Add(ex);
            }
        }

        foreach (var handler in async?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try
            {
                await ((Func<object, T, CancellationToken, Task>)handler)(sender, args, ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // A subscriber's failure must not fail the checkout
            catch (Exception ex) when (!cancellationPassesThrough || ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                failures.Add(ex);
            }
        }

        return failures;
    }

    public static event Func<object, PaymentOrderItemsPreparingEventArgs, CancellationToken, Task>? PaymentOrderItemsPreparingAsync;
    public static Task OnPaymentOrderItemsPreparingAsync(object sender, PaymentOrderItemsPreparingEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(PaymentOrderItemsPreparingAsync, sender, args, ct);
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

public class PaymentOrderItemsPreparingEventArgs : EventArgs
{
    public required IOrderInfo OrderInfo { get; set; }
    public required PaymentRequest PaymentRequest { get; set; }
    public required Collection<OrderItem> OrderItems { get; set; }
}

/// <summary>The settings a handler can change before a paid order is completed.</summary>
public class CompletingCheckoutEventArgs : EventArgs
{
    /// <summary>The order as it is before completion.</summary>
    public required IOrderInfo OrderInfo { get; set; }

    /// <summary>Set to false to complete the order without checking product stock.</summary>
    public bool StockValidation { get; set; } = true;

    /// <summary>Set to false to leave the order status as it is. False from the start for an offline payment.</summary>
    public bool UpdateOrderStatus { get; set; } = true;

    /// <summary>The payment the order was paid with, when there is one.</summary>
    public Services.OrderPaymentAttempt? Payment { get; set; }
}

/// <summary>A completed order. <see cref="OrderData"/> and <see cref="OrderInfo"/> are read after completion.</summary>
public class CompleteCheckoutEventArgs : EventArgs
{
    public required OrderData OrderData { get; set; }
    public required IOrderInfo OrderInfo { get; set; }

    /// <summary>Whether the order was completed with a stock check. Setting it here has no effect.</summary>
    [Obsolete("CompleteCheckout is raised after the order has completed, so setting this has no effect. Set it in a CheckoutEvents.CompletingCheckoutAsync handler.")]
    public bool StockValidation { get; set; } = true;

    /// <summary>Whether completion set the order status. Setting it here has no effect.</summary>
    [Obsolete("CompleteCheckout is raised after the order has completed, so setting this has no effect. Set it in a CheckoutEvents.CompletingCheckoutAsync handler.")]
    public bool UpdateOrderStatus { get; set; } = true;

    /// <summary>
    /// The payment the order was paid with: provider, transaction ID, authorization code, card brand and
    /// masked number, payment method, amounts and times, the provider's own details, and its response through
    /// <see cref="Services.OrderPaymentAttempt.GetData{T}"/>. Null when no payment is recorded on the order,
    /// for example when completion is called directly.
    /// </summary>
    public Services.OrderPaymentAttempt? Payment { get; set; }
}
