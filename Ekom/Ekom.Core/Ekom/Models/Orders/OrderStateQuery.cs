namespace Ekom.Models.Orders;

internal static class OrderStateQuery
{
    // Only enum-whitelisted values become SQL literals. Caller input is never interpolated directly.
    public static string? Predicate(string? filter)
    {
        if (string.IsNullOrEmpty(filter) || filter == "AllOrders") filter = "states:;;";
        if (!filter.StartsWith("states:", StringComparison.Ordinal)) return null;
        var parts = filter[7..].Split(';');
        if (parts.Length != 3) return "1 = 0";
        // Drafts are opt-in, including when only payment or fulfillment is filtered.
        if (string.IsNullOrEmpty(parts[0])) parts[0] = "Open,Closed,Cancelled";
        var legacy = "CASE OrderStatusCol WHEN '0' THEN 'Cancelled' WHEN '1' THEN 'Closed' WHEN '2' THEN 'PaymentFailed' WHEN '3' THEN 'Incomplete' WHEN '4' THEN 'OfflinePayment' WHEN '5' THEN 'Pending' WHEN '6' THEN 'ReadyForDispatch' WHEN '7' THEN 'ReadyForDispatchWhenStockArrives' WHEN '8' THEN 'Dispatched' WHEN '9' THEN 'WaitingForPayment' WHEN '10' THEN 'Returned' WHEN '11' THEN 'Wishlist' WHEN '12' THEN 'ReadyForPickup' ELSE OrderStatusCol END";
        var order = $"COALESCE(OrderStateCol, CASE WHEN {legacy} IN ('Incomplete','WaitingForPayment','PaymentFailed','Wishlist') THEN 'Draft' WHEN {legacy} IN ('Closed','Returned') THEN 'Closed' WHEN {legacy} = 'Cancelled' THEN 'Cancelled' WHEN {legacy} IN ('Pending','OfflinePayment','ReadyForDispatch','ReadyForDispatchWhenStockArrives','ReadyForPickup','Dispatched') THEN 'Open' END)";
        var payment = $"COALESCE(PaymentStateCol, CASE WHEN PaidDate IS NOT NULL OR {legacy} IN ('Closed','OfflinePayment','ReadyForDispatch','ReadyForDispatchWhenStockArrives','ReadyForPickup','Dispatched') THEN 'Paid' WHEN {legacy} = 'PaymentFailed' THEN 'Failed' WHEN {legacy} IN ('Cancelled','Incomplete','WaitingForPayment','Wishlist','Pending') THEN 'Pending' END)";
        var fulfillment = $"COALESCE(FulfillmentStateCol, CASE WHEN {legacy} IN ('Closed','Dispatched','Returned') THEN 'Fulfilled' WHEN {legacy} = 'ReadyForPickup' THEN 'Packed' WHEN {legacy} IN ('Cancelled','Incomplete','WaitingForPayment','PaymentFailed','Wishlist','Pending','OfflinePayment','ReadyForDispatch','ReadyForDispatchWhenStockArrives') THEN 'Unfulfilled' END)";
        return $"({Match<OrderStatus>(parts[0], order)} AND {Match<PaymentStatus>(parts[1], payment)} AND {Match<FulfillmentStatus>(parts[2], fulfillment)})";
    }

    private static string Match<T>(string value, string expression) where T : struct, Enum
    {
        if (string.IsNullOrEmpty(value)) return "1 = 1";
        var names = Enum.GetNames<T>().ToHashSet(StringComparer.Ordinal);
        var values = value.Split(',');
        if (values.Any(x => !names.Contains(x))) return "1 = 0";
        return expression + " IN (" + string.Join(",", values.Select(x => "'" + x + "'")) + ")";
    }
}
