using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class StoreConstructorException : EkomException
    {
        /// <summary>
        /// 
        /// </summary>
        public StoreConstructorException()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public StoreConstructorException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public StoreConstructorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
