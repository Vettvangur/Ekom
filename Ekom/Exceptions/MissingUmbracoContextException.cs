using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class MissingUmbracoContextException : EkomException
    {
        /// <summary>
        /// 
        /// </summary>
        public MissingUmbracoContextException()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public MissingUmbracoContextException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public MissingUmbracoContextException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
