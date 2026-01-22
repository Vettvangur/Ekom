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
    // Order updating
    // ----------------------------

    public static event EventHandler<OrderUpdatingEventArgs>? OrderUpdating;

    internal static void OnOrderUpdating(object sender, OrderUpdatingEventArgs args)
        => OrderUpdating?.Invoke(sender, args);

    public static event Func<object, OrderUpdatingEventArgs, CancellationToken, Task>? OrderUpdatingAsync;

    public static Task OnOrderUpdatingAsync(object sender, OrderUpdatingEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(OrderUpdatingAsync, sender, args, ct);

    // ----------------------------
    // Order status changing/changed
    // ----------------------------

    public static event EventHandler<OrderStatusEventArgs>? OrderStatusChanging;

    internal static void OnOrderStatusChanging(object sender, OrderStatusEventArgs args)
        => OrderStatusChanging?.Invoke(sender, args);

    public static event Func<object, OrderStatusEventArgs, CancellationToken, Task>? OrderStatusChangingAsync;

    public static Task OnOrderStatusChangingAsync(object sender, OrderStatusEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(OrderStatusChangingAsync, sender, args, ct);

    public static event EventHandler<OrderStatusEventArgs>? OrderStatusChanged;

    internal static void OnOrderStatusChanged(object sender, OrderStatusEventArgs args)
        => OrderStatusChanged?.Invoke(sender, args);

    public static event Func<object, OrderStatusEventArgs, CancellationToken, Task>? OrderStatusChangedAsync;

    public static Task OnOrderStatusChangedAsync(object sender, OrderStatusEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(OrderStatusChangedAsync, sender, args, ct);

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

    public static event EventHandler<UpdatedOrderlineEventArgs>? UpdatedOrderline;

    internal static void OnUpdatedOrderline(object sender, UpdatedOrderlineEventArgs args)
        => UpdatedOrderline?.Invoke(sender, args);

    public static event Func<object, UpdatedOrderlineEventArgs, CancellationToken, Task>? UpdatedOrderlineAsync;

    public static Task OnUpdatedOrderlineAsync(object sender, UpdatedOrderlineEventArgs args, CancellationToken ct = default)
        => AsyncEventInvoker.InvokeAsync(UpdatedOrderlineAsync, sender, args, ct);
}

/// <summary>For changing and changed <see cref="OrderStatus"/> events</summary>
public sealed class OrderStatusEventArgs : EventArgs
{
    public Guid OrderUniqueId { get; init; }
    public OrderStatus PreviousStatus { get; init; }
    public OrderStatus Status { get; set; } // keep settable if handlers can change the status
    public bool ClearCustomerOrderReference { get; set; } = true;
}

public sealed class OrderUpdatedEventArgs : EventArgs
{
    public required IOrderInfo OrderInfo { get; init; }
}

public sealed class OrderUpdatingEventArgs : EventArgs
{
    public required IOrderInfo OrderInfo { get; init; }
}

public sealed class AddingOrderlineEventArgs : EventArgs
{
    public required OrderSettings Settings { get; init; }
    public required IProduct Product { get; init; }
    public IVariant? Variant { get; init; }
    public decimal Quantity { get; init; }
    public OrderAction Action { get; init; }
    public required IOrderInfo OrderInfo { get; init; }
}

public sealed class AddedOrderlineEventArgs : EventArgs
{
    public required OrderInfo OrderInfo { get; init; }
}

public sealed class UpdatedOrderlineEventArgs : EventArgs
{
    public required OrderInfo OrderInfo { get; init; }
}
