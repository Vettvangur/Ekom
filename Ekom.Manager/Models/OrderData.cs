using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ekom.Models.Data;

namespace Ekom.Manager.Models
{
    public class OrderListData
    {

        public OrderListData(IEnumerable<OrderData> orders)
        {
            Orders = orders;

            this.Count = Orders.Count();

            var _grandTotal = Orders.Sum(z => z.TotalAmount);
            var _averageAmount = Orders.Average(a => a.TotalAmount);
            this.GrandTotal = string.Format(new CultureInfo("is-IS"), "{0:C}", _grandTotal) + "";
            this.AverageAmount = string.Format(new CultureInfo("is-IS"), "{0:C}", _averageAmount) + "";
        }
        
        public IEnumerable<OrderData> Orders { get; set; }
        public string GrandTotal { get; set; }
        public string AverageAmount { get; set; }
        public int Count { get; set; }
    }
}
