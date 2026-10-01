namespace Ekom.Exceptions;

/// <summary>
/// A whole-order coupon application was rejected by a pre-application event handler.
/// The message contains the customer-facing rejection reason.
/// </summary>
public class CouponApplicationRejectedException : EkomException
{
    public CouponApplicationRejectedException() : base("Coupon application was rejected.")
    {
    }

    public CouponApplicationRejectedException(string message) : base(message)
    {
    }

    public CouponApplicationRejectedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
