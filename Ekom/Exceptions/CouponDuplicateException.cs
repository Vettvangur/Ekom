using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class CouponDuplicateException : Exception
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public CouponDuplicateException(string message = "Duplicate coupon found, skipping discount") : base(message) { }
    }
}
