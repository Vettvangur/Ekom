using Ekom.Interfaces;
using System;
using System.Collections.Generic;

namespace Ekom.Models
{
    public class OrderDynamicRequest
    {
        public Guid OrderLineLink { get; set; }
        public List<IPrice> Prices { get; set; }
        public string Title { get; set; }
        public string SKU { get; set; }
        public string Type { get; set; }
        public bool CountToTotal { get; set; } = true;
    }
}
