using Ekom.Mailchimp.Dispatching;
using Ekom.Mailchimp.Enrichers;
using Ekom.Mailchimp.Mappers;
using Ekom.Mailchimp.Models;
using Ekom.Models;
using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp.Services;

internal sealed class MailchimpService : IMailchimpService
{
    private readonly IMailchimpDispatcher _dispatcher;
    private readonly IEnumerable<IMailchimpPurchaseEnricher> _enrichers;
    private readonly MailchimpOptions _options;

    public MailchimpService(
        IOptions<MailchimpOptions> options,
        IMailchimpDispatcher dispatcher,
        IEnumerable<IMailchimpPurchaseEnricher> enrichers)
    {
        _options = options.Value;
        _dispatcher = dispatcher;
        _enrichers = enrichers;
    }

    public ValueTask SubscribeAsync(MailchimpSubscribeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.StoreAlias);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        return !_options.Enabled || !_options.Subscriptions.Enabled
            ? ValueTask.CompletedTask
            : _dispatcher.EnqueueAsync(new SubscribeWorkItem(request), ct);
    }

    public ValueTask UnsubscribeAsync(MailchimpUnsubscribeRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.StoreAlias);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        return !_options.Enabled || !_options.Subscriptions.Enabled
            ? ValueTask.CompletedTask
            : _dispatcher.EnqueueAsync(new UnsubscribeWorkItem(request), ct);
    }

    public async ValueTask TrackPurchaseAsync(MailchimpPurchase purchase, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        if (!_options.Enabled || !_options.Purchases.Enabled)
        {
            return;
        }

        MailchimpPurchase enrichedPurchase = purchase;
        foreach (IMailchimpPurchaseEnricher enricher in _enrichers)
        {
            enrichedPurchase = await enricher.EnrichAsync(enrichedPurchase, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"{enricher.GetType().Name} returned a null purchase.");
        }

        MailchimpPurchaseValidator.Validate(enrichedPurchase);
        await _dispatcher.EnqueueAsync(new PurchaseWorkItem(enrichedPurchase), ct).ConfigureAwait(false);
    }

    public ValueTask TrackPurchaseAsync(IOrderInfo order, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (!_options.Enabled || !_options.Purchases.Enabled)
        {
            return ValueTask.CompletedTask;
        }

        return TrackPurchaseAsync(order.ToMailchimpPurchase(_options), ct);
    }
}
