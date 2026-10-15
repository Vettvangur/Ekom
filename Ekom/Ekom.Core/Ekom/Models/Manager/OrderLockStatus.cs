namespace Ekom.Models.Manager;

public sealed class OrderLockStatus
{
    public bool HasLock => PaymentOperationLocked || PreparationLocked || ReconciliationRequired;

    public bool PaymentOperationLocked { get; init; }

    public bool PreparationLocked { get; init; }

    public bool ReconciliationRequired { get; init; }

    public Guid? AttemptId { get; init; }

    public string? AttemptState { get; init; }
}
