using Ekom.Models;
using Ekom.Models.Manager;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class OrderListCurrencyTests
{
    [Fact]
    public void MixedCurrenciesDoNotDisplayAMisleadingMonetaryTotal()
    {
        var result = new OrderListData(new[] { new OrderData() }, new OrderListDataTotals
        {
            Count = 2, TotalAmount = 500, AverageAmount = 250, CurrencyCount = 2, Currency = "ISK",
        }, "USD");
        Assert.Equal(2, result.Count);
        Assert.Equal("Mixed currencies", result.GrandTotal);
        Assert.Equal("Mixed currencies", result.AverageAmount);
    }

    [Fact]
    public void TotalsUseTheOrdersCurrencyRatherThanTheStoresDefault()
    {
        var totals = new OrderListDataTotals { Count = 1, TotalAmount = 100, AverageAmount = 100, CurrencyCount = 1, Currency = "ISK" };
        var actual = new OrderListData(new[] { new OrderData() }, totals, "USD");
        var expected = new OrderListData(new[] { new OrderData() }, totals, "ISK");
        Assert.Equal(expected.GrandTotal, actual.GrandTotal);
        Assert.DoesNotContain("$", actual.GrandTotal);
    }
}
