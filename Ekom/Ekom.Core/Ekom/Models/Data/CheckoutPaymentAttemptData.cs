using LinqToDB.Mapping;

namespace Ekom.Models;

[Table(Name = "EkomCheckoutPaymentAttempt")]
internal sealed class CheckoutPaymentAttemptData
{
    [PrimaryKey, NotNull]
    public Guid AttemptId { get; set; }
    [Column, NotNull]
    public Guid OrderId { get; set; }
    [Column, NotNull]
    public CheckoutPaymentAttemptState State { get; set; }
    [Column(Length = int.MaxValue)]
    public string? SubmittedOrderData { get; set; }
    [Column(Length = int.MaxValue)]
    public string? SubmittedOrderInfo { get; set; }
    [Column(Length = int.MaxValue), NotNull]
    public string ReservationIds { get; set; } = "[]";
    [Column, NotNull]
    public DateTime CreatedUtc { get; set; }
    [Column]
    public DateTime? SubmittedUtc { get; set; }
    [Column]
    public DateTime? ExpiresUtc { get; set; }
    [Column]
    public DateTime? ClosedUtc { get; set; }
}

internal enum CheckoutPaymentAttemptState
{
    Preparing, Submitted, ReleasePending, Released, CompletionPending, Completed, ReconciliationRequired,
}
