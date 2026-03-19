using Ekom.Klaviyo.Clients;
using Ekom.Klaviyo.Models.Profiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Dispatching.Profiles;

public interface IKlaviyoProfilesDispatcher
{
    ValueTask EnqueueAsync(KlaviyoProfilesWork work, CancellationToken ct = default);
}

public sealed record KlaviyoProfilesWork(
    KlaviyoProfilesEventType Type,
    object Payload,
    DateTimeOffset OccurredAt,
    string StoreAlias,
    string CustomerIdentifier,
    string? ListId = null);

internal sealed class KlaviyoProfilesDispatcher
    : BatchingChannelDispatcher<KlaviyoProfilesWork>, IKlaviyoProfilesDispatcher
{
    private readonly IKlaviyoProfilesClient _client;
    private readonly IKlaviyoListsClient _listsClient;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoProfilesDispatcher> _logger;

    public KlaviyoProfilesDispatcher(
        IKlaviyoProfilesClient client,
        IKlaviyoListsClient listsClient,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoProfilesDispatcher> logger)
        : base(
            name: "ProfilesDispatcher",
            dispatch: options.Value.Subscriptions.Dispatching,
            logger: logger)
    {
        _client = client;
        _listsClient = listsClient;
        _opt = options.Value;
        _logger = logger;
    }

    public ValueTask EnqueueAsync(KlaviyoProfilesWork work, CancellationToken ct = default)
        => base.EnqueueAsync(work, ct);

    protected override Task<List<KlaviyoProfilesWork>> PrepareBatchAsync(List<KlaviyoProfilesWork> drained, CancellationToken ct)
    {
        if (!_opt.Enabled || !_opt.Subscriptions.Enabled)
            return Task.FromResult(new List<KlaviyoProfilesWork>(0));

        return Task.FromResult(drained);
    }

    protected override async Task HandleChunkAsync(KlaviyoProfilesWork[] chunk, CancellationToken ct)
    {
        if (chunk.Length == 0) return;

        var maxConcurrency = Math.Max(1, _opt.Subscriptions.Dispatching.MaxConcurrency);
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var tasks = chunk.Select(async work =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                switch (work.Type)
                {
                    case KlaviyoProfilesEventType.ProfileUpsert:
                        await _client.UpsertProfileAsync(work.Payload, work.StoreAlias, ct).ConfigureAwait(false);
                        break;

                    case KlaviyoProfilesEventType.AddToList:
                        await _listsClient.AddProfilesToListAsync(work.ListId ?? string.Empty, work.Payload, work.StoreAlias, ct)
                            .ConfigureAwait(false);
                        break;

                    case KlaviyoProfilesEventType.Subscribe:
                        await _client.BulkSubscribeAsync(work.Payload, work.StoreAlias, ct).ConfigureAwait(false);
                        break;

                    case KlaviyoProfilesEventType.Unsubscribe:
                        await _client.BulkUnsubscribeAsync(work.Payload, work.StoreAlias, ct).ConfigureAwait(false);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(work.Type), work.Type, "Unknown subscriptions work type");
                }

                _logger.LogDebug(
                    "Klaviyo ProfilesDispatcher sent {Type} CustomerIdentifier={CustomerIdentifier} OccurredAt={OccurredAt} StoreAlias={StoreAlias} Testing={Testing}.",
                    work.Type, work.CustomerIdentifier, work.OccurredAt, work.StoreAlias, _opt.Testing);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _logger.LogDebug("Klaviyo ProfilesDispatcher sent {Count} profiles jobs in chunk.", chunk.Length);
    }
}
