using System;

namespace Ekom.Payments.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class EnsureNodesException : NetPaymentException
    {
        /// <summary>
        /// 
        /// </summary>
        public EnsureNodesException()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public EnsureNodesException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public EnsureNodesException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
