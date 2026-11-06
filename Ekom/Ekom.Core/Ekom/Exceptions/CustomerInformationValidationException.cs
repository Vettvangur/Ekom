namespace Ekom.Exceptions;

internal sealed class CustomerInformationValidationException : ArgumentException
{
    public CustomerInformationValidationException(string message) : base(message)
    {
    }
}
