using Ekom.Interfaces;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models.Events
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
