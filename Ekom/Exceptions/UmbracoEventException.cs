using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class EventException : EkomException
    {
        /// <summary>
        /// 
        /// </summary>
        public EventException()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public EventException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public EventException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
