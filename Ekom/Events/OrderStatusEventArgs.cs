using Ekom.Utilities;
using System;

namespace Ekom.Models
{
    /// <summary>
    /// For changing and changed <see cref="OrderStatus"/> events
    /// </summary>
    public class OrderStatusEventArgs : EventArgs
    {
        public Guid OrderUniqueId { get; set; }

        public OrderStatus PreviousStatus { get; set; }

        public OrderStatus Status { get; set; }
    }
}
