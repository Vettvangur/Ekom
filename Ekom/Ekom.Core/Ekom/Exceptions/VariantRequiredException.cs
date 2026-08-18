namespace Ekom.Exceptions;

/// <summary>
/// The product has variants but no variant was selected for the order line.
/// </summary>
public class VariantRequiredException : EkomException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VariantRequiredException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public VariantRequiredException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VariantRequiredException"/> class.
    /// </summary>
    public VariantRequiredException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VariantRequiredException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public VariantRequiredException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
