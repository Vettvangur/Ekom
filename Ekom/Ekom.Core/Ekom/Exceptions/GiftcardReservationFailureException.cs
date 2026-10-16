using Ekom.Models;

namespace Ekom.Exceptions;

/// <summary>A failed reserve carrying partial or compensated external-effect evidence.</summary>
/// <remarks>Do not translate this into a generic checkout conflict before Ekom captures its receipt.</remarks>
public sealed class GiftcardReservationFailureException : EkomException
{
    public GiftcardReservationFailure Failure { get; }

    public GiftcardReservationFailureException(GiftcardReservationFailure failure)
        : base("Gift-card preparation failed; external effects require recovery verification.")
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    public GiftcardReservationFailureException(GiftcardReservationFailure failure, Exception innerException)
        : base("Gift-card preparation failed; external effects require recovery verification.", innerException)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }
}
