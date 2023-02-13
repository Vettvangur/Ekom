using System;

namespace Ekom.Payments.ValitorPay;

/// <summary>
/// Callbacks to run only for this provider on success/error.
/// Supplied by library consumer.
/// Local in this context is in contrast with callbacks to be performed after a remote provider response f.x.
/// </summary>
public static class LocalCallback
{
    /// <summary>
    /// Raises the success event on successful payment verification
    /// </summary>
    /// <param name="o"></param>
    internal static void OnSuccess(OrderStatus o)
    {
        Success?.Invoke(o);
        Events.OnSuccess(o);

    }

    /// <summary>
    /// Raises the success event on successful payment verification
    /// </summary>
    /// <param name="o"></param>
    /// <param name="ex"></param>
    internal static void OnError(OrderStatus o, Exception ex)
    {
        Error?.Invoke(o, ex);
        Events.OnError(o, ex);
    }
    /// <summary>
    /// Event fired on successful payment verification
    /// </summary>
    public static event Events.SuccessEvent Success;
    /// <summary>
    /// Event fired on payment verification error
    /// </summary>
    public static event Events.ErrorEvent Error;
}
