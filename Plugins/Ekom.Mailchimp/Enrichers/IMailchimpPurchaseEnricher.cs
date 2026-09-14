using Ekom.Mailchimp.Models;

namespace Ekom.Mailchimp.Enrichers;

public interface IMailchimpPurchaseEnricher
{
    ValueTask<MailchimpPurchase> EnrichAsync(MailchimpPurchase purchase, CancellationToken ct = default);
}
