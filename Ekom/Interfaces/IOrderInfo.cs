using System;
using Ekom.Models;

namespace Ekom.Interfaces
{
    public interface IOrderInfo
    {

        /// <summary>
        /// Gets the uniqueId.
        /// </summary>
        /// <value>
        /// The uniqueId.
        /// </value>
        Guid UniqueId { get; }

        /// <summary>
        /// Gets the quantity.
        /// </summary>
        /// <value>
        /// The quantity.
        /// </value>
        int Quantity { get; }

        /// <summary>
        /// Gets the store info.
        /// </summary>
        /// <value>
        /// The store info.
        /// </value>
        StoreInfo StoreInfo { get; }
    }
}
