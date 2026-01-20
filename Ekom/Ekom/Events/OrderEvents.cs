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

    public static event Func<object, OrderUpdatedEventArgs, Task>? OrderUpdatedAsync;
    public static async Task OnOrderUpdatedAsync(object sender, OrderUpdatedEventArgs args)
    {
        if (OrderUpdatedAsync is null) return;

        foreach (var handler in OrderUpdatedAsync.GetInvocationList()
                 .Cast<Func<object, OrderUpdatedEventArgs, Task>>())
        {
            await handler(sender, args);
        }
    }

    public static event EventHandler<OrderUpdatingEventArgs> OrderUpdateing;
    internal static void OnOrderUpdateing(object sender, OrderUpdatingEventArgs args)
        => OrderUpdateing?.Invoke(sender, args);

    public static event Func<object, OrderUpdatingEventArgs, Task>? OrderUpdateingAsync;
    public static async Task OnOrderUpdateingAsync(object sender, OrderUpdatingEventArgs args)
    {
        if (OrderUpdateingAsync is null) return;

        foreach (var handler in OrderUpdateingAsync.GetInvocationList()
                 .Cast<Func<object, OrderUpdatingEventArgs, Task>>())
        {
            await handler(sender, args);
        }
    }

    public static event EventHandler<OrderStatusEventArgs> OrderStatusChanging;
    internal static void OnOrderStatusChanging(object sender, OrderStatusEventArgs args)
        => OrderStatusChanging?.Invoke(sender, args);

    public static event Func<object, OrderStatusEventArgs, Task>? OrderStatusChangingAsync;
    public static async Task OnOrderStatusChangingAsync(object sender, OrderStatusEventArgs args)
    {
        if (OrderStatusChangingAsync is null) return;

        foreach (var handler in OrderStatusChangingAsync.GetInvocationList()
                 .Cast<Func<object, OrderStatusEventArgs, Task>>())
        {
            await handler(sender, args);
        }
    }

    public static event EventHandler<OrderStatusEventArgs> OrderStatusChanged;
    internal static void OnOrderStatusChanged(object sender, OrderStatusEventArgs args)
        => OrderStatusChanged?.Invoke(sender, args);

    public static event Func<object, OrderStatusEventArgs, Task>? OrderStatusChangedAsync;
    public static async Task OnOrderStatusChangedAsync(object sender, OrderStatusEventArgs args)
    {
        if (OrderStatusChangedAsync is null) return;

        foreach (var handler in OrderStatusChangedAsync.GetInvocationList()
                 .Cast<Func<object, OrderStatusEventArgs, Task>>())
        {
            await handler(sender, args);
        }
    }
    public static event EventHandler<AddingOrderlineEventArgs> AddingOrderline;
    internal static void OnAddingOrderline(object sender, AddingOrderlineEventArgs args)
        => AddingOrderline?.Invoke(sender, args);
    public static event Func<object, AddingOrderlineEventArgs, Task>? AddingOrderlineAsync;
    public static async Task OnAddingOrderlineAsync(object sender, AddingOrderlineEventArgs args)
    {
        if (AddingOrderlineAsync is null) return;

        foreach (var handler in AddingOrderlineAsync.GetInvocationList()
                 .Cast<Func<object, AddingOrderlineEventArgs, Task>>())
        {
            await handler(sender, args);
        }
    }

    public static event EventHandler<AddedOrderlineEventArgs> AddedOrderline;
    internal static void OnAddedOrderline(object sender, AddedOrderlineEventArgs args)
        => AddedOrderline?.Invoke(sender, args);
    public static event Func<object, AddedOrderlineEventArgs, Task>? AddedOrderlineAsync;
    public static async Task OnAddedOrderlineAsync(object sender, AddedOrderlineEventArgs args)
    {
        if (AddedOrderlineAsync is null) return;

        foreach (var handler in AddedOrderlineAsync.GetInvocationList()
                 .Cast<Func<object, AddedOrderlineEventArgs, Task>>())
        {
            await handler(sender, args);
        }
    }

    public static event EventHandler<UpdatedOrderlineEventArgs> UpdatedOrderline;
    internal static void OnUpdatedOrderline(object sender, UpdatedOrderlineEventArgs args)
        => UpdatedOrderline?.Invoke(sender, args);
    public static event Func<object, UpdatedOrderlineEventArgs, Task>? UpdatedOrderlineAsync;
    public static async Task OnUpdatedOrderlineAsync(object sender, UpdatedOrderlineEventArgs args)
    {
        if (UpdatedOrderlineAsync is null) return;

        foreach (var handler in UpdatedOrderlineAsync.GetInvocationList()
                 .Cast<Func<object, UpdatedOrderlineEventArgs, Task>>())
        {
            await handler(sender, args);
        }
    }
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
    public IVariant? Variant { get; set; }
    public decimal Quantity { get; set; }
    public OrderAction Action { get; set; }
    public IOrderInfo OrderInfo { get; set; }
}

public class AddedOrderlineEventArgs : EventArgs
{
    public OrderInfo OrderInfo { get; set; }
}

public class UpdatedOrderlineEventArgs : EventArgs
{
    public OrderInfo OrderInfo { get; set; }
}

