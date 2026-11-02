namespace Ekom.Exceptions;

/// <summary>Invalid order custom data supplied by a caller.</summary>
public sealed class OrderCustomDataException : ArgumentException
{
    public OrderCustomDataException() { }
    public OrderCustomDataException(string message, Exception innerException) : base(message, innerException) { }
    public OrderCustomDataException(string message) : base(message) { }
}
