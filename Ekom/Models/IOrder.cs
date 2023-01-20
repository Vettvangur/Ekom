using System;

namespace Ekom.Models
{
    /// <summary>
    /// Order/Cart
    /// </summary>
    public interface IOrder
    {
        /// <summary>
        /// Gets the uniqueId.
        /// </summary>
        /// <value>
        /// The uniqueId.
        /// </value>
        Guid UniqueId { get; }

        /// <summary>
        /// Gets the referenceId.
        /// </summary>
        /// <value>
        /// The referenceId.
        /// </value>
        int ReferenceId { get; }

        /// <summary>
        /// Gets the orderinfo.
        /// </summary>
        /// <value>
        /// The orderinfo.
        /// </value>
        string OrderInfo { get; }

        /// <summary>
        /// Gets the ordernumber.
        /// </summary>
        /// <value>
        /// The ordernumber.
        /// </value>
        string OrderNumber { get; }

        /// <summary>
        /// Gets the orderstatus.
        /// </summary>
        /// <value>
        /// The orderstatus.
        /// </value>
        string OrderStatus { get; }

        /// <summary>
        /// Gets the store alias.
        /// </summary>
        /// <value>
        /// The store alias.
        /// </value>
        string StoreAlias { get; }

        /// <summary>
        /// Gets the create date.
        /// </summary>
        /// <value>
        /// The create date.
        /// </value>
        DateTime CreateDate { get; }

        /// <summary>
        /// Gets the update date.
        /// </summary>
        /// <value>
        /// The update date.
        /// </value>
        DateTime UpdateDate { get; }

        /// <summary>
        /// Gets the paid date.
        /// </summary>
        /// <value>
        /// The paid date.
        /// </value>
        DateTime PaidDate { get; }

    }
}
