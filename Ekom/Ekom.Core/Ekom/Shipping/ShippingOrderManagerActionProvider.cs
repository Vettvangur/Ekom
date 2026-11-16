using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Models.Shipments;
using Ekom.Services;

namespace Ekom.Shipping;

internal sealed class ShippingOrderManagerActionProvider : IOrderManagerActionProvider
{
    internal const string CreateAction = "ekom-shipping-create";
    internal const string RetryAction = "ekom-shipping-retry";
    internal const string LabelAction = "ekom-shipping-label";
    internal const string ViewAction = "ekom-shipping-view";
    internal const string DeleteAction = "ekom-shipping-delete";
    private readonly IShippingFulfillmentService _fulfillment;
    private readonly IShippingFulfillmentCarrierRegistry _carriers;

    public ShippingOrderManagerActionProvider(
        IShippingFulfillmentService fulfillment,
        IShippingFulfillmentCarrierRegistry carriers)
    {
        _fulfillment = fulfillment;
        _carriers = carriers;
    }

    public async Task<IReadOnlyCollection<OrderManagerAction>> GetActionsAsync(
        IOrderInfo orderInfo,
        CancellationToken ct = default)
    {
        var shippingProvider = orderInfo.ShippingProvider;
        if (shippingProvider is null)
        {
            return Array.Empty<OrderManagerAction>();
        }

        // Without a carrier package there is nothing to book, so the order's shipments are not even read.
        if (_carriers.Carriers.Count == 0)
        {
            return Array.Empty<OrderManagerAction>();
        }

        var record = await _fulfillment.GetAsync(orderInfo.UniqueId, ct).ConfigureAwait(false);
        var configuration = EkomShippingMethodConfiguration.FromProperties(
                shippingProvider.Key,
                shippingProvider.Properties);
        var carrierAlias = record?.CarrierAlias ?? configuration?.CarrierAlias;
        var carrier = _carriers.Carriers.FirstOrDefault(x =>
            string.Equals(x.Alias, carrierAlias, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(carrierAlias) || carrier is null)
        {
            return Array.Empty<OrderManagerAction>();
        }

        return record?.State switch
        {
            ShipmentState.Created =>
                CreatedActions(carrier),
            ShipmentState.Failed =>
            [
                new OrderManagerAction
                {
                    Key = RetryAction,
                    Label = "Retry shipment",
                    Look = "warning",
                    ConfirmMessage = "Retry creating this carrier shipment?",
                    SortOrder = 20,
                },
            ],
            ShipmentState.OutcomeUnknown =>
            [
                new OrderManagerAction
                {
                    Key = RetryAction,
                    Label = "Shipment outcome unknown",
                    Look = "danger",
                    Enabled = false,
                    SortOrder = 20,
                },
            ],
            ShipmentState.Processing or ShipmentState.Submitting =>
            [
                new OrderManagerAction
                {
                    Key = CreateAction,
                    Label = "Creating shipment…",
                    Enabled = false,
                    SortOrder = 20,
                },
            ],
            ShipmentState.Deleting =>
            [
                DisabledAction(DeleteAction, "Deleting shipment…"),
            ],
            // A deleted shipment stays in the history. A new one needs the shipping method to still name a carrier.
            ShipmentState.Deleted when configuration is null =>
            [
                DisabledAction(DeleteAction, "Shipment deleted"),
            ],
            ShipmentState.Deleted =>
            [
                new OrderManagerAction
                {
                    Key = CreateAction,
                    Label = "Create new shipment",
                    Look = "primary",
                    ConfirmMessage = "The previous shipment was deleted. Create a new carrier shipment?",
                    SortOrder = 20,
                },
            ],
            ShipmentState.DeleteOutcomeUnknown =>
            [
                DisabledAction(DeleteAction, "Deletion outcome unknown", "danger"),
            ],
            _ =>
            [
                new OrderManagerAction
                {
                    Key = CreateAction,
                    Label = "Create shipment",
                    Look = "primary",
                    ConfirmMessage = "Create this carrier shipment?",
                    SortOrder = 20,
                },
            ],
        };
    }

    public async Task<OrderManagerActionExecutionResult?> ExecuteAsync(
        IOrderInfo orderInfo,
        string actionKey,
        string? userName = null,
        CancellationToken ct = default)
    {
        try
        {
            switch (actionKey)
            {
                case CreateAction:
                    var created = await _fulfillment.CreateAsync(orderInfo.UniqueId, ct).ConfigureAwait(false);
                    return ResultFor(created);
                case RetryAction:
                    var retried = await _fulfillment.RetryAsync(orderInfo.UniqueId, ct).ConfigureAwait(false);
                    return ResultFor(retried);
                case LabelAction:
                    var label = await _fulfillment.GetLabelAsync(orderInfo.UniqueId, ct).ConfigureAwait(false);
                    return new OrderManagerActionFileResult
                    {
                        Content = label.Content,
                        ContentType = label.ContentType,
                        FileName = label.FileName,
                    };
                case ViewAction:
                    var shipment = await _fulfillment.GetShipmentAsync(orderInfo.UniqueId, ct).ConfigureAwait(false);
                    return new OrderManagerActionFileResult
                    {
                        Content = shipment.Content,
                        ContentType = shipment.ContentType,
                        FileName = shipment.FileName,
                    };
                case DeleteAction:
                    var deleted = await _fulfillment.DeleteAsync(orderInfo.UniqueId, ct).ConfigureAwait(false);
                    return deleted.State == ShipmentState.Deleted
                        ? new OrderManagerActionSuccessResult { Message = "Shipment deleted." }
                        : new OrderManagerActionBadRequestResult
                        {
                            Message = deleted.LastError ?? $"Shipment state: {deleted.State}.",
                        };
                default:
                    return null;
            }
        }
        catch (Exception exception) when (exception is ShippingException or ArgumentException)
        {
            return new OrderManagerActionBadRequestResult
            {
                Message = exception.Message,
            };
        }
    }

    private static OrderManagerActionExecutionResult ResultFor(ShippingFulfillmentRecord record) =>
        record.State == ShipmentState.Created
            ? new OrderManagerActionSuccessResult { Message = "Shipment created." }
            : new OrderManagerActionBadRequestResult
            {
                Message = record.LastError ?? $"Shipment state: {record.State}.",
            };

    private static IReadOnlyCollection<OrderManagerAction> CreatedActions(IShippingFulfillmentCarrier carrier)
    {
        var actions = new List<OrderManagerAction>();
        if (carrier.Capabilities.HasFlag(ShippingCarrierCapabilities.Labels))
        {
            actions.Add(new OrderManagerAction
            {
                Key = LabelAction,
                Label = "Print shipping label",
                Look = "primary",
                SortOrder = 20,
            });
        }

        if (carrier.Capabilities.HasFlag(ShippingCarrierCapabilities.ShipmentLookup))
        {
            actions.Add(new OrderManagerAction
            {
                Key = ViewAction,
                Label = "View shipment JSON",
                SortOrder = 21,
            });
        }

        if (carrier.Capabilities.HasFlag(ShippingCarrierCapabilities.ShipmentDeletion))
        {
            actions.Add(new OrderManagerAction
            {
                Key = DeleteAction,
                Label = "Delete shipment",
                Look = "danger",
                ConfirmMessage = "Delete this shipment from the carrier? This is only possible before collection.",
                SortOrder = 22,
            });
        }

        return actions;
    }

    private static OrderManagerAction DisabledAction(string key, string label, string? look = null) => new()
    {
        Key = key,
        Label = label,
        Look = look ?? string.Empty,
        Enabled = false,
        SortOrder = 20,
    };
}
