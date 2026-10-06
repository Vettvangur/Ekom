namespace Ekom.Services;

internal sealed class CheckoutPaymentOperationScope : IAsyncDisposable
{
    private static readonly AsyncLocal<CheckoutPaymentOperationScope?> Ambient = new();
    private readonly Func<Task>? _release;
    private readonly CheckoutPaymentOperationScope? _parent;
    private bool _preserveOwnership;
    private bool _disposed;
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

    internal CheckoutPaymentOperationScope(Guid orderId, string owner, Func<Task>? release,
        CheckoutPaymentOperationScope? parent = null)
    {
        OrderId = orderId;
        Owner = owner;
        _release = release;
        _parent = parent;
    }

    // Activation must happen in the caller's execution context, after awaiting Begin.
    public IDisposable Enter()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CheckoutPaymentOperationScope));
        var previous = Ambient.Value;
        Ambient.Value = this;
        return new Activation(previous);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (!PreserveOwnership && _release != null) await _release().ConfigureAwait(false);
    }

    private sealed class Activation(CheckoutPaymentOperationScope? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }
}
