using Ekom.Mailchimp.Models;

namespace Ekom.Mailchimp.Services;

public interface IMailchimpTransactionalService
{
    ValueTask<MailchimpTransactionalEnqueueResult> QueueMessageAsync(
        MailchimpTransactionalMessage message,
        CancellationToken ct = default);

    ValueTask<MailchimpTransactionalEnqueueResult> QueueTemplateAsync(
        MailchimpTransactionalTemplateMessage message,
        CancellationToken ct = default);
}
