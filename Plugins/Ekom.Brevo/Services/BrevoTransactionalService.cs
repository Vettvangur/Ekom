using Ekom.Brevo.Dispatching;
using Ekom.Brevo.Mappers;
using Ekom.Brevo.Models;

namespace Ekom.Brevo.Services;

internal sealed class BrevoTransactionalService(BrevoConfiguration configuration, BrevoTransactionalDispatcher dispatcher)
    : IBrevoTransactionalService
{
    public async ValueTask<BrevoSubmission> SendAsync(BrevoTransactionalMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        string id = Guid.NewGuid().ToString("N");
        if (!configuration.Enabled(message.StoreAlias, transactional: true))
            return new(BrevoSubmissionStatus.Disabled, id);
        var key = configuration.ApiKey(message.StoreAlias, transactional: true);
        if (string.IsNullOrWhiteSpace(key))
            return new(BrevoSubmissionStatus.ConfigurationMissing, id);
        var payload = BrevoTransactionalMapper.Map(message, configuration);
        return await dispatcher.EnqueueAsync(message, id, payload, cancellationToken).ConfigureAwait(false);
    }
}
