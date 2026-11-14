namespace Ekom.Payments.Exceptions;

/// <summary>The requested base payment provider is not installed.</summary>
public class PaymentProviderNotFoundException : Exception
{
    public PaymentProviderNotFoundException()
    {
    }

    public PaymentProviderNotFoundException(string message) : base(message)
    {
    }

    public PaymentProviderNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
