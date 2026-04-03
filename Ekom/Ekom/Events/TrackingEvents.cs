using Ekom.Models;
using Ekom.Tracking;
using Ekom.Utilities;

namespace Ekom.Events;

public static class TrackingEvents
{
    public static event Func<object, Ga4PurchasePreparingEventArgs, CancellationToken, Task>? Ga4PurchasePreparingAsync;
    public static event Func<object, MetaPurchasePreparingEventArgs, CancellationToken, Task>? MetaPurchasePreparingAsync;

    public static Task OnGa4PurchasePreparingAsync(object sender, Ga4PurchasePreparingEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(Ga4PurchasePreparingAsync, sender, args, ct);

    public static Task OnMetaPurchasePreparingAsync(object sender, MetaPurchasePreparingEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(MetaPurchasePreparingAsync, sender, args, ct);
}

public sealed class Ga4PurchasePreparingEventArgs : EventArgs
{
    public required IOrderInfo OrderInfo { get; set; }
    public required Ga4PurchaseRequest Request { get; set; }
    public bool Cancel { get; set; }
    public Dictionary<string, object?> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MetaPurchasePreparingEventArgs : EventArgs
{
    public required IOrderInfo OrderInfo { get; set; }
    public required MetaPurchaseRequest Request { get; set; }
    public bool Cancel { get; set; }
    public Dictionary<string, object?> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}
