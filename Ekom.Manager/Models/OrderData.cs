using Ekom.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ekom.Manager.Models
{
    public class OrderListData
    {

        public OrderListData(IEnumerable<OrderData> orders)
        {
            Orders = orders;

            this.Count = Orders.Count();

            if (orders != null && orders.Any())
            {
                var _grandTotal = Orders.Sum(z => z.TotalAmount);
                var _averageAmount = Orders.Average(a => a.TotalAmount);
                this.GrandTotal = string.Format(Configuration.IsCultureInfo, "{0:C}", _grandTotal) + "";
                this.AverageAmount = string.Format(Configuration.IsCultureInfo, "{0:C}", _averageAmount) + "";
            }

        }

        public IEnumerable<OrderData> Orders { get; set; }
        public string GrandTotal { get; set; }
        public string AverageAmount { get; set; }
        public int Count { get; set; }
    }
}
