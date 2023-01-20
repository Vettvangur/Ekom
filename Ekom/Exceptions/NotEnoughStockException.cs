using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// </summary>
    public class NotEnoughStockException : EkomException
    {
        /// <summary>
        /// Stock value in repository at time of exception
        /// </summary>
        public int? RepoValue { get; set; }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public NotEnoughStockException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NotEnoughStockException"/> class.
        /// </summary>
        public NotEnoughStockException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NotEnoughStockException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
        public NotEnoughStockException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
