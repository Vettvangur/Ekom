namespace uWebshop.Helpers
{
    /// <summary>
    /// 
    /// </summary>
    public enum OrderStatus
    {
        /// <summary>
        /// 
        /// </summary>
        Cancelled,
        /// <summary>
        /// 
        /// </summary>
        Closed,
        /// <summary>
        /// 
        /// </summary>
        PaymentFailed,
        /// <summary>
        /// 
        /// </summary>
        Incomplete,
        /// <summary>
        /// 
        /// </summary>
        Confirmed,
        /// <summary>
        /// 
        /// </summary>
        OfflinePayment,
        /// <summary>
        /// 
        /// </summary>
        Pending,
        /// <summary>
        /// 
        /// </summary>
        ReadyForDispatch,
        /// <summary>
        /// 
        /// </summary>
        ReadyForDispatchWhenStockArrives,
        /// <summary>
        /// 
        /// </summary>
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
