using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class DiscountNotFoundException : Exception
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public DiscountNotFoundException(string message = "Unable to find discount") : base(message) { }
    }
}
