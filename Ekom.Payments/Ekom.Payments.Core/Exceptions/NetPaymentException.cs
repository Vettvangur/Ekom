using System;

namespace Ekom.Payments.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class NetPaymentException : Exception
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public NetPaymentException(string message) : base(message) { }

        public NetPaymentException()
        {
        }

        public NetPaymentException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
