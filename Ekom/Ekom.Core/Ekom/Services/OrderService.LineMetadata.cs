using Ekom.API;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ekom.Services;

partial class OrderService
{
    public async Task<OrderInfo> UpdateOrderLineMetadataAsync(
        Guid orderId,
        IReadOnlyCollection<OrderLineMetadataUpdate> updates,
        OrderSettings? settings = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order ID cannot be empty.", nameof(orderId));
        }

        if (updates.Count == 0)
        {
            throw new ArgumentException("At least one order line update is required.", nameof(updates));
        }

        settings ??= new OrderSettings();
        if (settings.OrderInfo != null || settings.IsEventHandler)
        {
            throw new ArgumentException("This operation loads the persisted order and cannot use a supplied order or event-handler reentry.", nameof(settings));
        }

        var lineIds = new HashSet<Guid>();
        foreach (var update in updates)
        {
            if (update == null || update.LineId == Guid.Empty || !lineIds.Add(update.LineId)
                || update.Properties == null || update.Properties.Count == 0)
            {
                throw new ArgumentException("Each update must have a unique line ID and at least one property.", nameof(updates));
            }

            var propertyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in update.Properties)
            {
                if (string.IsNullOrWhiteSpace(key)
                    || !key.StartsWith("orderline", StringComparison.OrdinalIgnoreCase)
                    || !propertyKeys.Add(key)
                    || value == null)
                {
                    throw new ArgumentException("Order line properties must have unique 'orderline' keys and non-null values.", nameof(updates));
                }
            }
        }

        var semaphore = _orderLocks.GetOrAdd(orderId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var orderData = await _orderRepository.GetOrderAsync(orderId, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(orderData?.OrderInfo))
            {
                throw new OrderInfoNotFoundException();
            }

            var originalOrderInfo = orderData.OrderInfo;
            var orderJson = JObject.Parse(originalOrderInfo);
            if (orderJson[nameof(OrderInfo.OrderLines)] is not JArray orderLines)
            {
                throw new InvalidOperationException("Order has no order lines to update.");
            }

            var linesById = new Dictionary<Guid, JObject>(orderLines.Count);
            foreach (var token in orderLines)
            {
                if (token is not JObject line
                    || !Guid.TryParse(line[nameof(OrderLine.Key)]?.ToString(), out var lineId)
                    || lineId == Guid.Empty
                    || !linesById.TryAdd(lineId, line))
                {
                    throw new InvalidOperationException("Order contains an invalid or duplicate line ID.");
                }
            }

            foreach (var update in updates)
            {
                if (!linesById.ContainsKey(update.LineId))
                {
                    throw new OrderLineNotFoundException("Could not find order line with key: " + update.LineId);
                }
            }

            foreach (var update in updates)
            {
                var line = linesById[update.LineId];
                if (line[nameof(OrderLine.OrderLineInfo)] is JToken existingInfo
                    && existingInfo.Type is not (JTokenType.Object or JTokenType.Null))
                {
                    throw new InvalidOperationException("Order line metadata is not an object.");
                }

                var lineInfo = line[nameof(OrderLine.OrderLineInfo)] as JObject;
                if (lineInfo == null)
                {
                    lineInfo = new JObject();
                    line[nameof(OrderLine.OrderLineInfo)] = lineInfo;
                }

                if (lineInfo[nameof(OrderLineInfo.Properties)] is JToken existingProperties
                    && existingProperties.Type is not (JTokenType.Object or JTokenType.Null))
                {
                    throw new InvalidOperationException("Order line properties are not an object.");
                }

                var properties = lineInfo[nameof(OrderLineInfo.Properties)] as JObject;
                if (properties == null)
                {
                    properties = new JObject();
                    lineInfo[nameof(OrderLineInfo.Properties)] = properties;
                }

                foreach (var (key, value) in update.Properties)
                {
                    var existing = properties.Properties()
                        .FirstOrDefault(x => x.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.Value = value;
                    }
                    else
                    {
                        properties[key] = value;
                    }
                }
            }

            var updatedOrderInfo = orderJson.ToString(Formatting.None);
            var updateDate = DateTime.Now;
            orderData.OrderInfo = updatedOrderInfo;
            orderData.UpdateDate = updateDate;
            var result = new OrderInfo(orderData);

            if (!await _orderRepository.TryUpdateOrderInfoAsync(orderId, originalOrderInfo, updatedOrderInfo, updateDate, ct)
                .ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Order {orderId} changed during the metadata update. Retry the batch.");
            }

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
            semaphore.Release();
        }
    }
}
