namespace Ekom.Exceptions;

/// <summary>
/// Represents an exception that occurs during the checkout payment process in the Ekom system.
/// </summary>
public class CheckoutPayException : Exception
{
    public CheckoutPayException()
    {

    }
    public CheckoutPayException(string message) : base(message)
    {
    }

    public CheckoutPayException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
