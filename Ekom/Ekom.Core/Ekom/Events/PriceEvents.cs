namespace Ekom.Events;

/// <summary>Post-commit notifications. Handlers should enqueue work, not perform remote calls.</summary>
public static class PriceEvents
{
    public static event EventHandler<PriceChangedEventArgs>? PriceChanged;

    internal static Delegate[] Handlers => PriceChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
}

public sealed class PriceChangedEventArgs : EventArgs
{
    public required Guid Key { get; init; }
    public required string StoreAlias { get; init; }
    public required string Currency { get; init; }
    public required string PropertyAlias { get; init; }
    public decimal? OldValue { get; init; }
    /// <summary>Null means the override was removed; node fallback now applies.</summary>
    public decimal? NewValue { get; init; }
}
