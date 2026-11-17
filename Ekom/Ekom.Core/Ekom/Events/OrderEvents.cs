using Ekom.API;
using Ekom.Models;
using Ekom.Utilities;

namespace Ekom.Events;

public static class OrderEvents
{
    // ----------------------------
    // Order updated
    // ----------------------------

    /// <summary>Event to fire on <see cref="IOrderInfo"/> updates</summary>
    public static event EventHandler<OrderUpdatedEventArgs>? OrderUpdated;

    internal static void OnOrderUpdated(object sender, OrderUpdatedEventArgs args)
        => OrderUpdated?.Invoke(sender, args);

    public static event Func<object, OrderUpdatedEventArgs, CancellationToken, Task>? OrderUpdatedAsync;

    public static Task OnOrderUpdatedAsync(object sender, OrderUpdatedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(OrderUpdatedAsync, sender, args, ct);

    // ----------------------------
    // Customer email added
    // ----------------------------

    public static event Func<object, CustomerEmailAddedEventArgs, CancellationToken, Task>? CustomerEmailAddedAsync;

    public static Task OnCustomerEmailAddedAsync(object sender, CustomerEmailAddedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(CustomerEmailAddedAsync, sender, args, ct);

    // ----------------------------
    // Customer information updating/updated
    // ----------------------------

    public static event Func<object, CustomerInformationUpdatingEventArgs, CancellationToken, Task>? CustomerInformationUpdatingAsync;

    public static Task OnCustomerInformationUpdatingAsync(object sender, CustomerInformationUpdatingEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(CustomerInformationUpdatingAsync, sender, args, ct);


    public static event Func<object, CustomerInformationUpdatedEventArgs, CancellationToken, Task>? CustomerInformationUpdatedAsync;

    public static Task OnCustomerInformationUpdatedAsync(object sender, CustomerInformationUpdatedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(CustomerInformationUpdatedAsync, sender, args, ct);

    // ----------------------------
    // Order updating
    // ----------------------------

    public static event EventHandler<OrderUpdatingEventArgs>? OrderUpdating;

    internal static void OnOrderUpdating(object sender, OrderUpdatingEventArgs args)
        => OrderUpdating?.Invoke(sender, args);

    public static event Func<object, OrderUpdatingEventArgs, CancellationToken, Task>? OrderUpdatingAsync;

    public static Task OnOrderUpdatingAsync(object sender, OrderUpdatingEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(OrderUpdatingAsync, sender, args, ct);

    // ----------------------------
    // Status changed: order, payment, fulfilment
    // ----------------------------

    /// <summary>
    /// Raised every time an order's status (Draft, Open, Closed, Cancelled) changes, after the change is stored.
    /// The arguments carry the order and the status before and after.
    /// </summary>
    public static event Func<object, OrderStatusChangedEventArgs, CancellationToken, Task>? OrderStatusChangedAsync;

    public static Task OnOrderStatusChangedAsync(object sender, OrderStatusChangedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(OrderStatusChangedAsync, sender, args, ct);

    /// <summary>
    /// Raised every time an order's payment status changes, after the change is stored.
    /// The arguments carry the order and the payment status before and after.
    /// </summary>
    public static event Func<object, PaymentStatusChangedEventArgs, CancellationToken, Task>? PaymentStatusChangedAsync;

    public static Task OnPaymentStatusChangedAsync(object sender, PaymentStatusChangedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(PaymentStatusChangedAsync, sender, args, ct);

    /// <summary>
    /// Raised every time an order's fulfilment status changes, after the change is stored.
    /// The arguments carry the order and the fulfilment status before and after.
    /// </summary>
    public static event Func<object, FulfillmentStatusChangedEventArgs, CancellationToken, Task>? FulfillmentStatusChangedAsync;

    public static Task OnFulfillmentStatusChangedAsync(object sender, FulfillmentStatusChangedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(FulfillmentStatusChangedAsync, sender, args, ct);

    private static readonly AsyncLocal<bool> _statusChangesSuppressed = new();

    /// <summary>
    /// Keeps the status change events quiet for the saves made until the result is disposed.
    /// For a caller that was asked not to fire events, such as a manager change with events switched off.
    /// </summary>
    internal static IDisposable SuppressStatusChanges() => new StatusChangeSuppression();

    private sealed class StatusChangeSuppression : IDisposable
    {
        private readonly bool _previous = _statusChangesSuppressed.Value;

        public StatusChangeSuppression() => _statusChangesSuppressed.Value = true;

        public void Dispose() => _statusChangesSuppressed.Value = _previous;
    }

    /// <summary>
    /// Raises one event for each status of a stored order that differs from <paramref name="before"/>.
    /// Call it after the change is committed.
    /// </summary>
    internal static async Task RaiseStatusChangesAsync(object sender, OrderData order, Models.Orders.OrderStatuses before, CancellationToken ct)
    {
        if (_statusChangesSuppressed.Value) return;
        var after = Models.Orders.OrderStateRules.Statuses(order);
        if (after == before) return;

        // The change is already stored. An order whose stored content cannot be read still gets its events.
        IOrderInfo? orderInfo;
        try { orderInfo = new OrderInfo(order); }
        catch (Exception) { orderInfo = null; }

        if (after.Order is { } orderStatus && orderStatus != before.Order)
        {
            await OnOrderStatusChangedAsync(sender, new OrderStatusChangedEventArgs
            {
                OrderUniqueId = order.UniqueId,
                OrderInfo = orderInfo,
                PreviousStatus = before.Order,
                Status = orderStatus,
            }, ct).ConfigureAwait(false);
        }

        if (after.Payment is { } paymentStatus && paymentStatus != before.Payment)
        {
            await OnPaymentStatusChangedAsync(sender, new PaymentStatusChangedEventArgs
            {
                OrderUniqueId = order.UniqueId,
                OrderInfo = orderInfo,
                PreviousStatus = before.Payment,
                Status = paymentStatus,
            }, ct).ConfigureAwait(false);
        }

        if (after.Fulfillment is { } fulfillmentStatus && fulfillmentStatus != before.Fulfillment)
        {
            await OnFulfillmentStatusChangedAsync(sender, new FulfillmentStatusChangedEventArgs
            {
                OrderUniqueId = order.UniqueId,
                OrderInfo = orderInfo,
                PreviousStatus = before.Fulfillment,
                Status = fulfillmentStatus,
            }, ct).ConfigureAwait(false);
        }
    }

    // ----------------------------
    // Order lines
    // ----------------------------

    public static event EventHandler<AddingOrderlineEventArgs>? AddingOrderline;

    internal static void OnAddingOrderline(object sender, AddingOrderlineEventArgs args)
        => AddingOrderline?.Invoke(sender, args);

    public static event Func<object, AddingOrderlineEventArgs, CancellationToken, Task>? AddingOrderlineAsync;

    public static Task OnAddingOrderlineAsync(object sender, AddingOrderlineEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(AddingOrderlineAsync, sender, args, ct);

    public static event EventHandler<AddedOrderlineEventArgs>? AddedOrderline;

    internal static void OnAddedOrderline(object sender, AddedOrderlineEventArgs args)
        => AddedOrderline?.Invoke(sender, args);

    public static event Func<object, AddedOrderlineEventArgs, CancellationToken, Task>? AddedOrderlineAsync;

    public static Task OnAddedOrderlineAsync(object sender, AddedOrderlineEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(AddedOrderlineAsync, sender, args, ct);

    public static event Func<object, RemovedOrderlineEventArgs, CancellationToken, Task>? RemovedOrderlineAsync;

    public static Task OnRemovedOrderlineAsync(object sender, RemovedOrderlineEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(RemovedOrderlineAsync, sender, args, ct);

    public static event EventHandler<UpdatedOrderlineEventArgs>? UpdatedOrderline;

    internal static void OnUpdatedOrderline(object sender, UpdatedOrderlineEventArgs args)
        => UpdatedOrderline?.Invoke(sender, args);

    public static event Func<object, UpdatedOrderlineEventArgs, CancellationToken, Task>? UpdatedOrderlineAsync;

    public static Task OnUpdatedOrderlineAsync(object sender, UpdatedOrderlineEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(UpdatedOrderlineAsync, sender, args, ct);

    public static event Func<object, ShippingProviderAddedEventArgs, CancellationToken, Task>? ShippingProviderAddedAsync;

    public static Task OnShippingProviderAddedAsync(object sender, ShippingProviderAddedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(ShippingProviderAddedAsync, sender, args, ct);

    /// <summary>Raised after a payment transaction was stored and the order's payment status recalculated.</summary>
    public static event Func<object, PaymentRecordedEventArgs, CancellationToken, Task>? PaymentRecordedAsync;

    public static Task OnPaymentRecordedAsync(object sender, PaymentRecordedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(PaymentRecordedAsync, sender, args, ct);

    /// <summary>Raised after a shipment was created or something that happened to it was stored.</summary>
    public static event Func<object, ShipmentRecordedEventArgs, CancellationToken, Task>? ShipmentRecordedAsync;

    public static Task OnShipmentRecordedAsync(object sender, ShipmentRecordedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(ShipmentRecordedAsync, sender, args, ct);

    public static event Func<object, PaymentProviderAddedEventArgs, CancellationToken, Task>? PaymentProviderAddedAsync;

    public static Task OnPaymentProviderAddedAsync(object sender, PaymentProviderAddedEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(PaymentProviderAddedAsync, sender, args, ct);
}

/// <summary>A shipment event recorded on an order.</summary>
public sealed class ShipmentRecordedEventArgs : EventArgs
{
    public required Models.Shipments.OrderShipment Shipment { get; init; }
    public required Models.Shipments.OrderShipmentEvent Event { get; init; }

    /// <summary>The shipment's state before the event. Null when the shipment was just created.</summary>
    public Models.Shipments.ShipmentState? PreviousState { get; init; }
}

/// <summary>A payment transaction recorded on an order.</summary>
public sealed class PaymentRecordedEventArgs : EventArgs
{
    public required Models.Payments.OrderPayment Payment { get; init; }
    public required Models.Payments.OrderPaymentTransaction Transaction { get; init; }
    public Models.Orders.PaymentStatus? PreviousPaymentStatus { get; init; }
    public Models.Orders.PaymentStatus? PaymentStatus { get; init; }
}

/// <summary>An order's status (Draft, Open, Closed, Cancelled) changed and the change was stored.</summary>
public sealed class OrderStatusChangedEventArgs : EventArgs
{
    public required Guid OrderUniqueId { get; init; }

    /// <summary>The order as it was stored with the new status. Null when the stored order could not be read.</summary>
    public IOrderInfo? OrderInfo { get; init; }

    /// <summary>The order status before the change. Null when the order's history did not say.</summary>
    public Models.Orders.OrderStatus? PreviousStatus { get; init; }

    /// <summary>The order status the order has now.</summary>
    public required Models.Orders.OrderStatus Status { get; init; }
}

/// <summary>An order's payment status changed and the change was stored.</summary>
public sealed class PaymentStatusChangedEventArgs : EventArgs
{
    public required Guid OrderUniqueId { get; init; }

    /// <summary>The order as it was stored with the new payment status. Null when the stored order could not be read.</summary>
    public IOrderInfo? OrderInfo { get; init; }

    /// <summary>The payment status before the change. Null when the order's history did not say.</summary>
    public Models.Orders.PaymentStatus? PreviousStatus { get; init; }

    /// <summary>The payment status the order has now.</summary>
    public required Models.Orders.PaymentStatus Status { get; init; }
}

/// <summary>An order's fulfilment status changed and the change was stored.</summary>
public sealed class FulfillmentStatusChangedEventArgs : EventArgs
{
    public required Guid OrderUniqueId { get; init; }

    /// <summary>The order as it was stored with the new fulfilment status. Null when the stored order could not be read.</summary>
    public IOrderInfo? OrderInfo { get; init; }

    /// <summary>The fulfilment status before the change. Null when the order's history did not say.</summary>
    public Models.Orders.FulfillmentStatus? PreviousStatus { get; init; }

    /// <summary>The fulfilment status the order has now.</summary>
    public required Models.Orders.FulfillmentStatus Status { get; init; }
}

public sealed class OrderUpdatedEventArgs : EventArgs
{
    public required IOrderInfo OrderInfo { get; set; }
}

public sealed class CustomerEmailAddedEventArgs : EventArgs
{
    public required IOrderInfo OrderInfo { get; set; }
    public string? PreviousCustomerEmail { get; set; }
    public required string NewCustomerEmail { get; set; }
}

public sealed class CustomerInformationUpdatingEventArgs : EventArgs
{
    public required IOrderInfo OrderInfo { get; set; }
    public required IReadOnlyDictionary<string, string> Form { get; set; }
}

public sealed class CustomerInformationUpdatedEventArgs : EventArgs
{
    public required IOrderInfo OrderInfo { get; set; }
    public required IReadOnlyDictionary<string, string> Form { get; set; }
}

public sealed class OrderUpdatingEventArgs : EventArgs
{
    public required IOrderInfo OrderInfo { get; set; }
}

public sealed class AddingOrderlineEventArgs : EventArgs
{
    public required OrderSettings Settings { get; set; }
    public required IProduct Product { get; set; }
    public IVariant? Variant { get; set; }
    public decimal Quantity { get; set; }
    public OrderAction Action { get; set; }
    public required IOrderInfo OrderInfo { get; set; }
}

public sealed class AddedOrderlineEventArgs : EventArgs
{
    public required OrderInfo OrderInfo { get; set; }
    public required OrderLine OrderLine { get; set; }
    public required OrderSettings Settings { get; set; }
}

public sealed class RemovedOrderlineEventArgs : EventArgs
{
    public required OrderInfo OrderInfo { get; set; }
    public required OrderLine OrderLine { get; set; }
}

public sealed class UpdatedOrderlineEventArgs : EventArgs
{
    public required OrderInfo OrderInfo { get; set; }
}

public sealed class ShippingProviderAddedEventArgs : EventArgs
{
    public required OrderInfo OrderInfo { get; set; }
}

public sealed class PaymentProviderAddedEventArgs : EventArgs
{
    public required OrderInfo OrderInfo { get; set; }
}
