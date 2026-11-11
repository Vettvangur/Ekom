namespace Ekom.Models.Manager;

/// <summary>
/// Everything the manager order drawer shows, loaded in one request.
/// A section other than <see cref="Order"/> is null when it could not be loaded,
/// so one failing section does not hide the order.
/// </summary>
public sealed class OrderDrawer
{
    public object? Order { get; set; }

    public IReadOnlyList<OrderActivityLogEntry>? Logs { get; set; }

    public IReadOnlyCollection<OrderManagerAction>? Actions { get; set; }

    public OrderLockStatus? LockStatus { get; set; }

    public IReadOnlyList<GiftcardRecoveryView>? GiftcardRecovery { get; set; }

    public IReadOnlyList<OrderReview>? Reviews { get; set; }
}
