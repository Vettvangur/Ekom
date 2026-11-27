using Ekom.Brevo.Dispatching;
using Ekom.Brevo.Mappers;
using Ekom.Brevo.Models;
using System.Net.Http;

namespace Ekom.Brevo.Services;

internal sealed class BrevoTransactionalService(BrevoConfiguration configuration, BrevoTransactionalDispatcher dispatcher)
    : IBrevoTransactionalService
{
    public ValueTask<BrevoSubmission> SendAsync(BrevoTransactionalMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        string id = Guid.NewGuid().ToString("N");
        if (!configuration.Enabled(message.StoreAlias, transactional: true))
            return ValueTask.FromResult(new BrevoSubmission(BrevoSubmissionStatus.Disabled, id));
        var key = configuration.ApiKey(message.StoreAlias, transactional: true);
        if (string.IsNullOrWhiteSpace(key))
            return ValueTask.FromResult(new BrevoSubmission(BrevoSubmissionStatus.ConfigurationMissing, id));
        var payload = BrevoTransactionalMapper.Map(message, configuration);
        return ValueTask.FromResult(dispatcher.Enqueue(message.StoreAlias, key, HttpMethod.Post, "smtp/email", payload, false, id));
    }
}
