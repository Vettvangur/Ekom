using System;

namespace Ekom.Models
{
    public class OrderUpdatedEventArgs : EventArgs
    {
        public IOrderInfo OrderInfo { get; set; }
    }
    public class OrderUpdatingEventArgs : EventArgs
    {
        public IOrderInfo OrderInfo { get; set; }
    }
}
