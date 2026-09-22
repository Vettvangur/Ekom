using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp;

internal sealed class MailchimpConfigurationStartupService : IHostedService
{
    private readonly IMailchimpConfigurationResolver _configurationResolver;
    private readonly IMailchimpTransactionalConfigurationResolver _transactionalConfigurationResolver;
    private readonly MailchimpOptions _options;

    public MailchimpConfigurationStartupService(
        IOptions<MailchimpOptions> options,
        IMailchimpConfigurationResolver configurationResolver,
        IMailchimpTransactionalConfigurationResolver transactionalConfigurationResolver)
    {
        _options = options.Value;
        _configurationResolver = configurationResolver;
        _transactionalConfigurationResolver = transactionalConfigurationResolver;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        IEnumerable<string> storeAliases = _options.Stores.Count == 0
            ? ["default"]
            : _options.Stores.Select(x => x.Alias);
        foreach (string storeAlias in storeAliases)
        {
            if (_options.Subscriptions.Enabled)
            {
                _configurationResolver.TryResolve(storeAlias, requireEcommerceStore: false, out _);
            }

            if (_options.Purchases.Enabled)
            {
                _configurationResolver.TryResolve(storeAlias, requireEcommerceStore: true, out _);
            }

            if (_transactionalConfigurationResolver.IsEnabled(storeAlias))
            {
                _transactionalConfigurationResolver.TryResolve(storeAlias, out _);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
