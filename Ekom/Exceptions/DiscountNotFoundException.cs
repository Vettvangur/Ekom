using System;

namespace Ekom.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class DiscountNotFoundException : EkomException
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public DiscountNotFoundException(string message = "Unable to find discount") : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscountNotFoundException"/> class.
        /// </summary>
        public DiscountNotFoundException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscountNotFoundException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
        public DiscountNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class DiscountHasNoUsageException : EkomException
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public DiscountHasNoUsageException(string message = "Coupon has no usage.") : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscountHasNoUsageException"/> class.
        /// </summary>
        public DiscountHasNoUsageException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscountHasNoUsageException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
        public DiscountHasNoUsageException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class DiscountUnableToFindCouponException : EkomException
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="message"></param>
        public DiscountUnableToFindCouponException(string message = "Unable to find discount with coupon.") : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscountUnableToFindCouponException"/> class.
        /// </summary>
        public DiscountUnableToFindCouponException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscountUnableToFindCouponException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
        public DiscountUnableToFindCouponException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
