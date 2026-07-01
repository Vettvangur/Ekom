namespace Ekom.Exceptions;

/// <summary>
/// 
/// </summary>
public class EkomRootNodeException : EkomException
{
    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="message"></param>
    public EkomRootNodeException(string message = "Ekom root node not found") : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="EkomRootNodeException"/> class.
    /// </summary>
    public EkomRootNodeException()
    {
    }

    public EkomRootNodeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
