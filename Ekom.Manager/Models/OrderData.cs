using System;
using System.Collections.Generic;
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

            this.GrandTotal = _grandTotal.ToString();
            if (_grandTotal <= 0) {
                this.AverageAmount = "0";
            } else
            {
                this.AverageAmount = (this.Count / _grandTotal).ToString();
            }
        }

        public IEnumerable<OrderData> Orders { get; set; }
        public string GrandTotal { get; set; }
        public string AverageAmount { get; set; }
        public int Count { get; set; }
    }
}
