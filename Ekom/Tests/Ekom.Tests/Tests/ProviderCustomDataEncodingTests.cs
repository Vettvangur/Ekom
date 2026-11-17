using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using Newtonsoft.Json.Linq;
using System.Net;
using Xunit;

namespace Ekom.Tests.Tests;

public class ProviderCustomDataEncodingTests
{
    private const string Raw = "Leave at A & B <side door>";

    [Theory]
    [InlineData("customshippingInstructions")]
    [InlineData("CustomPaymentReference")]
    public void FromForm_LeavesProviderCustomDataUnencoded(string key)
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            [key] = Raw,
            ["customerName"] = Raw,
        });

        var result = OrderCustomData.FromForm(form);

        Assert.Equal(Raw, result[key]);
        Assert.NotEqual(Raw, result["customerName"]);
    }

    [Theory]
    [InlineData("customshippingInstructions")]
    [InlineData("CustomPaymentReference")]
    public void FromJson_LeavesProviderCustomDataUnencoded(string key)
    {
        var json = new JObject
        {
            [key] = Raw,
            ["customerName"] = Raw,
        };

        var result = OrderCustomData.FromJson(json);

        Assert.Equal(Raw, result[key]);
        Assert.NotEqual(Raw, result["customerName"]);
    }

    [Fact]
    public void ShippingProviderCustomData_FromRequest_IsEncodedOnce()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["customshippingInstructions"] = Raw,
        });
        var provider = new Mock<IShippingProvider>();
        provider.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>());

        var ordered = new OrderedShippingProvider(provider.Object, null!, OrderCustomData.FromForm(form), null);

        var stored = ordered.CustomData["customshippingInstructions"];
        Assert.NotEqual(Raw, stored);
        Assert.Equal(Raw, WebUtility.HtmlDecode(stored));
    }

    [Fact]
    public void PaymentProviderCustomData_FromRequest_IsEncodedOnce()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["custompaymentReference"] = Raw,
        });
        var provider = new Mock<IPaymentProvider>();
        provider.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>());

        var ordered = new OrderedPaymentProvider(provider.Object, null!, OrderCustomData.FromForm(form));

        var stored = ordered.CustomData["custompaymentReference"];
        Assert.NotEqual(Raw, stored);
        Assert.Equal(Raw, WebUtility.HtmlDecode(stored));
    }
}
