using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class CheckoutStateErrorTests
{
    private const string Secret = "SQL order-123 customer@example.test giftcard-secret";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnclassifiedPreparationConflictsNeverExposeDiagnostics(bool stock)
    {
        Exception exception = stock ? new StockException(Secret) : new EkomHttpException(HttpStatusCode.Conflict, Secret);
        var response = await Controller().HandleCheckoutStateAsync(new PaymentRequest { ReturnUrl = "/basket" },
            _ => Task.FromException<CheckoutResponse>(exception));

        Assert.Equal(409, response.HttpStatusCode);
        Assert.Equal("/basket", response.ReturnUrl);
        var error = Assert.IsType<CheckoutStateError>(response.ResponseBody);
        Assert.Equal("checkout_state_conflict", error.Code);
        Assert.Contains("refresh your basket", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("review", error.Message, StringComparison.Ordinal);
        Assert.False(error.CanRetry);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(error), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, Newtonsoft.Json.JsonConvert.SerializeObject(error), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CheckoutConflictReason.Busy, "checkoutBusy")]
    [InlineData(CheckoutConflictReason.PaymentReview, "paymentReviewRequired")]
    [InlineData(CheckoutConflictReason.Completed, "checkoutCompleted")]
    public async Task ExplicitConflictReasonsHaveStableSafeResponses(CheckoutConflictReason reason, string code)
    {
        var response = await Controller().HandleCheckoutStateAsync(new PaymentRequest(),
            _ => Task.FromException<CheckoutResponse>(new CheckoutConflictException(reason, Secret)));
        var error = Assert.IsType<CheckoutStateError>(response.ResponseBody);
        Assert.Equal(code, error.Code);
        Assert.False(error.CanRetry);
        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingProductNamesTheOrderLineToRemove()
    {
        var lineKey = Guid.NewGuid();
        var productKey = Guid.NewGuid();
        var exception = new OrderLineUnavailableException(lineKey, productKey, isVariant: false, "SKU-42", "Blue Mug", "Store");
        Assert.Contains("SKU-42", exception.Message, StringComparison.Ordinal);
        Assert.Contains(lineKey.ToString(), exception.Message, StringComparison.Ordinal);

        var response = await Controller().HandleCheckoutStateAsync(new PaymentRequest { ReturnUrl = "/basket" },
            _ => Task.FromException<CheckoutResponse>(exception));

        Assert.Equal(409, response.HttpStatusCode);
        Assert.Equal("/basket", response.ReturnUrl);
        var error = Assert.IsType<CheckoutStateError>(response.ResponseBody);
        Assert.Equal("productUnavailable", error.Code);
        Assert.Equal("\"Blue Mug\" is no longer available. Please remove it from your basket to continue.", error.Message);
        Assert.Equal(lineKey, error.OrderLineKey);
        Assert.Equal("SKU-42", error.Sku);
        Assert.False(error.CanRetry);
        foreach (var json in new[] { JsonSerializer.Serialize(error), Newtonsoft.Json.JsonConvert.SerializeObject(error) })
        {
            using var document = JsonDocument.Parse(json);
            Assert.Equal(lineKey, document.RootElement.GetProperty("orderLineKey").GetGuid());
            Assert.Equal("SKU-42", document.RootElement.GetProperty("sku").GetString());
            Assert.DoesNotContain(productKey.ToString(), json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MissingProductWithoutTitleFallsBackToSkuThenGenericMessage()
    {
        Assert.StartsWith("The product with SKU SKU-42 is no longer available",
            CheckoutStateError.ProductUnavailable(Guid.NewGuid(), "SKU-42", " ").Message, StringComparison.Ordinal);
        var generic = CheckoutStateError.ProductUnavailable(Guid.Empty, null, null);
        Assert.Equal(new CheckoutStateError(CheckoutConflictReason.ProductUnavailable).Message, generic.Message);
        Assert.Null(generic.OrderLineKey);
        Assert.Null(generic.Sku);
    }

    [Fact]
    public async Task GiftcardFailureReceiptIsNotExposedToTheCustomer()
    {
        var failure = new GiftcardReservationFailure
        {
            OrderId = Guid.NewGuid(),
            AttemptId = Guid.NewGuid(),
            Claims = [new GiftcardRecoveryClaim { CardCode = Secret, ClaimId = Secret, RawReturnedAmount = Secret }],
        };
        var exception = new GiftcardReservationFailureException(failure, new FormatException(Secret));

        var response = await Controller().HandleCheckoutStateAsync(new PaymentRequest { ReturnUrl = "/basket" },
            _ => Task.FromException<CheckoutResponse>(exception));

        Assert.Equal(409, response.HttpStatusCode);
        Assert.Equal("/basket", response.ReturnUrl);
        var error = Assert.IsType<CheckoutStateError>(response.ResponseBody);
        Assert.False(error.CanRetry);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(error), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, Newtonsoft.Json.JsonConvert.SerializeObject(error), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GiftcardFailureAfterPaymentInvocationStillPropagates()
    {
        var exception = new GiftcardReservationFailureException(new GiftcardReservationFailure());

        var actual = await Assert.ThrowsAsync<GiftcardReservationFailureException>(() => Controller().HandleCheckoutStateAsync(
            new PaymentRequest(), paymentStarting =>
            {
                paymentStarting();
                return Task.FromException<CheckoutResponse>(exception);
            }));

        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task ExistingOutOfStockResponseIsReturnedUnchanged()
    {
        var stockError = new StockError { OrderLineKey = Guid.NewGuid(), IsVariant = true };
        var original = new CheckoutResponse { HttpStatusCode = 530, ResponseBody = stockError };
        var response = await Controller().HandleCheckoutStateAsync(new PaymentRequest(), _ => Task.FromResult(original));
        Assert.Same(original, response);
        Assert.Same(stockError, response.ResponseBody);
        Assert.Equal(530, response.HttpStatusCode);
    }

    [Fact]
    public async Task OutOfStockExceptionsAreNotReclassifiedAsStateConflicts()
    {
        var exception = new NotEnoughStockException(Secret);
        var actual = await Assert.ThrowsAsync<NotEnoughStockException>(() => Controller().HandleCheckoutStateAsync(
            new PaymentRequest(), _ => Task.FromException<CheckoutResponse>(exception)));
        Assert.Same(exception, actual);
    }

    [Fact]
    public async Task ProgrammingErrorsAndNonConflictHttpErrorsStillPropagate()
    {
        foreach (var exception in new Exception[]
        {
            new InvalidOperationException(Secret),
            new EkomHttpException(HttpStatusCode.InternalServerError, Secret),
            new OperationCanceledException(),
        })
        {
            var actual = await Assert.ThrowsAnyAsync<Exception>(() => Controller().HandleCheckoutStateAsync(
                new PaymentRequest(), _ => Task.FromException<CheckoutResponse>(exception)));
            Assert.Same(exception, actual);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderExceptionsAfterSubmissionAreNotMappedToRetryablePreparationErrors(bool stock)
    {
        Exception exception = stock ? new StockException(Secret) : new EkomHttpException(HttpStatusCode.Conflict, Secret);
        var actual = await Assert.ThrowsAnyAsync<Exception>(() => Controller().HandleCheckoutStateAsync(
            new PaymentRequest(), paymentStarting =>
            {
                paymentStarting();
                return Task.FromException<CheckoutResponse>(exception);
            }));
        Assert.Same(exception, actual);
    }

    [Fact]
    public void BothSerializersUseThePublicResponseContract()
    {
        var error = new CheckoutStateError(CheckoutConflictReason.Busy);
        foreach (var json in new[] { JsonSerializer.Serialize(error), Newtonsoft.Json.JsonConvert.SerializeObject(error) })
        {
            using var document = JsonDocument.Parse(json);
            Assert.Equal("checkoutBusy", document.RootElement.GetProperty("code").GetString());
            Assert.False(document.RootElement.GetProperty("canRetry").GetBoolean());
            Assert.Equal(error.Message, document.RootElement.GetProperty("message").GetString());
            Assert.Equal(3, document.RootElement.EnumerateObject().Count());
        }
    }

    private static CheckoutControllerService Controller() => new(
        NullLogger.Instance, null!, null!, null!, new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
        null!, null!, null!);
}
