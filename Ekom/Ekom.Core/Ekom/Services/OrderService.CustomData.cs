using Ekom.API;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ekom.Services;

partial class OrderService
{
    public Task<OrderInfo> UpdateCustomDataAsync(Guid orderId, IReadOnlyDictionary<string, string> updates,
        OrderSettings? settings = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        var validated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in updates) OrderCustomData.Add(validated, pair.Key, pair.Value);
        return ChangeCustomDataAsync(orderId, validated, [], settings, ct);
    }

    public Task<OrderInfo> RemoveCustomDataAsync(Guid orderId, IReadOnlyCollection<string> keys,
        OrderSettings? settings = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var validated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys) OrderCustomData.Add(validated, key, string.Empty);
        return ChangeCustomDataAsync(orderId, new Dictionary<string, string>(), validated.Keys.ToArray(), settings, ct);
    }

    private async Task<OrderInfo> ChangeCustomDataAsync(Guid orderId, IReadOnlyDictionary<string, string> updates,
        IReadOnlyCollection<string> removals, OrderSettings? settings, CancellationToken ct)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("Order ID cannot be empty.", nameof(orderId));
        settings ??= new OrderSettings();
        if (settings.OrderInfo != null || settings.IsEventHandler)
            throw new ArgumentException("This operation loads the persisted order and cannot use a supplied order or event-handler reentry.", nameof(settings));

        var semaphore = _orderLocks.Get(orderId);
        var ownsSemaphore = CheckoutPaymentOperationScope.Current?.OrderId != orderId;
        if (ownsSemaphore) await semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var data = await _orderRepository.GetOrderAsync(orderId, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(data?.OrderInfo)) throw new OrderInfoNotFoundException();
            await using var operation = await BeginOrderInformationUpdateAsync(new OrderInfo(data), ct).ConfigureAwait(false);
            using var capability = operation?.Enter();
            data = await _orderRepository.GetOrderAsync(orderId, ct).ConfigureAwait(false)
                ?? throw new OrderInfoNotFoundException();
            var original = data.OrderInfo;
            var json = JObject.Parse(original);
            var order = new OrderInfo(data);
            OrderCustomData.Merge(order.CustomData, updates);
            foreach (var key in removals) order.CustomData.Remove(key);
            json[nameof(OrderInfo.CustomData)] = JObject.FromObject(order.CustomData);
            data.OrderInfo = json.ToString(Formatting.None);
            data.UpdateDate = DateTime.Now;
            var result = new OrderInfo(data);
            if (!await _orderRepository.TryUpdateOrderInfoAsync(orderId, original, data.OrderInfo, data.UpdateDate, ct, data.OrderStatusCol)
                .ConfigureAwait(false))
                throw new InvalidOperationException($"Order {orderId} changed during the metadata update. Retry the update.");

            var notifications = new List<Func<Task>>
            {
                () => { _memoryCache.Set(orderId.ToString(), result, Configuration.orderInfoCacheTime); return Task.CompletedTask; }
            };
            if (settings.FireOnOrderUpdatedEvent)
            {
                var args = new OrderUpdatedEventArgs { OrderInfo = result };
                notifications.Add(() => { OrderEvents.OnOrderUpdated(this, args); return Task.CompletedTask; });
                notifications.Add(() => OrderEvents.OnOrderUpdatedAsync(this, args, ct));
            }
            await OrderPersistenceNotifications.RunAsync(orderId, _logger, notifications.ToArray()).ConfigureAwait(false);
            return result;
        }
        finally
        {
            if (ownsSemaphore) semaphore.Release();
        }
    }
}
