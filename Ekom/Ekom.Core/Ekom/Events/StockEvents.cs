using Ekom.Utilities;

namespace Ekom.Events;

public static class StockEvents
{
    public static event Func<object, StockChangedEventArgs, CancellationToken, Task>? StockChangedAsync;

    public static Task OnStockChangedAsync(object sender, StockChangedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(StockChangedAsync, sender, args, ct);
}

public sealed class StockChangedEventArgs : EventArgs
{
    public Guid Key { get; init; }
    public string? StoreAlias { get; init; }
    public decimal OldValue { get; init; }
    public decimal NewValue { get; init; }
}
