namespace Ekom.Models.Manager
{
    public class OrderListData
    {

        public OrderListData(IEnumerable<OrderData> orders, OrderListDataTotals totals)
        {
            Orders = orders;

            this.Count = totals.Count;

            if (orders != null && orders.Any())
            {
                var _grandTotal = totals.TotalAmount;
                var _averageAmount = totals.AverageAmount;
                this.GrandTotal = string.Format(Configuration.IsCultureInfo, "{0:C}", _grandTotal) + "";
                this.AverageAmount = string.Format(Configuration.IsCultureInfo, "{0:C}", _averageAmount) + "";
            }

        }

        public IEnumerable<OrderData> Orders { get; set; }
        public string GrandTotal { get; set; }
        public string AverageAmount { get; set; }
        public int Count { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 30;
        public int TotalPages {
            get
            {
                return (Count + PageSize - 1) / PageSize;
            }
        }
    }

    public class OrderListDataTotals
    {
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
        public int Count { get; set; }
    }
}
