using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class StoreNotFoundException : EkomException
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public StoreNotFoundException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="StoreNotFoundException"/> class.
        /// </summary>
        public StoreNotFoundException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StoreNotFoundException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
        public StoreNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
