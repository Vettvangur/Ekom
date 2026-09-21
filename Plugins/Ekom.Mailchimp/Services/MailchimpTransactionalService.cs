using Ekom.Mailchimp.Dispatching;
using Ekom.Mailchimp.Models;
using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp.Services;

internal sealed class MailchimpTransactionalService : IMailchimpTransactionalService
{
    private readonly IMailchimpTransactionalConfigurationResolver _configurationResolver;
    private readonly IMailchimpTransactionalDispatcher _dispatcher;
    private readonly MailchimpTransactionalDispatcherOptions _options;

    public MailchimpTransactionalService(
        IOptions<MailchimpOptions> options,
        IMailchimpTransactionalConfigurationResolver configurationResolver,
        IMailchimpTransactionalDispatcher dispatcher)
    {
        _options = options.Value.Transactional.Dispatching;
        _configurationResolver = configurationResolver;
        _dispatcher = dispatcher;
    }

    public ValueTask<MailchimpTransactionalEnqueueResult> QueueMessageAsync(
        MailchimpTransactionalMessage message,
        CancellationToken ct = default)
    {
        if (message == null)
        {
            return Result(MailchimpTransactionalEnqueueStatus.InvalidRequest, "Message is required.");
        }
        if (ct.IsCancellationRequested)
        {
            return Result(MailchimpTransactionalEnqueueStatus.Cancelled);
        }
        if (string.IsNullOrWhiteSpace(message.StoreAlias))
        {
            return Result(MailchimpTransactionalEnqueueStatus.InvalidRequest, "StoreAlias is required.");
        }
        if (!_configurationResolver.IsEnabled(message.StoreAlias))
        {
            return Result(MailchimpTransactionalEnqueueStatus.Disabled);
        }
        if (!_configurationResolver.TryResolve(
            message.StoreAlias,
            out MailchimpTransactionalStoreConfiguration? configuration))
        {
            return Result(MailchimpTransactionalEnqueueStatus.ConfigurationMissing);
        }
        if (!MailchimpTransactionalMessageValidator.TryValidateAndSnapshot(
            message,
            configuration,
            _options.MaxMessageBytes,
            out MailchimpTransactionalMessage? snapshot,
            out long payloadBytes,
            out IReadOnlyList<string> errors))
        {
            return Result(MailchimpTransactionalEnqueueStatus.InvalidRequest, errors);
        }

        return Result(_dispatcher.TryEnqueue(new TransactionalMessageWorkItem(snapshot!, payloadBytes))
            ? MailchimpTransactionalEnqueueStatus.Queued
            : MailchimpTransactionalEnqueueStatus.QueueFull);
    }

    public ValueTask<MailchimpTransactionalEnqueueResult> QueueTemplateAsync(
        MailchimpTransactionalTemplateMessage message,
        CancellationToken ct = default)
    {
        if (message == null)
        {
            return Result(MailchimpTransactionalEnqueueStatus.InvalidRequest, "Message is required.");
        }
        if (ct.IsCancellationRequested)
        {
            return Result(MailchimpTransactionalEnqueueStatus.Cancelled);
        }
        if (string.IsNullOrWhiteSpace(message.StoreAlias))
        {
            return Result(MailchimpTransactionalEnqueueStatus.InvalidRequest, "StoreAlias is required.");
        }
        if (!_configurationResolver.IsEnabled(message.StoreAlias))
        {
            return Result(MailchimpTransactionalEnqueueStatus.Disabled);
        }
        if (!_configurationResolver.TryResolve(
            message.StoreAlias,
            out MailchimpTransactionalStoreConfiguration? configuration))
        {
            return Result(MailchimpTransactionalEnqueueStatus.ConfigurationMissing);
        }
        if (!MailchimpTransactionalMessageValidator.TryValidateAndSnapshot(
            message,
            configuration,
            _options.MaxMessageBytes,
            out MailchimpTransactionalTemplateMessage? snapshot,
            out long payloadBytes,
            out IReadOnlyList<string> errors))
        {
            return Result(MailchimpTransactionalEnqueueStatus.InvalidRequest, errors);
        }

        return Result(_dispatcher.TryEnqueue(new TransactionalTemplateWorkItem(snapshot!, payloadBytes))
            ? MailchimpTransactionalEnqueueStatus.Queued
            : MailchimpTransactionalEnqueueStatus.QueueFull);
    }

    private static ValueTask<MailchimpTransactionalEnqueueResult> Result(
        MailchimpTransactionalEnqueueStatus status,
        params IReadOnlyList<string> errors)
        => ValueTask.FromResult(new MailchimpTransactionalEnqueueResult
        {
            Status = status,
            Errors = errors,
        });
}
