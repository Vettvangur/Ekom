using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// Indicates an error while performing stock changes.
    /// Most likely unable to decrement stock
    /// </summary>
    public class StockException : Exception
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public StockException(string message) : base(message) { }

        /// <summary>
        /// Stock value in repository at time of exception
        /// </summary>
        public int RepoValue { get; set; }
    }
}
