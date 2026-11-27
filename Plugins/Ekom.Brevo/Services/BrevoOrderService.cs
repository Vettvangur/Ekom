using Ekom.Brevo.Dispatching;
using Ekom.Brevo.Mappers;
using Ekom.Brevo.Models;
using Ekom.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;

namespace Ekom.Brevo.Services;

internal sealed class BrevoOrderService(BrevoConfiguration configuration, BrevoMarketingDispatcher dispatcher)
    : IBrevoOrderService, IDisposable
{
    private readonly MemoryCache _submitted = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly object _gate = new();

    public ValueTask<BrevoSubmission> TrackOrderAsync(IOrderInfo order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        cancellationToken.ThrowIfCancellationRequested();
        string alias = order.StoreInfo.Alias;
        string id = BrevoOrderMapper.ScopedId(alias, order.UniqueId);
        if (!configuration.Enabled(alias) || !configuration.Options.Orders.Enabled)
            return ValueTask.FromResult(new BrevoSubmission(BrevoSubmissionStatus.Disabled, id));
        var key = configuration.ApiKey(alias);
        if (string.IsNullOrWhiteSpace(key))
            return ValueTask.FromResult(new BrevoSubmission(BrevoSubmissionStatus.ConfigurationMissing, id));
        string payload = order.ToBrevoOrder().ToJsonString();
        lock (_gate)
        {
            if (_submitted.TryGetValue(id, out OrderSubmission? previous))
            {
                // Definitive rejection is safe for an explicit caller retry. Unknown outcomes remain suppressed.
                if (previous?.Outcome is BrevoDeliveryStatus.Failed or BrevoDeliveryStatus.Cancelled)
                    _submitted.Remove(id);
                else
                    return ValueTask.FromResult(new BrevoSubmission(BrevoSubmissionStatus.Duplicate, id, previous?.Completion));
            }
            var submission = dispatcher.Enqueue(alias, key, HttpMethod.Post, "orders/status", payload, false, id);
            if (submission.Status == BrevoSubmissionStatus.Queued && submission.Completion is { } completion)
            {
                var entry = new OrderSubmission(completion);
                _submitted.Set(id, entry, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });
                submission = submission with { Completion = entry.Completion };
            }
            return ValueTask.FromResult(submission);
        }
    }

    private sealed class OrderSubmission
    {
        private BrevoDeliveryResult? _result;
        public Task<BrevoDeliveryResult> Completion { get; }
        public BrevoDeliveryStatus? Outcome => Volatile.Read(ref _result)?.Status;

        public OrderSubmission(Task<BrevoDeliveryResult> completion)
        {
            Completion = ObserveAsync(completion);
        }

        private async Task<BrevoDeliveryResult> ObserveAsync(Task<BrevoDeliveryResult> completion)
        {
            var result = await completion.ConfigureAwait(false);
            Volatile.Write(ref _result, result);
            return result;
        }
    }

    public void Dispose() => _submitted.Dispose();
}
