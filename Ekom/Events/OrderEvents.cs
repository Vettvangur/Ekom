using Ekom.API;
using Ekom.Models;
using Ekom.Utilities;

namespace Ekom.Events;

public static class OrderEvents
{
    /// <summary>
    /// Event to fire on <see cref="IOrderInfo"/> updates
    /// </summary>
    public static event EventHandler<OrderUpdatedEventArgs> OrderUpdated;
    internal static void OnOrderUpdated(object sender, OrderUpdatedEventArgs args)
        => OrderUpdated?.Invoke(sender, args);

    public static event EventHandler<OrderUpdatingEventArgs> OrderUpdateing;
    internal static void OnOrderUpdateing(object sender, OrderUpdatingEventArgs args)
        => OrderUpdateing?.Invoke(sender, args);

    public static event EventHandler<OrderStatusEventArgs> OrderStatusChanging;
    internal static void OnOrderStatusChanging(object sender, OrderStatusEventArgs args)
        => OrderStatusChanging?.Invoke(sender, args);
    public static event EventHandler<OrderStatusEventArgs> OrderStatusChanged;
    internal static void OnOrderStatusChanged(object sender, OrderStatusEventArgs args)
        => OrderStatusChanged?.Invoke(sender, args);
    /// </summary>
    public static event EventHandler<AddingOrderlineEventArgs> AddingOrderline;
    internal static void OnAddingOrderline(object sender, AddingOrderlineEventArgs args)
        => AddingOrderline?.Invoke(sender, args);
}

/// <summary>
/// For changing and changed <see cref="OrderStatus"/> events
/// </summary>
public class OrderStatusEventArgs : EventArgs
{
    public Guid OrderUniqueId { get; set; }

    public OrderStatus PreviousStatus { get; set; }

    public OrderStatus Status { get; set; }
    public bool ClearCustomerOrderReference { get; set; } = true;
}

public class OrderUpdatedEventArgs : EventArgs
{
    public IOrderInfo OrderInfo { get; set; }
}
public class OrderUpdatingEventArgs : EventArgs
{
    public IOrderInfo OrderInfo { get; set; }
}
public class AddingOrderlineEventArgs : EventArgs
{
    public OrderSettings Settings { get; set; }
    public IProduct Product { get; set; }
    public IVariant Variant { get; set; }
    public decimal Quantity { get; set; }
    public OrderAction Action { get; set; }
    public IOrderInfo OrderInfo { get; set; }
}
