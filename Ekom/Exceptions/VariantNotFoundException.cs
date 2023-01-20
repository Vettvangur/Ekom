using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class VariantNotFoundException : EkomException
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public VariantNotFoundException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="VariantNotFoundException"/> class.
        /// </summary>
        public VariantNotFoundException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VariantNotFoundException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
        public VariantNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
