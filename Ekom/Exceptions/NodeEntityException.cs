using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class NodeEntityException : Exception
    {
        /// <summary>
        /// 
        /// </summary>
        public NodeEntityException()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public NodeEntityException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public NodeEntityException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
