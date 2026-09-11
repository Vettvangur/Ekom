using Ekom.Models;

namespace Ekom.Services;

// The SQL preparation owner fences other requests; AsyncLocal carries that capability
// through unchanged legacy virtual hooks and same-call order saves.
internal sealed class CheckoutPreparationScope : IDisposable
{
    private static readonly AsyncLocal<CheckoutPreparationScope?> Ambient = new();
    private readonly CheckoutPreparationScope? _previous;
    internal static CheckoutPreparationScope? Current => Ambient.Value;
    internal CheckoutReservationService Service { get; }
    internal CheckoutPreparationData Ownership { get; }
    internal IOrderInfo Order { get; }
    internal HashSet<string> Created { get; } = new(StringComparer.Ordinal);
    internal HashSet<string> Reused { get; } = new(StringComparer.Ordinal);
    internal List<Func<Task>> CompensationSaves { get; } = new();
    internal bool UncertainSave { get; set; }
    internal bool IncludeInventoryRequirements { get; set; } = true;

    internal CheckoutPreparationScope(CheckoutReservationService service, CheckoutPreparationData ownership, IOrderInfo order)
    {
        Service = service;
        Ownership = ownership;
        Order = order;
        _previous = Ambient.Value;
        Ambient.Value = this;
    }

    public void Dispose() => Ambient.Value = _previous;
}
