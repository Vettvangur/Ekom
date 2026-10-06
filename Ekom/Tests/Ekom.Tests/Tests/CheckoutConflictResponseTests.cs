using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class CheckoutConflictResponseTests
{
    [Theory]
    [InlineData(CheckoutConflictReason.Busy, "checkoutBusy")]
    [InlineData(CheckoutConflictReason.PaymentReview, "paymentReviewRequired")]
    [InlineData(CheckoutConflictReason.Completed, "checkoutCompleted")]
    public void CartAndReturnConflictsNeverExposeDiagnosticExceptionMessages(CheckoutConflictReason reason, string code)
    {
        const string diagnostic = "Internal ownership details and external claim identifiers";

        var response = Assert.IsType<ObjectResult>(ExceptionHandler.Handle(new CheckoutConflictException(reason, diagnostic)));
        var error = Assert.IsType<CheckoutStateError>(response.Value);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(code, error.Code);
        Assert.False(error.CanRetry);
        Assert.DoesNotContain(diagnostic, error.Message, StringComparison.Ordinal);
    }
}
