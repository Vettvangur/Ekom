using Ekom.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ekom.Shipping;

/// <summary>
/// Books the carrier shipment after checkout has completed, for shipping methods whose carrier account
/// creates shipments automatically.
/// </summary>
internal sealed class ShippingAutomationHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShippingAutomationHostedService> _logger;

    public ShippingAutomationHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ShippingAutomationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Raised after completion, so a shipment is never booked for an order whose completion fails.
        CheckoutEvents.CompleteCheckoutAsync += OnCompleteCheckoutAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
        return Task.CompletedTask;
    }

    internal async Task OnCompleteCheckoutAsync(
        object sender,
        CompleteCheckoutEventArgs args,
        CancellationToken cancellationToken)
    {
        // A shipment that cannot be booked must never fail a paid checkout.
        try
        {
            if (args.OrderInfo?.ShippingProvider is null)
            {
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            if (!scope.ServiceProvider.GetRequiredService<IShippingFulfillmentCarrierRegistry>().Carriers.Any())
            {
                return;
            }

            var fulfillment = scope.ServiceProvider.GetRequiredService<IShippingFulfillmentService>();
            await fulfillment.CreateAutomaticAsync(args.OrderInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Automatic shipment creation failed after checkout completion for order {OrderId}",
                args.OrderInfo?.UniqueId);
        }
    }
}
