using Ekom.API;
using Ekom.Events;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;

namespace Ekom.Services;

partial class OrderService
{
    private async Task<OrderInfo> RefreshOrderLinePricingAsync(OrderInfo orderInfo, OrderLine line,
        string storeAlias, OrderSettings settings, CancellationToken ct)
    {
        IProduct? product = await Catalog.Instance.GetProductAsync(line.ProductKey, storeAlias, ct: ct)
            .ConfigureAwait(false);
        IVariant? variant = line.VariantKey is Guid variantKey
            ? await Catalog.Instance.GetVariantAsync(variantKey, storeAlias, ct).ConfigureAwait(false)
            : null;

        if (product == null || (line.VariantKey.HasValue && variant == null)
            || (variant != null && variant.ProductKey != product.Key)
            || (variant == null && product.AllVariants.Any()))
        {
            _logger.LogWarning(
                "Unable to refresh pricing for order line {OrderLineId}. Keeping its existing data. Product: {ProductKey}; Variant: {VariantKey}; Order: {OrderUniqueId}.",
                line.Key, line.ProductKey, line.VariantKey, orderInfo.UniqueId);
            return orderInfo;
        }

        var addingArgs = new AddingOrderlineEventArgs
        {
            OrderInfo = orderInfo,
            Product = product,
            Variant = variant,
            Quantity = line.Quantity,
            Action = OrderAction.Set,
            Settings = CreateReinitializeLineSettings(orderInfo, line, settings),
        };

        if (settings.FireEvents)
        {
            OrderEvents.OnAddingOrderline(this, addingArgs);
            await OrderEvents.OnAddingOrderlineAsync(this, addingArgs, ct).ConfigureAwait(false);
        }

        product = addingArgs.Product;
        variant = addingArgs.Variant;
        if (product.Key != line.ProductKey || variant?.Key != line.VariantKey
            || (variant != null && variant.ProductKey != product.Key))
        {
            throw new InvalidOperationException("Pricing refresh cannot change an order line's product or variant.");
        }
        OrderLineVariantValidator.Validate(product, variant);

        // Keep line identity, quantity and settings. Only the product/variant price snapshot is rebuilt.
        var dynamicRequest = CreateReinitializeDynamicRequest(line, addingArgs.Settings.OrderDynamicRequest);
        line.Product = new OrderedProduct(product, variant, orderInfo.StoreInfo, dynamicRequest);
        // The legacy line model annotates these as non-nullable, but no-discount/no-coupon is valid.
        line.Discount = (line.Product.DisableDiscounts ? null : line.Variant?.Price.Discount ?? line.Product.Price.Discount)!;
        if (line.Product.DisableDiscounts)
        {
            line.Coupon = null!;
        }

        foreach (var property in addingArgs.Settings.CustomData)
        {
            if (property.Key.StartsWith("orderline", StringComparison.OrdinalIgnoreCase))
            {
                line.OrderLineInfo.Properties[property.Key] = property.Value;
            }
        }

        await ApplyLinkedOrderLineDiscountAsync(product, line, orderInfo, ct).ConfigureAwait(false);
        line.InvalidateAmount();

        if (settings.FireEvents)
        {
            var addedArgs = new AddedOrderlineEventArgs
            {
                OrderInfo = orderInfo,
                OrderLine = line,
                Settings = addingArgs.Settings,
            };
            OrderEvents.OnAddedOrderline(this, addedArgs);
            await OrderEvents.OnAddedOrderlineAsync(this, addedArgs, ct).ConfigureAwait(false);

            var updatedArgs = new UpdatedOrderlineEventArgs { OrderInfo = addedArgs.OrderInfo };
            OrderEvents.OnUpdatedOrderline(this, updatedArgs);
            await OrderEvents.OnUpdatedOrderlineAsync(this, updatedArgs, ct).ConfigureAwait(false);
            return updatedArgs.OrderInfo;
        }
        return orderInfo;
    }

    private static AddOrderSettings CreateReinitializeLineSettings(OrderInfo orderInfo, OrderLine line, OrderSettings settings)
    {
        var customData = new Dictionary<string, string>(line.OrderLineInfo.Properties, StringComparer.OrdinalIgnoreCase);
        foreach (var property in settings.CustomData)
        {
            customData[property.Key] = property.Value;
        }

        return new AddOrderSettings
        {
            OrderInfo = orderInfo,
            OrderAction = OrderAction.Set,
            VariantKey = line.VariantKey,
            IsEventHandler = true, // The refresh already owns the order lock.
            FireEvents = settings.FireEvents,
            FireOnOrderUpdatedEvent = settings.FireOnOrderUpdatedEvent,
            CustomData = customData,
            AlgoliaQueryId = settings.AlgoliaQueryId,
            Consent = settings.Consent,
            Tracking = settings.Tracking,
            OrderDynamicRequest = CreateReinitializeDynamicRequest(line, null),
        };
    }

    private static OrderDynamicRequest CreateReinitializeDynamicRequest(OrderLine line, OrderDynamicRequest? overrides)
    {
        // Identity metadata belongs to the line, whereas its old price overrides must not be replayed.
        line.Product.Properties.TryGetValue("dynamicType", out var type);
        var request = CloneDynamicRequest(overrides) ?? new OrderDynamicRequest();
        request.Title ??= line.Product.Title;
        request.SKU ??= line.Product.SKU;
        request.Type ??= type!;
        request.OrderLineLink = line.Settings?.Link ?? Guid.Empty;
        request.CountToTotal = line.Settings?.CountToTotal ?? true;
        return request;
    }

    private static void ValidateReinitializedOrder(OrderInfo original, OrderInfo refreshed,
        IReadOnlyCollection<ReinitializeLineState> originalLines)
    {
        if (refreshed.UniqueId != original.UniqueId
            || !string.Equals(refreshed.StoreInfo.Alias, original.StoreInfo.Alias, StringComparison.Ordinal)
            || refreshed.orderLines.Count != originalLines.Count)
        {
            throw new InvalidOperationException("Pricing refresh must preserve the basket and its existing lines.");
        }

        var linesByKey = refreshed.orderLines.ToDictionary(line => line.Key);
        foreach (var expected in originalLines)
        {
            if (!linesByKey.TryGetValue(expected.Key, out var line)
                || line.ProductKey != expected.ProductKey || line.VariantKey != expected.VariantKey
                || line.Quantity != expected.Quantity
                || (line.Settings?.Link ?? Guid.Empty) != expected.Link
                || (line.Settings?.CountToTotal ?? true) != expected.CountToTotal
                || expected.PropertyKeys.Any(key => !line.OrderLineInfo.Properties.ContainsKey(key)))
            {
                throw new InvalidOperationException("Pricing refresh must preserve line identity, quantity, metadata and links.");
            }
        }
    }

    private sealed record ReinitializeLineState
    {
        public ReinitializeLineState(OrderLine line)
        {
            Key = line.Key;
            ProductKey = line.ProductKey;
            VariantKey = line.VariantKey;
            Quantity = line.Quantity;
            Link = line.Settings?.Link ?? Guid.Empty;
            CountToTotal = line.Settings?.CountToTotal ?? true;
            PropertyKeys = line.OrderLineInfo.Properties.Keys.ToArray();
        }

        public Guid Key { get; }
        public Guid ProductKey { get; }
        public Guid? VariantKey { get; }
        public decimal Quantity { get; }
        public Guid Link { get; }
        public bool CountToTotal { get; }
        public string[] PropertyKeys { get; }
    }
}
