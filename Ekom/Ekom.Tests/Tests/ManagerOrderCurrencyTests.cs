using Ekom.Models;
using Ekom.Models.Manager;
using System.Globalization;
using Xunit;

namespace Ekom.Tests.Tests;

public class ManagerOrderCurrencyTests
{
    [Fact]
    public void FormattedTotal_UsesOrderCurrency()
    {
        var order = new OrderData
        {
            Currency = "EUR",
            TotalAmount = 1234m
        };

        string formattedTotal = order.FormattedTotal;

        Assert.Contains("€", formattedTotal, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderListData_UsesProvidedCurrencyForTotals()
    {
        var orders = new[]
        {
            new OrderData()
        };
        var totals = new OrderListDataTotals
        {
            Count = 1,
            TotalAmount = 1234m,
            AverageAmount = 1234m
        };

        var orderListData = new OrderListData(orders, totals, "de-DE");
        string expectedTotal = string.Format(new CultureInfo("de-DE"), "{0:C}", 1234m);

        Assert.Equal(expectedTotal, orderListData.GrandTotal);
        Assert.Equal(expectedTotal, orderListData.AverageAmount);
    }
}
