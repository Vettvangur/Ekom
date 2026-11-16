using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Services;

/// <summary>Completes the checkout of an order whose payment was captured.</summary>
internal interface ICheckoutCompletion
{
    /// <returns>False when the payment arrived for an attempt that needs reconciliation.</returns>
    Task<bool> CompleteAsync(Guid orderId, Guid? attemptId, CancellationToken ct = default);
}

internal sealed class CheckoutCompletion : ICheckoutCompletion
{
    private readonly IServiceProvider _services;

    public CheckoutCompletion(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<bool> CompleteAsync(Guid orderId, Guid? attemptId, CancellationToken ct = default)
    {
        var checkoutService = _services.GetRequiredService<CheckoutService>();
        var attempts = _services.GetService<CheckoutPaymentAttemptService>();
        if (attempts == null)
        {
            await checkoutService.CompleteAsync(orderId, ct).ConfigureAwait(false);
            return true;
        }

        return await attempts.CompleteAsync(orderId, attemptId, () => checkoutService.CompleteAsync(orderId, ct), ct).ConfigureAwait(false);
    }
}
