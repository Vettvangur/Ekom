namespace Ekom.Exceptions;

/// <summary>
/// Common base type for all exceptions thrown by Ekom
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
