namespace Ekom.Services;

internal sealed class CheckoutPaymentOperationScope : IAsyncDisposable
{
    private static readonly AsyncLocal<CheckoutPaymentOperationScope?> Ambient = new();
    private readonly Func<Task>? _release;
    private readonly CheckoutPaymentOperationScope? _parent;
    private bool _preserveOwnership;
    private bool _submissionInProgress;
    private volatile bool _disposed;
    private readonly object _disposeGate = new();
    private Task? _disposeTask;
    internal static CheckoutPaymentOperationScope? Current => Ambient.Value;
    public Guid OrderId { get; }
    public string Owner { get; }
    public Guid? AttemptId { get; internal set; }
    public bool PreserveOwnership
    {
        get => _parent?.PreserveOwnership ?? _preserveOwnership;
        set
        {
            if (_parent != null) _parent.PreserveOwnership = value;
            else _preserveOwnership = value;
        }
    }
    internal bool IsCheckout { get; set; }
    // Remains true until SubmitAsync has returned successfully, including transaction disposal.
    // Nested scopes must communicate this boundary to the outer owner's cleanup callback.
    internal bool SubmissionInProgress
    {
        get => _parent?.SubmissionInProgress ?? _submissionInProgress;
        set
        {
            if (_parent != null) _parent.SubmissionInProgress = value;
            else _submissionInProgress = value;
        }
    }
    internal bool IsOrderInformationUpdate { get; set; }
    internal bool IsManagerOverride { get; init; }
    // Nested information updates retain an enclosing cart/checkout operation's authority.
    internal bool IsInformationOnly { get; set; }
    internal bool AllowsCustomerProviderUpdates { get; set; }

    internal CheckoutPaymentOperationScope(Guid orderId, string owner, Func<Task>? release,
        CheckoutPaymentOperationScope? parent = null)
    {
        OrderId = orderId;
        Owner = owner;
        _release = release;
        _parent = parent;
    }

    internal static CheckoutPaymentOperationScope BeginManagerOverride(Guid orderId)
        => new(orderId, string.Empty, null) { IsManagerOverride = true };

    // Recovery actions must acquire real database ownership, never borrow a manager override.
    internal static IDisposable Suppress()
    {
        var previous = Ambient.Value;
        Ambient.Value = null;
        return new Activation(previous);
    }

    // Activation must happen in the caller's execution context, after awaiting Begin.
    public IDisposable Enter()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CheckoutPaymentOperationScope));
        var previous = Ambient.Value;
        Ambient.Value = this;
        return new Activation(previous);
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            // Share in-progress/successful cleanup. A failed cleanup may be retried,
            // but disposal permanently prevents this scope from authorizing new work.
            _disposed = true;
            if (_disposeTask == null || _disposeTask.IsFaulted || _disposeTask.IsCanceled)
                _disposeTask = ReleaseAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task ReleaseAsync()
    {
        if (!PreserveOwnership && _release != null) await _release().ConfigureAwait(false);
    }

    private sealed class Activation(CheckoutPaymentOperationScope? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }
}
