using Ekom.Utilities;
using System.Globalization;

namespace Ekom.Models.Manager
{
    public class OrderListData
    {

        public OrderListData(IEnumerable<OrderData> orders, OrderListDataTotals totals, string? currency = null)
        {
            Orders = orders;

            this.Count = totals.Count;

            if (totals.CurrencyCount > 1)
            {
                GrandTotal = "Mixed currencies";
                AverageAmount = "Mixed currencies";
                return;
            }

            currency = string.IsNullOrWhiteSpace(totals.Currency) ? currency : totals.Currency;

            if (orders != null && orders.Any())
            {
                decimal _grandTotal = totals.TotalAmount;
                decimal _averageAmount = totals.AverageAmount;
                CultureInfo cultureInfo = CultureHelper.GetCultureInfo(currency) ?? Configuration.IsCultureInfo;

                this.GrandTotal = string.Format(cultureInfo, "{0:C}", _grandTotal) + "";
                this.AverageAmount = string.Format(cultureInfo, "{0:C}", _averageAmount) + "";
            }

        }

        public IEnumerable<OrderData> Orders { get; set; }
        public string GrandTotal { get; set; } = string.Empty;
        public string AverageAmount { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 30;
        public int TotalPages
        {
            get
            {
                return (Count + PageSize - 1) / PageSize;
            }
        }
    }

    public class OrderListDataTotals
    {
        public int CurrencyCount { get; set; }
        public string? Currency { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Number of orders matching one manager order view.
    /// </summary>
    public class OrderViewCount
    {
        public string View { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
