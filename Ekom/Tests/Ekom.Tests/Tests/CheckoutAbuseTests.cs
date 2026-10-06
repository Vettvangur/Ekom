using Ekom.Models;
using Xunit;

namespace Ekom.Tests.Tests;

// What a customer could try around the payment window to get more than they paid for.
// Each must end with at most one completed order, for exactly the basket that was paid for,
// with stock deducted once. A payment that can't complete is flagged for reconciliation.
[Collection("Reservations")]
public sealed class CheckoutAbuseTests
{
    // Pay for 1, then raise the basket to 3 before the provider confirms.
    [Fact]
    public async Task PaymentForABasketChangedAfterPayingDoesNotComplete()
    {
        using var f = new CheckoutPaymentWindowFixture(reservations: true);
        Assert.Equal("230", await f.PayAsync());
        var first = await f.CurrentAttemptIdAsync();
        await f.EditQuantityAsync(3);

        Assert.False(await f.ProviderConfirmsPaymentAsync(first));
        Assert.Equal(CheckoutPaymentAttemptState.ReconciliationRequired, await f.AttemptStateAsync(first));
        Assert.False(await f.StockCompletedAsync());
        Assert.Equal(10, await f.StockAsync());
    }

    // The same, with the basket changed after the payment window has passed.
    [Fact]
    public async Task PaymentForABasketChangedAfterTheWindowDoesNotComplete()
    {
        using var f = new CheckoutPaymentWindowFixture(reservations: true);
        Assert.Equal("230", await f.PayAsync());
        var first = await f.CurrentAttemptIdAsync();
        await f.PaymentWindowPassesAsync();
        await f.EditQuantityAsync(3);

        Assert.False(await f.ProviderConfirmsPaymentAsync(first));
        Assert.Equal(CheckoutPaymentAttemptState.ReconciliationRequired, await f.AttemptStateAsync(first));
        Assert.False(await f.StockCompletedAsync());
        Assert.Equal(10, await f.StockAsync());
    }

    // A second payment page for the same basket while the first is live (back button, second tab).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SecondCheckoutWithinTheWindowIsBusy(bool reservations)
    {
        using var f = new CheckoutPaymentWindowFixture(reservations);
        Assert.Equal("230", await f.PayAsync());

        Assert.Equal("409 checkoutBusy", await f.PayAsync());
        Assert.Equal(reservations ? 9 : 10, await f.StockAsync());
    }

    // The provider confirms after the hold expired, when the stock may already be sold.
    [Fact]
    public async Task PaymentAfterItsHoldExpiredDoesNotComplete()
    {
        using var f = new CheckoutPaymentWindowFixture(reservations: true);
        Assert.Equal("230", await f.PayAsync());
        var first = await f.CurrentAttemptIdAsync();
        await f.PaymentWindowPassesAsync();

        Assert.False(await f.ProviderConfirmsPaymentAsync(first));
        Assert.Equal(CheckoutPaymentAttemptState.ReconciliationRequired, await f.AttemptStateAsync(first));
        Assert.False(await f.StockCompletedAsync());
        Assert.Equal(10, await f.StockAsync());
    }

    // Without holds no stock was promised, so a slow payment with no second attempt still completes.
    [Fact]
    public async Task SlowPaymentWithoutHoldsStillCompletes()
    {
        using var f = new CheckoutPaymentWindowFixture(reservations: false);
        Assert.Equal("230", await f.PayAsync());
        var first = await f.CurrentAttemptIdAsync();
        await f.PaymentWindowPassesAsync();

        Assert.True(await f.ProviderConfirmsPaymentAsync(first));
        Assert.Equal(9, await f.StockAsync());
    }

    // Pay again after the window, then also complete the first payment at the provider.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FirstPaymentDoesNotCompleteAfterThePaidRetry(bool reservations)
    {
        using var f = new CheckoutPaymentWindowFixture(reservations);
        Assert.Equal("230", await f.PayAsync());
        var first = await f.CurrentAttemptIdAsync();
        await f.PaymentWindowPassesAsync();
        Assert.Equal("230", await f.PayAsync());
        Assert.True(await f.ProviderConfirmsPaymentAsync());

        Assert.False(await f.ProviderConfirmsPaymentAsync(first));
        Assert.Equal(CheckoutPaymentAttemptState.ReconciliationRequired, await f.AttemptStateAsync(first));
        Assert.Equal(9, await f.StockAsync());
    }

    // Change the basket after the window and pay for it: only the new basket is completed.
    [Fact]
    public async Task BasketChangedAfterTheWindowCompletesOnlyTheNewPayment()
    {
        using var f = new CheckoutPaymentWindowFixture(reservations: true);
        Assert.Equal("230", await f.PayAsync());
        var first = await f.CurrentAttemptIdAsync();
        await f.PaymentWindowPassesAsync();
        await f.EditQuantityAsync(2);
        Assert.Equal("230", await f.PayAsync());
        Assert.True(await f.ProviderConfirmsPaymentAsync());

        Assert.False(await f.ProviderConfirmsPaymentAsync(first));
        Assert.Equal(8, await f.StockAsync());
    }

    // The same within the window.
    [Fact]
    public async Task BasketChangedWithinTheWindowCompletesOnlyTheNewPayment()
    {
        using var f = new CheckoutPaymentWindowFixture(reservations: true);
        Assert.Equal("230", await f.PayAsync());
        var first = await f.CurrentAttemptIdAsync();
        await f.EditQuantityAsync(2);
        Assert.Equal("230", await f.PayAsync());
        Assert.True(await f.ProviderConfirmsPaymentAsync());

        Assert.False(await f.ProviderConfirmsPaymentAsync(first));
        Assert.Equal(8, await f.StockAsync());
    }
}
