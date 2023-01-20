using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// Indicates an error while performing order changes.
    /// </summary>
    public class OrderLineNegativeException : EkomException
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public OrderLineNegativeException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderLineNegativeException"/> class.
        /// </summary>
        public OrderLineNegativeException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderLineNegativeException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
        public OrderLineNegativeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
