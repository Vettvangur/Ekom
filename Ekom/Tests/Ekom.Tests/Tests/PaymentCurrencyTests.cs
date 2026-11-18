using Ekom.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class PaymentCurrencyTests
{
    [Theory]
    [InlineData("ISK", "ISK")]
    [InlineData("EUR", "EUR")]
    // Not in the Ekom.Payments.Currency enum. These used to be sent as ISK.
    [InlineData("JPY", "JPY")]
    [InlineData("NZD", "NZD")]
    [InlineData("jpy", "JPY")]
    [InlineData(" USD ", "USD")]
    public void StoreCurrencyIsSentAsItsOwnIsoCode(string storeCurrency, string expected)
    {
        Assert.Equal(expected, CheckoutControllerService.ResolvePaymentCurrency(storeCurrency, NullLogger.Instance));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kr")]
    [InlineData("12")]
    [InlineData("EURO")]
    public void CurrencyWithoutAnIsoCodeIsSentAsIskAndLogged(string? storeCurrency)
    {
        var logger = new Mock<ILogger>();

        Assert.Equal("ISK", CheckoutControllerService.ResolvePaymentCurrency(storeCurrency, logger.Object));

        logger.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}
