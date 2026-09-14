using Ekom.Mailchimp.Models;
using Ekom.Models;

namespace Ekom.Mailchimp.Services;

public interface IMailchimpService
{
    ValueTask SubscribeAsync(MailchimpSubscribeRequest request, CancellationToken ct = default);
    ValueTask UnsubscribeAsync(MailchimpUnsubscribeRequest request, CancellationToken ct = default);
    ValueTask TrackPurchaseAsync(MailchimpPurchase purchase, CancellationToken ct = default);
    ValueTask TrackPurchaseAsync(IOrderInfo order, CancellationToken ct = default);
}
