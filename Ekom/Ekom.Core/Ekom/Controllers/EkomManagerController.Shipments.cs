using Ekom.Authorization;
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Models.Shipments;
using Ekom.Services;
using Ekom.Shipping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ekom.Controllers;

public partial class EkomManagerController
{
    /// <summary>The order's shipments: the current shipment and every earlier one, with what happened to each.</summary>
    [HttpGet("orderShipments/{orderId:guid}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GetOrderShipmentsAsync(Guid orderId, [FromServices] IOrderShipments shipments, CancellationToken ct = default)
    {
        try
        {
            var orderData = await _repo.GetOrderAsync(orderId, ct);
            if (orderData == null)
            {
                return NotFound();
            }

            if (!CanAccessStore(orderData.StoreAlias))
            {
                return ForbidStore(orderData.StoreAlias);
            }

            var order = await _repo.GetOrderInfoAsync(orderId, ct).ConfigureAwait(false);
            return Ok(order == null
                ? await shipments.GetForOrderAsync(orderId, ct).ConfigureAwait(false)
                : await LoadOrderShipmentsAsync(order, shipments, ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _logger.LogError(ex, "Failed to get order shipments. {OrderId}", orderId);

            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Sets the tracking number of the order's shipment. An order without a shipment, such as one sent with
    /// a courier Ekom does not book, gets a shipment entered by hand.
    /// </summary>
    [HttpPost("Order/{orderId:guid}/Shipment/Tracking")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> UpdateOrderShipmentTrackingAsync(Guid orderId,
        [FromBody] OrderShipmentTrackingUpdateRequest? request, [FromServices] IOrderShipments shipments,
        CancellationToken ct = default)
    {
        if (orderId == Guid.Empty || request == null)
        {
            return BadRequest("A tracking number is required.");
        }

        var trackingNumber = request.TrackingNumber?.Trim() ?? string.Empty;
        var trackingUrl = request.TrackingUrl?.Trim() ?? string.Empty;
        if (trackingNumber.Length > 200)
        {
            return BadRequest("The tracking number is too long.");
        }

        if (trackingUrl.Length > 0 && !IsWebAddress(trackingUrl))
        {
            return BadRequest("The tracking link must be an http or https address.");
        }

        try
        {
            var orderData = await _repo.GetOrderAsync(orderId, ct);
            if (orderData == null)
            {
                return NotFound();
            }

            if (!CanAccessStore(orderData.StoreAlias))
            {
                return ForbidStore(orderData.StoreAlias);
            }

            var order = await _repo.GetOrderInfoAsync(orderId, ct).ConfigureAwait(false);
            if (order == null)
            {
                return NotFound();
            }

            // An order whose shipment still lives on its shipping custom data is copied over first.
            var current = (await LoadOrderShipmentsAsync(order, shipments, ct).ConfigureAwait(false)).Current;
            var userName = HttpContext?.RequestServices.GetService(typeof(ISecurityService)) is ISecurityService security
                && !string.IsNullOrWhiteSpace(security.GetAuthenticatedUserName())
                    ? security.GetAuthenticatedUserName()
                    : HttpContext?.User?.Identity?.Name;
            var actor = string.IsNullOrWhiteSpace(userName) ? "Manager" : userName;

            if (current == null)
            {
                if (trackingNumber.Length == 0)
                {
                    return BadRequest("A tracking number is required.");
                }

                await shipments.CreateAsync(new NewOrderShipment
                {
                    OrderUniqueId = orderId,
                    OrderNumber = order.OrderNumber,
                    StoreAlias = orderData.StoreAlias,
                    State = ShipmentState.Created,
                    ProviderKey = order.ShippingProvider?.Key,
                    ProviderName = order.ShippingProvider?.Title,
                    TrackingNumber = trackingNumber,
                    TrackingUrl = trackingUrl.Length == 0 ? null : trackingUrl,
                    Message = "Entered by hand.",
                    Actor = actor,
                }, ct).ConfigureAwait(false);
            }
            else
            {
                await shipments.RecordAsync(new ShipmentUpdate
                {
                    ShipmentId = current.ShipmentId,
                    Type = ShipmentEventType.Manual,
                    // An empty value clears what the shipment has.
                    TrackingNumber = trackingNumber,
                    TrackingUrl = trackingUrl,
                    Actor = actor,
                }, ct).ConfigureAwait(false);
            }

            return Ok(await shipments.GetForOrderAsync(orderId, ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _logger.LogError(ex, "Failed to update the shipment tracking number. {OrderId}", orderId);

            return StatusCode(500, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// The order's shipments. An order whose shipment was stored on its shipping custom data, before Ekom
    /// kept shipment records, has it copied over the first time the order is opened.
    /// </summary>
    internal async Task<OrderShipmentsView> LoadOrderShipmentsAsync(IOrderInfo order, IOrderShipments shipments, CancellationToken ct)
    {
        var view = await shipments.GetForOrderAsync(order.UniqueId, ct).ConfigureAwait(false);
        if (view.Shipments.Count > 0)
        {
            return view;
        }

        NewOrderShipment? legacy;
        try
        {
            legacy = ShippingFulfillmentService.ReadLegacyShipment(order);
        }
        catch (ShippingException ex)
        {
            // A shipping method that lost part of its carrier settings: the order still opens.
            _logger.LogWarning(ex, "Could not read the shipment stored on the order's shipping custom data. {OrderId}", order.UniqueId);
            return view;
        }

        if (legacy == null)
        {
            return view;
        }

        await shipments.CreateAsync(legacy, ct).ConfigureAwait(false);
        return await shipments.GetForOrderAsync(order.UniqueId, ct).ConfigureAwait(false);
    }

    private static bool IsWebAddress(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
