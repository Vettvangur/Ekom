using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// Indicates an error while performing stock changes.
    /// </summary>
    public class StockException : EkomException
    {
        /// <summary>
        /// Stock value in repository at time of exception
        /// </summary>
        public int? RepoValue { get; set; }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public StockException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StockException"/> class.
        /// </summary>
        public StockException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StockException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
        public StockException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
