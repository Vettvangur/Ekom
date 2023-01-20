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
        ReadyForPickup = 12,
        /// <summary>
        /// 
        /// </summary>
        Dispatched = 8,
        /// <summary>
        /// Used to lock order for modifications.
        /// If user requests the order while it is in this state he receives a new one with the old data.
        /// </summary>
        WaitingForPayment,
        /// <summary>
        /// 
        /// </summary>
        Returned,
        /// <summary>
        /// 
        /// </summary>
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
        Set,
        /// <summary>
        /// Always new orderline even with the same SKU
        /// </summary>
        New
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

    /// <summary>
    /// Form controller action to render
    /// </summary>
    public enum FormType
    {
        /// <summary>
        /// AddOrderLine action with product page class
        /// </summary>
        AddToOrderProduct,
        /// <summary>
        /// AddOrderLine action with cart view styles
        /// </summary>
        AddToOrderCart,
        /// <summary>
        /// 
        /// </summary>
        RemoveOrderLine,
        /// <summary>
        /// 
        /// </summary>
        UpdatePaymentProvider,
        /// <summary>
        /// 
        /// </summary>
        UpdateShippingProvider,
        /// <summary>
        /// 
        /// </summary>
        UpdateCustomerInformation,
        /// <summary>
        /// 
        /// </summary>
        ApplyCouponToOrder,
        /// <summary>
        /// 
        /// </summary>
        ChangeCurrency
    }

    /// <summary>
    /// Ekom Property Editor Type
    /// </summary>
    public enum PropertyEditorType
    {
        Empty,
        Store,
        Language
    }
}
