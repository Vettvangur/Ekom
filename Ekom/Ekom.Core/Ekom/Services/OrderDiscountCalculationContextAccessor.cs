namespace Ekom.Services;

public sealed class OrderDiscountCalculationContextAccessor
{
    private readonly AsyncLocal<IReadOnlyDictionary<string, string>?> _current = new();

    public IReadOnlyDictionary<string, string>? Current => _current.Value;

    public IDisposable? Activate(IReadOnlyDictionary<string, string>? pricingContext)
    {
        if (pricingContext == null)
        {
            return null;
        }

        var previous = _current.Value;
        _current.Value = new Dictionary<string, string>(
            pricingContext,
            StringComparer.OrdinalIgnoreCase);

        return new Scope(this, previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly OrderDiscountCalculationContextAccessor _owner;
        private readonly IReadOnlyDictionary<string, string>? _previous;
        private bool _disposed;

        public Scope(
            OrderDiscountCalculationContextAccessor owner,
            IReadOnlyDictionary<string, string>? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _owner._current.Value = _previous;

            _disposed = true;
        }
    }
}
