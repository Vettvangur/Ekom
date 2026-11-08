using Ekom.Controllers;
using Ekom.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ekom.Models.Manager;
using System.Globalization;
using Xunit;

namespace Ekom.Tests.Tests;

public class ManagerOrderCurrencyTests
{
    [Fact]
    public void OrderResponse_UsesSavedStoreCultureDuringSerializationAndRestoresRequestCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("da-DK");
            var order = CreateOrder();
            order.ResolveUrl = () =>
            {
                Assert.Equal("is-IS", CultureInfo.CurrentCulture.Name);
                Assert.Equal("is-IS", CultureInfo.CurrentUICulture.Name);
                return "/is/product/";
            };

            var response = Assert.IsType<JObject>(EkomManagerController.GetOrderInfoResponse(order));

            Assert.Equal("/is/product/", response["productUrl"]?.Value<string>());
            Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
            Assert.Equal("da-DK", CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUICulture;
        }
    }

    [Fact]
    public void OrderResponse_RestoresCultureWhenSerializationFails()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUICulture = CultureInfo.CurrentUICulture;
        var order = CreateOrder();
        order.ResolveUrl = () => throw new InvalidOperationException("Serialization failed");

        Assert.Throws<JsonSerializationException>(() => EkomManagerController.GetOrderInfoResponse(order));

        Assert.Same(previousCulture, CultureInfo.CurrentCulture);
        Assert.Same(previousUICulture, CultureInfo.CurrentUICulture);
    }

    private static CultureProbeOrder CreateOrder()
    {
        var currency = new CurrencyModel { CurrencyValue = "is-IS" };
        var store = new StoreInfo(Guid.NewGuid(), currency, [currency], "is-IS", "Store", true, 0, true);
        return new CultureProbeOrder(new OrderData
        {
            OrderInfo = new JObject
            {
                ["Culture"] = "en-US",
                ["StoreInfo"] = JToken.FromObject(store),
                ["OrderLines"] = new JArray(),
            }.ToString(),
        });
    }

    [JsonObject(MemberSerialization.OptIn)]
    private sealed class CultureProbeOrder(OrderData data) : OrderInfo(data)
    {
        public Func<string> ResolveUrl { get; set; } = () => "";

        [JsonProperty]
        public string ProductUrl => ResolveUrl();
    }

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
