using System;

namespace Ekom.Payments.Valitor;

/// <summary>
/// Callbacks to run only for this provider on success/error.
/// Supplied by library consumer.
/// Local in this context is in contrast with callbacks to be performed after a remote provider response f.x.
/// </summary>
public static class Events
{
    /// <summary>
    /// Raises the success event on successful payment verification
    /// </summary>
    /// <param name="o"></param>
    internal static void OnSuccess(OrderStatus o)
    {
        Success?.Invoke(o);
        Ekom.Payments.Events.OnSuccess(o);
    }
    
    /// <summary>
    /// Raises the error event on failed payments
    /// </summary>
    /// <param name="o"></param>
    /// <param name="ex"></param>
    internal static void OnError(OrderStatus o, Exception ex)
    {
        Error?.Invoke(o, ex);
        Ekom.Payments.Events.OnError(o, ex);
    }
    
    /// <summary>
    /// Event fired on successful payment verification
    /// </summary>
    public static event Ekom.Payments.Events.SuccessEvent Success;
    /// <summary>
    /// Event fired on payment verification error
    /// </summary>
    public static event Ekom.Payments.Events.ErrorEvent Error;
}
