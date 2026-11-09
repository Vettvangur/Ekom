using Ekom.Models;

namespace Ekom.Printer.Services;

/// <summary>Durably captures a saved order for an explicitly triggered station.</summary>
public interface IPickingSlipCapture
{
    /// <summary>
    /// Capture after committing warehouse metadata, before completing dispatch work.
    /// Order ID, store and station form the stable idempotency key. The first snapshot wins.
    /// Success confirms durable capture, not rendering or physical printing.
    /// </summary>
    Task<PickingSlipCaptureResult> CaptureAsync(OrderData savedOrder, string stationAlias,
        CancellationToken cancellationToken = default);
}

/// <summary>Created is false when this order/store/station already has a capture or job.</summary>
public sealed record PickingSlipCaptureResult(string JobId, bool Created);
