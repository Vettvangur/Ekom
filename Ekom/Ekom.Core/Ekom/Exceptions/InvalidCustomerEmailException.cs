namespace Ekom.Exceptions;

internal sealed class InvalidCustomerEmailException : FormatException
{
    public InvalidCustomerEmailException() : base("Invalid email address.")
    {
    }
}
