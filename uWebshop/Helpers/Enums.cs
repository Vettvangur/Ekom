namespace uWebshop.Helpers
{
    public enum OrderStatus
    {
        Cancelled,
        Closed,
        PaymentFailed,
        Incomplete,
        Confirmed,
        OfflinePayment,
        Pending,
        ReadyForDispatch,
        ReadyForDispatchWhenStockArrives,
        Dispatched,
        Undeliverable,
        WaitingForPayment,
        WaitingForPaymentProvider,
        Returned,
        Wishlist,
        Scheduled,
    }

    public enum OrderAction
    {
        Add,
        Update
    }
}
