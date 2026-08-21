namespace Ekom;

/// <summary>
/// Ambient, per-async-flow pricing context (e.g. "vitaminAdvisorApproved", customer group).
/// Ekom activates it around price-sensitive work such as order discount calculation and forwards
/// the current value into pricing related event args (<c>DiscountEvents</c>, <c>PriceCache</c>),
/// so event handlers can read it from the args instead of relying on ambient state directly.
/// </summary>
public static class PricingContext
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, string>?> _current = new();

    private static readonly IReadOnlyDictionary<string, string> _empty =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Current context for this async flow, or null when none is active.</summary>
    public static IReadOnlyDictionary<string, string>? Current => _current.Value;

    /// <summary>Current context or an empty, case-insensitive dictionary. Never null.</summary>
    public static IReadOnlyDictionary<string, string> CurrentOrEmpty => _current.Value ?? _empty;

    /// <summary>
    /// Activates <paramref name="pricingContext"/> for the current async flow until the returned
    /// scope is disposed. Keys are compared case-insensitively. Passing null activates nothing
    /// and returns null.
    /// </summary>
    public static IDisposable? Activate(IReadOnlyDictionary<string, string>? pricingContext)
    {
        if (pricingContext == null)
        {
            return null;
        }

        var previous = _current.Value;
        _current.Value = new Dictionary<string, string>(pricingContext, StringComparer.OrdinalIgnoreCase);

        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly IReadOnlyDictionary<string, string>? _previous;
        private bool _disposed;

        public Scope(IReadOnlyDictionary<string, string>? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _current.Value = _previous;
            _disposed = true;
        }
    }
}
