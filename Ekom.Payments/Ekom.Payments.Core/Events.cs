using System;

namespace Ekom.Payments;

/// <summary>
/// Events for all payment providers on success/error.
/// Supplied by library consumer.
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
    }

    /// <summary>
    /// Raises the success event on failed payment verification
    /// </summary>
    /// <param name="o"></param>
    /// <param name="ex"></param>
    internal static void OnError(OrderStatus o, Exception ex)
    {
        Error?.Invoke(o, ex);
    }

    /// <summary>
    /// Event fired on successful payment verification
    /// </summary>
    public static event SuccessEvent Success;
    /// <summary>
    /// Event fired on payment verification error
    /// </summary>
    public static event ErrorEvent Error;

    /// <summary>
    /// Event to run on success
    /// </summary>
    /// <param name="o"></param>
    public delegate void SuccessEvent(OrderStatus o);

    /// <summary>
    /// Event to run on error
    /// </summary>
    /// <param name="o">Errors can hit before we have <see cref="OrderStatus"/></param>
    /// <param name="ex">There can be errors without an <see cref="Exception"/></param>
    public delegate void ErrorEvent(OrderStatus o = null, Exception ex = null);
}
