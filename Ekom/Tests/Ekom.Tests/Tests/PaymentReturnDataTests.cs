using Ekom.Controllers;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class PaymentReturnDataTests
{
    [Fact]
    public void Preserves_Case_Distinct_Order_Id_Callback_Values()
    {
        const string ekomOrderId = "8e32fc81-ac6b-4444-a2cd-5fa7468aa4e8";
        const string providerOrderId = "54173eb6-d255-4d48-b411-01f0927f0cfa";

        var callbackData = EkomCheckoutApiController.ParsePaymentReturnData(
            $"?reference={providerOrderId}&orderId={ekomOrderId}&outcome=cancel&orderid={providerOrderId}");

        Assert.Equal(ekomOrderId, callbackData["orderId"]);
        Assert.Equal(providerOrderId, callbackData["orderid"]);
        Assert.Equal("cancel", callbackData["outcome"]);
        Assert.True(Guid.TryParse(callbackData["orderId"], out var orderId));
        Assert.Equal(Guid.Parse(ekomOrderId), orderId);

        var redirectUrl = QueryHelpers.AddQueryString("/cancel", callbackData);

        Assert.Contains($"orderId={ekomOrderId}", redirectUrl, StringComparison.Ordinal);
        Assert.Contains($"orderid={providerOrderId}", redirectUrl, StringComparison.Ordinal);
    }
}
