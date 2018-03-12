using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// Indicates an error while rolling back stock changes.
    /// </summary>
    public class StockRollbackException : Exception
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public StockRollbackException(string message) : base(message) { }
    }
}
