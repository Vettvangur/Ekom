using Ekom.API;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using Newtonsoft.Json;

namespace Ekom.Services;

partial class OrderService
{
    public async Task<OrderInfo> AddLinkedOrderLinesAsync(
        Guid productKey,
        decimal quantity,
        string storeAlias,
        IReadOnlyCollection<LinkedOrderLineRequest> linkedProducts,
        AddOrderSettings settings,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(linkedProducts);
        ArgumentNullException.ThrowIfNull(settings);

        if (productKey == Guid.Empty)
        {
            throw new ArgumentException("Empty product key", nameof(productKey));
        }

        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
        }

        if (linkedProducts.Count == 0)
        {
            throw new ArgumentException("At least one linked product is required", nameof(linkedProducts));
        }

        var parent = await ResolveLinkedOrderLineAsync(
            productKey,
            settings.VariantKey,
            quantity,
            storeAlias,
            settings.CustomData,
            ct).ConfigureAwait(false);

        var children = new List<ResolvedLinkedOrderLine>(linkedProducts.Count);
        foreach (LinkedOrderLineRequest? linkedProduct in linkedProducts)
        {
            if (linkedProduct == null)
            {
                throw new ArgumentException("Linked products cannot contain null entries", nameof(linkedProducts));
            }

            children.Add(await ResolveLinkedOrderLineAsync(
                linkedProduct.ProductId,
                linkedProduct.VariantId,
                linkedProduct.Quantity,
                storeAlias,
                linkedProduct.CustomData,
                ct).ConfigureAwait(false));
        }

        IStore? store = _storeSvc.GetStoreByAlias(storeAlias);
        if (store == null)
        {
            throw new ArgumentException("Unable to find store", nameof(storeAlias));
        }

        OrderInfo? currentOrder = settings.OrderInfo as OrderInfo
            ?? await GetOrderAsync(store, ct).ConfigureAwait(false);

        if (currentOrder == null)
        {
            currentOrder = await CreateEmptyOrderAsync(store.Alias, ct).ConfigureAwait(false);
        }

        SemaphoreSlim semaphore = GetOrderLock(currentOrder);
        if (!settings.IsEventHandler)
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
        }

        try
        {
            OrderInfo lockedOrder = currentOrder;
            if (!settings.IsEventHandler)
            {
                OrderData? latestOrderData = await _orderRepository.GetOrderAsync(currentOrder.UniqueId, ct)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(latestOrderData?.OrderInfo))
                {
                    lockedOrder = new OrderInfo(latestOrderData);
                }
            }

            OrderInfo stagedOrder = CloneOrderInfo(lockedOrder);
            ApplyConsentAndTracking(
                stagedOrder,
                settings.Consent,
                settings.Tracking,
                replaceExisting: settings.Tracking?.HasData() == true || settings.Consent != null);

            var parentSettings = CreateLinkedAddSettings(
                settings,
                stagedOrder,
                parent.CustomData,
                Guid.Empty,
                copyDynamicRequest: true);
            (stagedOrder, StagedLinkedOrderLine parentAddition) = await StageNewOrderLineAsync(
                stagedOrder,
                parent,
                parentSettings,
                Guid.Empty,
                ct).ConfigureAwait(false);
            OrderLine parentLine = parentAddition.OrderLine;

            var addedLines = new List<StagedLinkedOrderLine>(children.Count + 1) { parentAddition };
            foreach (ResolvedLinkedOrderLine child in children)
            {
                var childSettings = CreateLinkedAddSettings(
                    settings,
                    stagedOrder,
                    child.CustomData,
                    parentLine.Key,
                    copyDynamicRequest: false);
                (stagedOrder, StagedLinkedOrderLine childAddition) = await StageNewOrderLineAsync(
                    stagedOrder,
                    child,
                    childSettings,
                    parentLine.Key,
                    ct).ConfigureAwait(false);
                addedLines.Add(childAddition);
            }

            if (settings.FireOnOrderUpdatedEvent)
            {
                var updatingArgs = new OrderUpdatingEventArgs { OrderInfo = stagedOrder };
                OrderEvents.OnOrderUpdating(this, updatingArgs);
                await OrderEvents.OnOrderUpdatingAsync(this, updatingArgs, ct).ConfigureAwait(false);
            }

            stagedOrder = await UpdateOrderAndOrderInfoAsync(
                stagedOrder,
                fireOnOrderUpdatedEvents: false,
                ct: ct,
                reservationPersistence: true).ConfigureAwait(false);

            await PublishLinkedOrderLineNotificationsAsync(stagedOrder, addedLines, settings, ct)
                .ConfigureAwait(false);

            return stagedOrder;
        }
        finally
        {
            if (!settings.IsEventHandler)
            {
                semaphore.Release();
            }
        }
    }

    private async Task<ResolvedLinkedOrderLine> ResolveLinkedOrderLineAsync(
        Guid productKey,
        Guid? variantKey,
        decimal quantity,
        string storeAlias,
        IReadOnlyDictionary<string, string>? customData,
        CancellationToken ct)
    {
        if (productKey == Guid.Empty)
        {
            throw new ArgumentException("Empty product key", nameof(productKey));
        }

        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
        }

        IProduct? product = await Catalog.Instance.GetProductAsync(productKey, storeAlias, ct: ct)
            .ConfigureAwait(false);
        if (product == null)
        {
            throw new ProductNotFoundException("Unable to find product with key " + productKey);
        }

        IVariant? variant = null;
        if (variantKey.HasValue && variantKey.Value != Guid.Empty)
        {
            variant = Catalog.Instance.GetVariant(variantKey.Value, storeAlias);
            if (variant == null)
            {
                throw new VariantNotFoundException("Unable to find variant with key " + variantKey);
            }

            if (variant.ProductKey != productKey)
            {
                throw new EkomException("Mismatch between product and variant. Ensure chosen variant is a child of given Product");
            }
        }

        OrderLineVariantValidator.Validate(product, variant);

        if (customData?.Keys.Contains("ekomUpdateInformation", StringComparer.OrdinalIgnoreCase) == true)
        {
            throw new ArgumentException(
                "ekomUpdateInformation is not supported when adding linked products. Update customer information separately.",
                nameof(customData));
        }

        return new ResolvedLinkedOrderLine(
            product,
            variant,
            quantity,
            customData == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(customData, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<(OrderInfo OrderInfo, StagedLinkedOrderLine Addition)> StageNewOrderLineAsync(
        OrderInfo orderInfo,
        ResolvedLinkedOrderLine request,
        AddOrderSettings settings,
        Guid link,
        CancellationToken ct)
    {
        var addingArgs = new AddingOrderlineEventArgs
        {
            Product = request.Product,
            Variant = request.Variant,
            Quantity = request.Quantity,
            Settings = settings,
            Action = OrderAction.New,
            OrderInfo = orderInfo,
        };

        if (settings.FireEvents)
        {
            OrderEvents.OnAddingOrderline(this, addingArgs);
            await OrderEvents.OnAddingOrderlineAsync(this, addingArgs, ct).ConfigureAwait(false);
        }

        IProduct product = addingArgs.Product;
        IVariant? variant = addingArgs.Variant;
        decimal quantity = addingArgs.Quantity;
        settings = addingArgs.Settings as AddOrderSettings
            ?? CreateLinkedAddSettings(
                addingArgs.Settings,
                orderInfo,
                request.CustomData,
                link,
                copyDynamicRequest: true);

        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
        }

        OrderLineVariantValidator.Validate(product, variant);
        if (variant != null && variant.ProductKey != product.Key)
        {
            throw new EkomException("Mismatch between product and variant. Ensure chosen variant is a child of given Product");
        }

        Dictionary<string, string> orderLineData = settings.CustomData
            .Where(x => x.Key.StartsWith("orderline", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

        decimal requestedStock = orderInfo.OrderLines
            .Where(x => x.ProductKey == product.Key && x.VariantKey == variant?.Key)
            .Sum(x => x.Quantity) + quantity;
        VerifyStock(requestedStock, variant?.Stock ?? product.Stock, product, variant);

        OrderDynamicRequest? dynamicRequest = CloneDynamicRequest(settings.OrderDynamicRequest);
        if (link != Guid.Empty || dynamicRequest != null)
        {
            dynamicRequest ??= new OrderDynamicRequest();
            dynamicRequest.OrderLineLink = link;
        }

        var orderLine = new OrderLine(
            product,
            quantity,
            Guid.NewGuid(),
            orderInfo,
            orderLineData,
            variant,
            dynamicRequest);
        orderInfo.orderLines.Add(orderLine);

        await ApplyLinkedOrderLineDiscountAsync(product, orderLine, orderInfo, ct)
            .ConfigureAwait(false);

        return (orderInfo, new StagedLinkedOrderLine(orderLine, settings));
    }

    private async Task ApplyLinkedOrderLineDiscountAsync(
        IProduct product,
        OrderLine orderLine,
        OrderInfo orderInfo,
        CancellationToken ct)
    {
        var productDiscount = await product.ProductDiscountAsync(ct: ct).ConfigureAwait(false);
        if (productDiscount == null || (orderInfo.Discount != null && !orderInfo.Discount.Stackable))
        {
            return;
        }

        await ApplyDiscountToOrderLineAsync(
            orderLine,
            productDiscount,
            orderInfo,
            new DiscountOrderSettings { UpdateOrder = false },
            ct).ConfigureAwait(false);
    }

    private async Task PublishLinkedOrderLineNotificationsAsync(
        OrderInfo orderInfo,
        IReadOnlyCollection<StagedLinkedOrderLine> addedLines,
        AddOrderSettings settings,
        CancellationToken ct)
    {
        var notifications = new List<Func<Task>>();
        if (settings.FireEvents)
        {
            foreach (StagedLinkedOrderLine addition in addedLines)
            {
                notifications.Add(() =>
                {
                    OrderEvents.OnAddedOrderline(this, new AddedOrderlineEventArgs
                    {
                        OrderInfo = orderInfo,
                        OrderLine = addition.OrderLine,
                        Settings = addition.Settings,
                    });
                    return Task.CompletedTask;
                });
                notifications.Add(() => OrderEvents.OnAddedOrderlineAsync(
                    this,
                    new AddedOrderlineEventArgs
                    {
                        OrderInfo = orderInfo,
                        OrderLine = addition.OrderLine,
                        Settings = addition.Settings,
                    },
                    ct));
                notifications.Add(() =>
                {
                    OrderEvents.OnUpdatedOrderline(this, new UpdatedOrderlineEventArgs { OrderInfo = orderInfo });
                    return Task.CompletedTask;
                });
                notifications.Add(() => OrderEvents.OnUpdatedOrderlineAsync(
                    this,
                    new UpdatedOrderlineEventArgs { OrderInfo = orderInfo },
                    ct));
            }
        }

        if (settings.FireOnOrderUpdatedEvent)
        {
            notifications.Add(() =>
            {
                OrderEvents.OnOrderUpdated(this, new OrderUpdatedEventArgs { OrderInfo = orderInfo });
                return Task.CompletedTask;
            });
            notifications.Add(() => OrderEvents.OnOrderUpdatedAsync(
                this,
                new OrderUpdatedEventArgs { OrderInfo = orderInfo },
                ct));
        }

        foreach (StagedLinkedOrderLine addition in addedLines)
        {
            notifications.Add(() => _orderActivityLogService.AddOrderLogAsync(
                orderInfo.UniqueId,
                $"Order line added. Product: {addition.OrderLine.Product.Title} - {addition.OrderLine.Product.SKU} {(addition.OrderLine.Variant != null ? " Variant: " + addition.OrderLine.Variant.SKU : "")}",
                ct: ct));
        }

        await OrderPersistenceNotifications.RunAsync(orderInfo.UniqueId, _logger, notifications.ToArray())
            .ConfigureAwait(false);
    }

    private static AddOrderSettings CreateLinkedAddSettings(
        OrderSettings source,
        OrderInfo orderInfo,
        IReadOnlyDictionary<string, string> customData,
        Guid link,
        bool copyDynamicRequest)
    {
        OrderDynamicRequest? dynamicRequest = copyDynamicRequest
            ? CloneDynamicRequest(source.OrderDynamicRequest)
            : null;
        if (link != Guid.Empty || dynamicRequest != null)
        {
            dynamicRequest ??= new OrderDynamicRequest();
            dynamicRequest.OrderLineLink = link;
        }

        return new AddOrderSettings
        {
            FireEvents = source.FireEvents,
            FireOnOrderUpdatedEvent = source.FireOnOrderUpdatedEvent,
            IsEventHandler = source.IsEventHandler,
            OrderInfo = orderInfo,
            OrderDynamicRequest = dynamicRequest,
            CustomData = new Dictionary<string, string>(customData, StringComparer.OrdinalIgnoreCase),
            AlgoliaQueryId = source.AlgoliaQueryId,
            Consent = source.Consent,
            Tracking = source.Tracking,
            OrderAction = OrderAction.New,
        };
    }

    private static OrderDynamicRequest? CloneDynamicRequest(OrderDynamicRequest? source)
    {
        if (source == null)
        {
            return null;
        }

        return new OrderDynamicRequest
        {
            OrderLineLink = source.OrderLineLink,
            Prices = source.Prices,
            VariantPrices = source.VariantPrices,
            Title = source.Title,
            SKU = source.SKU,
            Type = source.Type,
            CountToTotal = source.CountToTotal,
        };
    }

    private static OrderInfo CloneOrderInfo(OrderInfo source)
    {
        string serialized = JsonConvert.SerializeObject(source, EkomJsonDotNet.Settings);
        OrderData orderData = source.OrderDataClone();
        orderData.OrderInfo = serialized;
        return new OrderInfo(orderData);
    }

    private sealed record ResolvedLinkedOrderLine(
        IProduct Product,
        IVariant? Variant,
        decimal Quantity,
        IReadOnlyDictionary<string, string> CustomData);

    private sealed record StagedLinkedOrderLine(
        OrderLine OrderLine,
        AddOrderSettings Settings);
}
