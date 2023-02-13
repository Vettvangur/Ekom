using System;

namespace Ekom.Payments.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class ExternalIndexNotFoundException : NetPaymentException
    {
        public ExternalIndexNotFoundException()
        {
        }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public ExternalIndexNotFoundException(string message) : base(message) { }

        public ExternalIndexNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
