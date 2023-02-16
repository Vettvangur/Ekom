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
    internal static void OnSuccess(object sender, SuccessEventArgs successEventArgs)
    {
        Success?.Invoke(sender, successEventArgs);
    }

    /// <summary>
    /// Raises the success event on failed payment verification
    /// </summary>
    internal static void OnError(object sender, ErrorEventArgs errorEventArgs)
    {
        Error?.Invoke(sender, errorEventArgs);
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
