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
    internal static void OnSuccess(object sender, SuccessEventArgs successEventArgs)
    {
        Success?.Invoke(sender, successEventArgs);
        Ekom.Payments.Events.OnSuccess(sender, successEventArgs);
    }
    
    /// <summary>
    /// Raises the error event on failed payments
    /// </summary>
    /// <param name="o"></param>
    /// <param name="ex"></param>
    internal static void OnError(object sender, ErrorEventArgs errorEventArgs)
    {
        Error?.Invoke(sender, errorEventArgs);
        Ekom.Payments.Events.OnError(sender, errorEventArgs);
    }
    
    /// <summary>
    /// Event fired on successful payment verification
    /// </summary>
    public static event EventHandler<SuccessEventArgs>? Success;
    /// <summary>
    /// Event fired on payment verification error
    /// </summary>
    public static event EventHandler<ErrorEventArgs>? Error;
}
