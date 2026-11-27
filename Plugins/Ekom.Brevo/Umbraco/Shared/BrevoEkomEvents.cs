using Ekom.Brevo.Models;
using Ekom.Brevo.Services;
using Ekom.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Ekom.Brevo.Events;

internal sealed class BrevoEkomEvents : IAsyncComponent
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BrevoEkomEvents> _logger;

    public BrevoEkomEvents(IServiceScopeFactory scopeFactory, ILogger<BrevoEkomEvents> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        CheckoutEvents.CompleteCheckoutAsync += OnCompleteCheckoutAsync;
        return Task.CompletedTask;
    }

    internal async Task OnCompleteCheckoutAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetService<IBrevoOrderService>();
        var contacts = scope.ServiceProvider.GetService<IBrevoContactsService>();
        if (service == null || contacts == null) return;
        var options = scope.ServiceProvider.GetRequiredService<IOptions<BrevoOptions>>().Value;
        if (!options.Enabled) return;
        var order = args.OrderInfo;
        if (order == null) return;
        var customer = order.CustomerInformation.Customer;
        // Contact opt-in is independent of order tracking. Missing/false consent never unsubscribes.
        if (options.Subscriptions.Enabled && HasConsent(customer.Value("customerBrevoConsentToSubscribeEmail")))
        {
            await TryQueueAsync(() => contacts.SubscribeAsync(new BrevoContact
            {
                StoreAlias = order.StoreInfo.Alias,
                Email = customer.Email,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
            }, cancellationToken: ct), order.StoreInfo.Alias, "subscription").ConfigureAwait(false);
        }
        if (options.Orders.Enabled && options.Orders.TrackCompletedCheckouts)
            await TryQueueAsync(() => service.TrackOrderAsync(order, ct), order.StoreInfo.Alias, "order").ConfigureAwait(false);
    }

    private async Task TryQueueAsync(Func<ValueTask<BrevoSubmission>> enqueue, string store, string operation)
    {
        try
        {
            var result = await enqueue().ConfigureAwait(false);
            if (result.Status is not (BrevoSubmissionStatus.Queued or BrevoSubmissionStatus.Duplicate or BrevoSubmissionStatus.Disabled))
                _logger.LogWarning("Brevo {Operation} for store {StoreAlias} was not queued: {Status}", operation, store, result.Status);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Invalid marketing data must not fail a completed checkout; exception messages can contain PII.
            _logger.LogWarning("Brevo {Operation} for store {StoreAlias} failed validation ({ErrorType})", operation, store, ex.GetType().Name);
        }
    }

    internal static bool HasConsent(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) || value == "1";

    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
        return Task.CompletedTask;
    }
}

public sealed class BrevoComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.Components().Append<BrevoEkomEvents>();
}
