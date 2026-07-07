using Ekom.Models;

namespace Ekom.Klaviyo.Events;

public static class KlaviyoProductFeedEvents
{
#pragma warning disable CA1003 // Async event handlers need CancellationToken and ValueTask.
    public static event Func<KlaviyoProductFeedProductsEventArgs, CancellationToken, ValueTask>? ProductFeedProductsLoadingAsync;
#pragma warning restore CA1003

    public static async ValueTask InvokeProductFeedProductsLoadingAsync(
        KlaviyoProductFeedProductsEventArgs args,
        CancellationToken ct = default)
    {
        if (ProductFeedProductsLoadingAsync is null)
            return;

        foreach (var handler in ProductFeedProductsLoadingAsync.GetInvocationList())
        {
            ct.ThrowIfCancellationRequested();

            await ((Func<KlaviyoProductFeedProductsEventArgs, CancellationToken, ValueTask>)handler)(args, ct);
        }
    }
}

public sealed class KlaviyoProductFeedProductsEventArgs : EventArgs
{
    public required string StoreAlias { get; init; }

    public required string Culture { get; init; }

    public required IStore Store { get; init; }

    public IEnumerable<IProduct>? Products { get; set; }

    public bool Handled { get; set; }
}
