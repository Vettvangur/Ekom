using Xunit;

namespace Ekom.Tests.Tests;

// A customer leaves the payment page and comes back after the payment window has passed.
// Each test runs the real flow with payment attempts registered, as in production:
// pay, the window passes, pay again, then the provider's verified success.
[Collection("Reservations")]
public sealed class CheckoutRetryAfterTimeoutTests
{
    [Fact]
    public async Task PayAgainAfterHoldExpiredCompletesTheNewPayment()
    {
        using var f = new CheckoutPaymentWindowFixture(reservations: true);
        Assert.Equal("230", await f.PayAsync());
        await f.PaymentWindowPassesAsync();
        Assert.Equal(10, await f.StockAsync());

        Assert.Equal("230", await f.PayAsync());
        Assert.Equal(9, await f.StockAsync());
        Assert.True(await f.ProviderConfirmsPaymentAsync());
        Assert.True(await f.StockCompletedAsync());
        Assert.Equal(9, await f.StockAsync());
    }

    [Fact]
    public async Task CancelAfterHoldExpiredThenPayAgainCompletesTheNewPayment()
    {
        using var f = new CheckoutPaymentWindowFixture(reservations: true);
        Assert.Equal("230", await f.PayAsync());
        await f.PaymentWindowPassesAsync();
        await f.CancelReturnAsync();

        Assert.Equal("230", await f.PayAsync());
        Assert.True(await f.ProviderConfirmsPaymentAsync());
        Assert.True(await f.StockCompletedAsync());
        Assert.Equal(9, await f.StockAsync());
    }

    [Fact]
    public async Task LegacyLineOverridePayAgainAfterHoldExpiredCompletesTheNewPayment()
    {
        using var f = new CheckoutPaymentWindowFixture(reservations: false, legacyLineOverride: true);
        Assert.Equal("230", await f.PayAsync());
        await f.PaymentWindowPassesAsync();

        Assert.Equal("230", await f.PayAsync());
        Assert.Equal(9, await f.StockAsync());
        Assert.True(await f.ProviderConfirmsPaymentAsync());
        Assert.True(await f.StockCompletedAsync());
        Assert.Equal(9, await f.StockAsync());
    }

    [Fact]
    public async Task PayAgainAfterAbandonedPaymentWithoutHoldsWorksOnceTheWindowHasPassed()
    {
        // Default settings: reservations are disabled, so the attempt has no holds.
        using var f = new CheckoutPaymentWindowFixture(reservations: false);
        Assert.Equal("230", await f.PayAsync());
        await f.PaymentWindowPassesAsync();

        Assert.Equal("230", await f.PayAsync());
        Assert.True(await f.ProviderConfirmsPaymentAsync());
        Assert.True(await f.StockCompletedAsync());
        Assert.Equal(9, await f.StockAsync());
    }
}
