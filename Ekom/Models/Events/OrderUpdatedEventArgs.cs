using Ekom.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models.Events
{
    public class OrderUpdatedEventArgs : EventArgs
    {
        public IOrderInfo OrderInfo { get; set; }
    }
}
