using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// Indicates an error while performing order changes.
    /// </summary>
    public class OrderException : Exception
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public OrderException(string message) : base(message) { }
    }
}
