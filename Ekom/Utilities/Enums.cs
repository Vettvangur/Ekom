using System;

namespace Ekom.Utilities
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
        /// <summary>
        /// Used to lock order for modifications.
        /// If user requests the order while it is in this state he receives a new one with the old data.
        /// </summary>
        WaitingForPayment,
        Returned,
        Wishlist,
    }

    /// <summary>
    /// Default is AddOrUpdate, we also allow to set quantity to fixed amount
    /// </summary>
    public enum OrderAction
    {
        /// <summary>
        /// Add or update order line with given product and quantity.
        /// Negative values indicate a reduction in quantity.
        /// F.x. quantity +2/-2
        /// </summary>
        AddOrUpdate,
        /// <summary>
        /// Set quantity to provided amount
        /// </summary>
        Set
    }

    /// <summary>
    /// Rounding action to perform after Vat calculation
    /// </summary>
    public enum Rounding
    {
        /// <summary>
        /// Leave decimal outcome as-is
        /// </summary>
        None,

        /// <summary>
        /// <see cref="Math.Floor(decimal)"/>
        /// </summary>
        RoundDown,

        /// <summary>
        /// <see cref="Math.Ceiling(decimal)"/>
        /// </summary>
        RoundUp,

        /// <summary>
        /// <see cref="MidpointRounding.ToEven"/>
        /// </summary>
        RoundToEven,

        /// <summary>
        /// <see cref="MidpointRounding.AwayFromZero"/>
        /// </summary>
        AwayFromZero
    }
}
