using System;

namespace Ekom.Payments.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class PaymentProviderNotFoundException : NetPaymentException
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public PaymentProviderNotFoundException(string message) : base(message) { }

        public PaymentProviderNotFoundException()
        {
        }

        public PaymentProviderNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
