namespace Ekom.Payments;

/// <summary>
/// Event to run on success
/// </summary>
/// <param name="o"></param>
public class SuccessEventArgs : EventArgs
{
    public OrderStatus OrderStatus { get; set; }
}
