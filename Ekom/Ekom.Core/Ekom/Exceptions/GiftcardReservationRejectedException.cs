namespace Ekom.Exceptions;

/// <summary>
/// A giftcard reservation was rejected with confirmed absence of external hold changes.
/// </summary>
/// <remarks>
/// Integrations may throw this only after confirming that the entire ReserveAsync call
/// created no new holds and changed no existing holds, including any partially processed
/// selections. Existing holds and their persisted claim metadata must remain valid and
/// unchanged. A timeout, unknown provider outcome, or rejection message alone is not
/// sufficient confirmation. Use another exception when any effect is uncertain.
/// </remarks>
public sealed class GiftcardReservationRejectedException : EkomException
{
    public GiftcardReservationRejectedException() : base("Giftcard reservation was rejected without changing external holds.")
    {
    }

    public GiftcardReservationRejectedException(string message) : base(message)
    {
    }

    public GiftcardReservationRejectedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
