using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// Indicates an error while rolling back stock changes.
    /// </summary>
    public class StockRollbackException : EkomException
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public StockRollbackException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StockRollbackException"/> class.
        /// </summary>
        public StockRollbackException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StockRollbackException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
        public StockRollbackException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
