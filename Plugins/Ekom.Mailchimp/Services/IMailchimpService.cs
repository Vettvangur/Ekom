using Ekom.Mailchimp.Models;
using Ekom.Models;

namespace Ekom.Mailchimp.Services;

public interface IMailchimpService
{
    Task<IReadOnlyList<MailchimpTag>> GetTagsAsync(CancellationToken ct = default)
        => Task.FromException<IReadOnlyList<MailchimpTag>>(
            new NotSupportedException("This Mailchimp service does not support fetching audience tags."));

    /// <summary>Queues a subscription; throws MailchimpRateLimitExceededException or MailchimpSubscriptionQueueFullException when not accepted.</summary>
    ValueTask SubscribeAsync(MailchimpSubscribeRequest request, CancellationToken ct = default);
    /// <summary>Queues an opt-out using its independent rate limit; rejected requests are not silently dropped.</summary>
    ValueTask UnsubscribeAsync(MailchimpUnsubscribeRequest request, CancellationToken ct = default);
    ValueTask TrackPurchaseAsync(MailchimpPurchase purchase, CancellationToken ct = default);
    ValueTask TrackPurchaseAsync(IOrderInfo order, CancellationToken ct = default);
}
