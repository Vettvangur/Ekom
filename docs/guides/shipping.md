[Ekom docs](../README.md) / [Guides](README.md) / Shipping and shipments

# Shipping and shipments

Ekom keeps a record of each shipment of an order: who carries it, its tracking number, and everything that happened to it. Carriers plug into this record, and the Manager shows it to staff. Read this page when you connect a carrier, record shipments from a warehouse system, or send tracking numbers to customers.

A store that ships with a courier it books by hand needs nothing but Ekom: staff enter the tracking number on the order in the Manager. Each carrier that Ekom books with is one extra package, for example `Ekom.Shipping.Dropp`. To set up the delivery options a customer can choose, see [Payment and shipping providers](providers.md). For the checkout flow, see [Checkout](checkout.md).

On this page:

- [Names used on this page](#names-used-on-this-page)
- [What Ekom stores](#what-ekom-stores)
- [Shipment states](#shipment-states)
- [Where the customer's carrier choice is saved](#where-the-customers-carrier-choice-is-saved)
- [Read shipments](#read-shipments)
- [Record shipments from your own system](#record-shipments-from-your-own-system)
- [Connect a carrier](#connect-a-carrier)
- [React to shipment changes](#react-to-shipment-changes)
- [Find shipment changes in the activity log](#find-shipment-changes-in-the-activity-log)
- [See shipments in the Manager](#see-shipments-in-the-manager)
- [Moving from the Ekom.Shipping packages](#moving-from-the-ekomshipping-packages)

## Names used on this page

| Name | What it is | Where it comes from |
| --- | --- | --- |
| Order ID | The order's GUID. | `OrderData.UniqueId` |
| Shipment ID | One shipment of an order, in Ekom. An order can have several. | `OrderShipment.ShipmentId` |
| Carrier shipment ID | The carrier's own ID for the shipment. | The carrier's booking response |
| Tracking number | What the recipient and staff follow the shipment with. | The carrier's booking response, or entered by staff |
| Shipment reference | The booking reference, such as a barcode reserved before the shipment was sent to the carrier. | The carrier |

## What Ekom stores

Ekom creates two tables at startup.

`EkomOrderShipments` has one row per shipment. Each value staff work with has its own column:

| Group | Columns |
| --- | --- |
| Identity | `ShipmentId`, `OrderUniqueId`, `OrderNumber`, `StoreAlias`, `Kind` (`Outbound`, `Return` or `ExtraParcel`) |
| Method | `ProviderKey`, `ProviderName`, `CarrierAlias`, `AccountReference`, `ServiceId`, `ServiceName` |
| Carrier | `TrackingNumber`, `TrackingUrl`, `CarrierShipmentId`, `ShipmentReference` |
| State | `State`, `CarrierStatus`, `CarrierStatusText`, `Attempts`, `LastError` |
| Pickup point | `PickupLocationId`, `PickupLocationName`, `PickupLocationAddress`, `PickupLocationPostalCode`, `PickupLocationCity` |
| Money | `Amount` and `Currency`: what the customer paid for shipping |
| Dates | `CreatedUtc`, `UpdatedUtc`, `ShippedUtc`, `DeliveredUtc`, `EstimatedDeliveryUtc` |
| Carrier extras | `Documents` (references to stored labels, never the labels themselves) and `Details` |

`Details` is a list of labelled values the carrier wants staff to see, such as a number of parcels. They are stored with the shipment, so the Manager can show them without knowing the carrier.

`EkomOrderShipmentEvents` has one row for each thing that happened to a shipment. Rows are only added, never changed, so earlier attempts, failures and deleted shipments stay visible.

| Type | Meaning |
| --- | --- |
| `State` | The shipment moved to another state. For a failure, the row says why. |
| `Tracking` | The carrier reported progress, such as "out for delivery", with its status code and location. |
| `Manual` | A Manager user changed the shipment, such as its tracking number. |
| `Note` | Information that changes nothing. |

## Shipment states

| State | Meaning |
| --- | --- |
| `Pending` | The default state of a new shipment record when no other state is given. |
| `Processing` | Ekom is preparing the booking. |
| `Submitting` | The booking was sent to the carrier. |
| `Created` | The carrier created the shipment. |
| `Failed` | The carrier refused the booking, or Ekom could not prepare it. It can be retried. |
| `OutcomeUnknown` | The carrier may or may not have created the shipment, for example after a dropped connection. Ekom never retries it by itself: check with the carrier. |
| `Deleting`, `Deleted` | The shipment is being deleted, or was deleted, at the carrier. |
| `DeleteOutcomeUnknown` | The carrier may or may not have deleted the shipment. |

A deleted shipment stays in the order's history, and the order can get a new shipment.

A failed shipment is retried on the same shipment record. The retry keeps a booking reference that was already reserved and adds one to `Attempts`. A retry is always an explicit action: Ekom does not book again after `Failed` on its own.

A booking that has been `Submitting` for more than 10 minutes is treated as `OutcomeUnknown` the next time someone tries to book it. Booking a shipment that is already `Created` returns the existing shipment and books nothing new.

## Where the customer's carrier choice is saved

The carrier service and pickup point a customer chooses belong to the basket, before any shipment exists. `IEkomShippingCheckoutService.SaveSelectionAsync` checks the choice and saves it on the order's shipping provider as `customshipping...` values, for example `customshippingServiceId` and `customshippingPickupLocationId`. When a shipment is booked, Ekom copies the service name and the pickup point to the shipment.

Ekom does not write anything about the shipment itself to the order.

## Read shipments

Inject `Ekom.Services.IOrderShipments`.

```csharp
using Ekom.Services;

public sealed class WarehouseExport(IOrderShipments shipments)
{
    public async Task<string?> TrackingNumberAsync(Guid orderId, CancellationToken ct)
    {
        OrderShipmentsView view = await shipments.GetForOrderAsync(orderId, ct);
        return view.Current?.TrackingNumber;
    }
}
```

| Method | Returns |
| --- | --- |
| `GetForOrderAsync(orderId)` | The current shipment and every shipment of the order, newest first, each with its events. |
| `GetCurrentAsync(orderId)` | The latest outbound shipment that is not deleted. When all are deleted, the latest outbound shipment. `null` when there is none. |
| `GetCurrentShipmentsAsync(orderIds)` | The current shipment of many orders, loaded together. Orders without a shipment are left out. |
| `GetAsync(shipmentId)` | One shipment. |
| `FindOrderAsync(text)` | The order of a tracking number, a carrier shipment ID or a shipment reference. |

## Record shipments from your own system

The shipping framework records the shipments it books. Code that ships another way, for example a warehouse system that prints its own labels, records its shipments too:

```csharp
using Ekom.Models.Shipments;
using Ekom.Services;

OrderShipment shipment = await shipments.CreateAsync(new NewOrderShipment
{
    OrderUniqueId = order.UniqueId,
    OrderNumber = order.OrderNumber,
    StoreAlias = order.StoreInfo.Alias,
    State = ShipmentState.Created,
    ProviderName = "Warehouse courier",
    TrackingNumber = "AB123456789IS",
    TrackingUrl = "https://courier.example/track/AB123456789IS",
}, ct);

await shipments.RecordAsync(new ShipmentUpdate
{
    ShipmentId = shipment.ShipmentId,
    Type = ShipmentEventType.Tracking,
    CarrierStatus = "delivered",
    DeliveredUtc = deliveredAt,
    // The same update reported twice is stored once.
    IdempotencyKey = $"{shipment.ShipmentId:N}:delivered",
}, ct);
```

`CreateAsync` throws `ArgumentException` when the order ID is empty. `RecordAsync` throws `InvalidOperationException` when the shipment ID does not exist.

How `RecordAsync` applies a `ShipmentUpdate`:

- A text value left `null` keeps what the shipment has. An empty string clears it.
- `Details` replaces the shipment's details. An empty list clears them.
- `Error` is saved as the shipment's `LastError` whenever `State` is set. A state change without an error clears `LastError`.
- When you leave `Type` out, the event is a `State` event if `State` is set, otherwise a `Note`.
- With an `IdempotencyKey` that was already used, nothing is stored and the result has `Recorded = false`. Without a key, every update is stored. Set a key when a webhook or a tracking poll can report the same update more than once.

## Connect a carrier

A carrier package implements two interfaces in the `Ekom.Shipping` namespace:

- `IShippingCarrier` for checkout: the services the carrier offers and its pickup points.
- `IShippingFulfillmentCarrier` for booking: reserving a booking reference, creating the shipment, labels and tracking.

A carrier never stores anything itself. It returns a `ShipmentBookingResult`, and Ekom records it.

```csharp
return new ShipmentBookingResult(carrierShipmentId, bookingReference, trackingNumber)
{
    TrackingUrl = ShippingTrackingLink.Format(account.TrackingUrlTemplate, trackingNumber),
    ServiceName = "Express Worldwide",
    Details = [new ShipmentDetail("pieces", "Pieces", "2")],
};
```

`ShippingTrackingLink.Format` puts the tracking number in place of `{trackingNumber}` in the template. It returns `null` when the template or the number is missing, or when the result is not an `http` or `https` address.

Register the carrier in the site's startup with the method its package provides, for example `AddDroppShipping`. Ekom registers the shipping framework itself.

A shipping provider node names its carrier with three properties: `shippingCarrierAlias`, `shippingCarrierAccount` and `shippingCarrierService`.

### Book shipments

`IShippingFulfillmentService` books, retries, prints and deletes the carrier shipment of an order: `CreateAsync`, `RetryAsync`, `GetLabelAsync`, `GetShipmentAsync` and `DeleteAsync`. These methods throw `ShippingException` when the action is not possible in the shipment's state, for example retrying a shipment that did not fail.

When a carrier account is in `Automatic` mode, Ekom books the shipment when checkout completes. It does this from a `CheckoutEvents.CompleteCheckoutAsync` handler. Ekom skips automatic booking when:

- the order has no shipping provider,
- no booking carrier is registered,
- the shipping provider node has no carrier set,
- the carrier account is not in `Automatic` mode,
- any registered `IShippingAutomationRule` returns `false` from `CanCreateShipmentAsync`.

A shipment that cannot be booked never fails the checkout. Ekom logs the error, and the shipment shows its state in the Manager.

The handler runs before completion takes stock. If completion fails after it, for example because stock ran out, the shipment can already be booked for an order that did not complete. Check the shipment when you handle a `completion-stock-failed` or `completion-failed` [order review](order-reviews.md).

## React to shipment changes

`OrderEvents.ShipmentRecordedAsync` is raised after a shipment is created or an update is stored. The arguments hold `Shipment`, `Event`, and `PreviousState`, which is `null` for a new shipment.

```csharp
using Ekom.Events;
using Ekom.Models.Shipments;

OrderEvents.ShipmentRecordedAsync += async (sender, args, ct) =>
{
    if (args.Event.Type == ShipmentEventType.State && args.Shipment.State == ShipmentState.Created)
    {
        await NotifyCustomerAsync(args.Shipment.OrderUniqueId, args.Shipment.TrackingNumber, ct);
    }
};
```

The event is not raised for an update with an idempotency key that was already used, or for a shipment copied from an older order's custom data. If your handler throws, the update stays stored and the exception reaches the code that recorded it.

## Find shipment changes in the activity log

Ekom writes the activity log entry for a shipment in the same database transaction as the event. The steps on the way to the carrier (`Processing`, `Submitting`, `Deleting`) and carrier tracking updates are kept in the shipment history only. The activity log gets:

- shipment created, with the carrier and the tracking number,
- shipment deleted,
- booking failed or outcome unknown, with the reason (as an alert),
- deletion outcome unknown (as an alert),
- changes made by staff, such as a tracking number set or removed.

## See shipments in the Manager

The order drawer shows the current shipment: state, tracking number with a link to the carrier, carrier, service, pickup point, carrier status and dates, and the carrier's details. Below it is the full history, with every event and every earlier shipment. Staff can set or correct the tracking number, and the change is recorded with their name.

The Manager uses `GET /ekom/manager/orderShipments/{orderId}` and `POST /ekom/manager/Order/{orderId}/Shipment/Tracking`. See [Orders in the Manager](../manager/orders.md).

## Moving from the Ekom.Shipping packages

The shipping framework used to be separate packages, and kept the shipment in the custom data of the order's shipping provider. If the site references `Ekom.Shipping`, `Ekom.Shipping.Core`, `Ekom.Shipping.U17` or `Ekom.Shipping.U18`, follow [Upgrade shipping](../upgrading/shipping.md).

## Related pages

- [Payment and shipping providers](providers.md): set up the delivery options a customer can choose.
- [Checkout](checkout.md): where automatic booking happens.
- [Orders in the Manager](../manager/orders.md): where staff see shipments and set tracking numbers.
- [Events](../reference/events.md): every event Ekom raises.
- [Integrations](../integrations/README.md): carrier and other integration packages.
- [Upgrade shipping](../upgrading/shipping.md): moving from the Ekom.Shipping packages.

[Back to Guides](README.md)
