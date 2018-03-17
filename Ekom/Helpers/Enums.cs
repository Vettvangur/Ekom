namespace Ekom.Helpers
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

    /// <summary>
    /// Default is AddOrUpdate, we also allow to set quantity to fixed amount
    /// </summary>
    public enum OrderAction
    {
        /// <summary>
        /// Add or update order line with given product and quantity
        /// </summary>
        AddOrUpdate,
        /// <summary>
        /// Set quantity to provided amount
        /// </summary>
        Set
    }
}
