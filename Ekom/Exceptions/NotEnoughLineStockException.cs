using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// </summary>
    public class NotEnoughLineStockException : NotEnoughStockException
    {
        /// <summary>
        /// 
        /// </summary>
        public bool? Variant { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public Guid OrderLineKey { get; set; }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public NotEnoughLineStockException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NotEnoughLineStockException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
        public NotEnoughLineStockException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NotEnoughLineStockException"/> class.
        /// </summary>
        public NotEnoughLineStockException()
        {
        }
    }
}
