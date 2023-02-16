namespace Ekom.Payments;

/// <summary>
/// Event to run on success
/// </summary>
/// <param name="o"></param>
public class ErrorEventArgs : EventArgs
{
    public OrderStatus? OrderStatus { get; set; }

    public Exception? Exception { get; set; }
}
