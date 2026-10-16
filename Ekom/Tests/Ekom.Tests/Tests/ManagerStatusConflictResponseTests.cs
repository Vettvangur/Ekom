using Ekom.Controllers;
using Ekom.Models;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class ManagerStatusConflictResponseTests
{
    [Theory]
    [InlineData(CheckoutConflictReason.Busy, "checkoutBusy", "confirm that no payment is processing")]
    [InlineData(CheckoutConflictReason.PaymentReview, "paymentReviewRequired", "reconcile the payment with the provider")]
    [InlineData(CheckoutConflictReason.Completed, "checkoutCompleted", "verify the completed payment")]
    [InlineData(CheckoutConflictReason.PreparationUnavailable, "checkout_state_conflict", "review its checkout and payment state")]
    [InlineData((CheckoutConflictReason)999, "checkout_state_conflict", "review its checkout and payment state")]
    public void MapsReasonsToSafeManagerGuidance(CheckoutConflictReason reason, string code, string guidance)
    {
        var payload = JObject.FromObject(EkomManagerController.GetStatusConflictResponse(reason, "IS-1234"));

        Assert.Equal(new[] { "code", "message" }, payload.Properties().Select(x => x.Name).ToArray());
        Assert.Equal(code, payload["code"]!.Value<string>());
        var message = payload["message"]!.Value<string>()!;
        Assert.StartsWith("The status of order IS-1234 was not changed because ", message, StringComparison.Ordinal);
        Assert.Contains(guidance, message, StringComparison.Ordinal);
        if (reason == CheckoutConflictReason.Busy)
        {
            Assert.Contains("checkout is locked", message, StringComparison.Ordinal);
            Assert.Contains("Open the order via View", message, StringComparison.Ordinal);
            Assert.Contains("Release Lock and review its warnings before retrying", message, StringComparison.Ordinal);
        }
        else if (reason == CheckoutConflictReason.PaymentReview)
        {
            Assert.Contains("before considering Release Lock", message, StringComparison.Ordinal);
            Assert.Contains("do not blindly retry", message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("Reload the order", message, StringComparison.Ordinal);
            Assert.DoesNotContain("Release Lock", message, StringComparison.Ordinal);
            Assert.DoesNotContain("retry", message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingDisplayNumberUsesGenericSubject(string? orderNumber)
    {
        var payload = JObject.FromObject(EkomManagerController.GetStatusConflictResponse(CheckoutConflictReason.Busy, orderNumber));

        Assert.StartsWith("The order status was not changed", payload["message"]!.Value<string>()!, StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayNumberCannotIntroduceHtmlIntoMessage()
    {
        var payload = JObject.FromObject(EkomManagerController.GetStatusConflictResponse(
            CheckoutConflictReason.Busy, "<script>alert('order')</script>"));

        var message = payload["message"]!.Value<string>()!;
        Assert.DoesNotContain("<", message, StringComparison.Ordinal);
        Assert.DoesNotContain(">", message, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", message, StringComparison.Ordinal);
    }
}
