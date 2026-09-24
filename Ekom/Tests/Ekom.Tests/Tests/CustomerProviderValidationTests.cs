using Ekom.Models;
using Ekom.Services;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ekom.Tests.Tests;

public class CustomerProviderValidationTests
{
    [Fact]
    public void Customer_Metadata_Update_Does_Not_Revalidate_Selected_Providers()
    {
        var order = CreateOrder();
        var form = new Dictionary<string, string>
        {
            ["storeAlias"] = "Store",
            ["customerDKInvoiceNumber"] = "12345"
        };

        Assert.False(OrderService.HasProviderRelevantCustomerChanges(form, order));
    }

    [Theory]
    [InlineData("customerCountry", "DK")]
    [InlineData("shippingCountry", "DK")]
    public void Changing_Country_Revalidates_Providers(string field, string country)
    {
        var order = CreateOrder();

        Assert.True(OrderService.HasProviderRelevantCustomerChanges(new Dictionary<string, string> { [field] = country }, order));
    }

    [Fact]
    public void Unchanged_Country_Does_Not_Revalidate_Providers()
    {
        var order = CreateOrder();

        Assert.False(OrderService.HasProviderRelevantCustomerChanges(
            new Dictionary<string, string> { ["shippingCountry"] = "IS", ["customerCountry"] = "IS" }, order));
    }

    [Theory]
    [InlineData("ShippingProvider")]
    [InlineData("PaymentProvider")]
    public void Selecting_A_Different_Provider_Revalidates_Providers(string field)
    {
        var order = CreateOrder();

        Assert.True(OrderService.HasProviderRelevantCustomerChanges(
            new Dictionary<string, string> { [field] = Guid.NewGuid().ToString() }, order));
    }

    [Fact]
    public void Unchanged_Provider_Does_Not_Revalidate_Providers()
    {
        var order = CreateOrder();

        Assert.False(OrderService.HasProviderRelevantCustomerChanges(
            new Dictionary<string, string> { ["ShippingProvider"] = order.ShippingProvider.Key.ToString() }, order));
    }

    private static IOrderInfo CreateOrder()
    {
        var storeInfo = new StoreInfo(Guid.NewGuid(), new CurrencyModel { CurrencyValue = "is-IS" }, [], "is-IS", "Store", true, 0, true);
        var shipping = new OrderedShippingProvider(new JObject
        {
            ["Id"] = 1,
            ["Key"] = Guid.NewGuid(),
            ["Title"] = "Pickup"
        }, storeInfo);
        var payment = new OrderedPaymentProvider(new JObject
        {
            ["Id"] = 2,
            ["Key"] = Guid.NewGuid(),
            ["Title"] = "Invoice"
        }, storeInfo);
        var customerInfo = new CustomerInfo();
        customerInfo.Customer.Properties["customerCountry"] = "IS";
        customerInfo.Shipping.Properties["shippingCountry"] = "IS";

        var order = new Mock<IOrderInfo>();
        order.SetupGet(x => x.CustomerInformation).Returns(customerInfo);
        order.SetupGet(x => x.ShippingProvider).Returns(shipping);
        order.SetupGet(x => x.PaymentProvider).Returns(payment);
        return order.Object;
    }
}
