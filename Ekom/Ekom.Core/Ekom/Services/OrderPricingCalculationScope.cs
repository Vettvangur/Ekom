using Ekom.Models;

namespace Ekom.Services;

internal sealed class OrderPricingCalculationScope : IDisposable
{
    private static readonly AsyncLocal<State?> _current = new();

    private readonly State? _previous;
    private readonly State _state;
    private bool _disposed;

    private OrderPricingCalculationScope(IOrderInfo orderInfo)
    {
        _previous = _current.Value;
        _state = _previous != null
            && ReferenceEquals(_previous.OrderInfo, orderInfo)
            && ReferenceEquals(_previous.Discount, orderInfo.Discount)
                ? _previous
                : new State(orderInfo, orderInfo.Discount);
        _current.Value = _state;
    }

    internal IReadOnlyDictionary<Guid, decimal> Allocations => _state.Allocations.Value;

    internal static OrderPricingCalculationScope Enter(IOrderInfo orderInfo)
    {
        ArgumentNullException.ThrowIfNull(orderInfo);
        return new OrderPricingCalculationScope(orderInfo);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _current.Value = _previous;
        _disposed = true;
    }

    private sealed class State
    {
        internal State(IOrderInfo orderInfo, OrderedDiscount? discount)
        {
            OrderInfo = orderInfo;
            Discount = discount;
            Allocations = new Lazy<IReadOnlyDictionary<Guid, decimal>>(
                () => discount == null
                    ? new Dictionary<Guid, decimal>()
                    : OrderDiscountQuantityAllocator.Allocate(orderInfo, discount),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        internal IOrderInfo OrderInfo { get; }
        internal OrderedDiscount? Discount { get; }
        internal Lazy<IReadOnlyDictionary<Guid, decimal>> Allocations { get; }
    }
}
