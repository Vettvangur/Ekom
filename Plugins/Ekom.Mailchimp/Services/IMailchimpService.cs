using Ekom.Mailchimp.Models;
using Ekom.Models;

namespace Ekom.Mailchimp.Services;

public interface IMailchimpService
{
    Task<IReadOnlyList<MailchimpTag>> GetTagsAsync(CancellationToken ct = default)
        => Task.FromException<IReadOnlyList<MailchimpTag>>(
            new NotSupportedException("This Mailchimp service does not support fetching audience tags."));

    ValueTask SubscribeAsync(MailchimpSubscribeRequest request, CancellationToken ct = default);
    ValueTask UnsubscribeAsync(MailchimpUnsubscribeRequest request, CancellationToken ct = default);
    ValueTask TrackPurchaseAsync(MailchimpPurchase purchase, CancellationToken ct = default);
    ValueTask TrackPurchaseAsync(IOrderInfo order, CancellationToken ct = default);
}
