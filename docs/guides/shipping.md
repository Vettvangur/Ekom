# Shipping and shipments

Ekom owns everything about an order's shipments: the contracts a carrier implements, the record of each shipment and what happened to it, and the manager view built from that record. Carriers plug into it.

A site that ships with a courier it books by hand needs Ekom alone: staff enter the tracking number on the order. Each carrier Ekom books with is one extra package, for example `Ekom.Shipping.Dropp`. See [Providers](providers.md) for configuring shipping provider nodes and [Checkout](checkout.md) for the request flow.

## Identifiers

| Name | What it is | Where it comes from |
| --- | --- | --- |
| Order id | The order's Guid. | `OrderData.UniqueId` |
| Shipment id | One shipment of an order, in Ekom. An order can have several. | `OrderShipment.ShipmentId` |
| Carrier shipment ID | The carrier's own id of the shipment. | The carrier's booking response |
| Tracking number | What the recipient and staff follow the shipment with. | The carrier's booking response, or entered by staff |
| Shipment reference | The booking reference, such as a barcode reserved before the shipment was submitted. | The carrier |

## What is stored

Two tables, created at startup.

`EkomOrderShipments` has one row per shipment. What staff act on has a column of its own:

| Group | Columns |
| --- | --- |
| Identity | `ShipmentId`, `OrderUniqueId`, `OrderNumber`, `StoreAlias`, `Kind` (`Outbound`, `Return`, `ExtraParcel`) |
| Method | `ProviderKey`, `ProviderName`, `CarrierAlias`, `AccountReference`, `ServiceId`, `ServiceName` |
| Carrier | `TrackingNumber`, `TrackingUrl`, `CarrierShipmentId`, `ShipmentReference` |
| State | `State`, `CarrierStatus`, `CarrierStatusText`, `Attempts`, `LastError` |
| Pickup point | `PickupLocationId`, `PickupLocationName`, `PickupLocationAddress`, `PickupLocationPostalCode`, `PickupLocationCity` |
| Money | `Amount`, `Currency`: what the customer paid for shipping |
| Dates | `CreatedUtc`, `UpdatedUtc`, `ShippedUtc`, `DeliveredUtc`, `EstimatedDeliveryUtc` |
| Carrier extras | `Documents` (references to stored labels, never the labels themselves) and `Details` |

`Details` is a list of labelled values the carrier chooses to show staff, such as a piece count. They are stored with the shipment, so the manager shows them without knowing the carrier.

`EkomOrderShipmentEvents` has one row per thing that happened to a shipment. Rows are only ever added, so earlier attempts, failures and deleted shipments stay visible.

| Type | Meaning |
| --- | --- |
| `State` | The shipment moved to another state. The row carries the state and, for a failure, why. |
| `Tracking` | The carrier reported progress, such as "out for delivery", with its status code and location. |
| `Manual` | A manager user changed the shipment, such as its tracking number. |
| `Note` | Something worth knowing that changes nothing. |

## Shipment states

| State | Meaning |
| --- | --- |
| `Processing` | Ekom is preparing the booking. |
| `Submitting` | The booking was sent to the carrier. |
| `Created` | The carrier created the shipment. |
| `Failed` | The carrier refused the booking. It can be retried. |
| `OutcomeUnknown` | The carrier may or may not have created the shipment, for example after a dropped connection. It is never retried automatically. |
| `Deleting`, `Deleted` | The shipment is being, or was, deleted at the carrier. |
| `DeleteOutcomeUnknown` | The carrier may or may not have deleted the shipment. |

A deleted shipment stays in the order's history, and the order can be given a new shipment. A failed shipment is retried on the same shipment, which keeps a reserved booking reference and counts the attempt.

## Where the checkout selection lives

The carrier, service and pickup point a customer chooses belong to the cart, before any shipment exists. They stay on the order's shipping provider as `customshipping*` custom data, written by `IEkomShippingCheckoutService.SaveSelectionAsync`. When a shipment is booked, the service name and the pickup point are copied onto the shipment.

Nothing about the shipment itself is written to the order's stored information.

## Reading shipments

```csharp
public sealed class WarehouseExport(IOrderShipments shipments)
{
    public async Task<string?> TrackingNumberAsync(Guid orderId, CancellationToken ct)
    {
        var view = await shipments.GetForOrderAsync(orderId, ct);
        return view.Current?.TrackingNumber;
    }
}
```

| Method | Returns |
| --- | --- |
| `GetForOrderAsync(orderId)` | The current shipment and every shipment of the order, each with its events. |
| `GetCurrentAsync(orderId)` | The latest outbound shipment that was not deleted. |
| `GetCurrentShipmentsAsync(orderIds)` | The current shipment of many orders, loaded together. |
| `GetAsync(shipmentId)` | One shipment. |
| `FindOrderAsync(text)` | The order of a tracking number, a carrier shipment ID or a shipment reference. |

## Recording shipments

The shipping framework records the shipments it books. Code that ships another way, such as a warehouse system that prints its own labels, records them too:

```csharp
var shipment = await shipments.CreateAsync(new NewOrderShipment
{
    OrderUniqueId = order.UniqueId,
    OrderNumber = order.OrderNumber,
    State = ShipmentState.Created,
    ProviderName = "Warehouse courier",
    TrackingNumber = "AB123456789",
    TrackingUrl = "https://courier.example/track/AB123456789",
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

In a `ShipmentUpdate`, a value left null keeps what the shipment has, and an empty string clears it. `Error` is stored as the shipment's last error whenever `State` is set, so a state change without an error clears it.

## Carriers

A carrier package implements `IShippingCarrier` for checkout (services and pickup points) and `IShippingFulfillmentCarrier` for booking, in the `Ekom.Shipping` namespace. A carrier never stores anything itself: it returns a `ShipmentBookingResult` and Ekom records it.

```csharp
return new ShipmentBookingResult(shipmentId, bookingReference, trackingNumber)
{
    TrackingUrl = ShippingTrackingLink.Format(account.TrackingUrlTemplate, trackingNumber),
    ServiceName = "Express Worldwide",
    Details = [new ShipmentDetail("pieces", "Pieces", "2")],
};
```

Register a carrier in the site's startup with the package's own method, for example `services.AddDroppShipping(configuration)`. A shipping provider node names its carrier with three properties: `shippingCarrierAlias`, `shippingCarrierAccount` and `shippingCarrierService`.

`IShippingFulfillmentService` books, retries, prints and deletes a carrier shipment for an order. A carrier account in `Automatic` mode has its shipment booked when checkout completes; a shipment that cannot be booked never fails the checkout.

## Events

`OrderEvents.ShipmentRecordedAsync` is raised after a shipment was created or something that happened to it was stored. The arguments carry the shipment, the event and the state before it.

```csharp
OrderEvents.ShipmentRecordedAsync += async (sender, args, ct) =>
{
    if (args.Event.Type == ShipmentEventType.State && args.Shipment.State == ShipmentState.Created)
    {
        await NotifyCustomerAsync(args.Shipment.OrderUniqueId, args.Shipment.TrackingNumber, ct);
    }
};
```

## Activity log

The activity log entry for a shipment is written in the same database transaction as the event. Steps on the way to the carrier (`Processing`, `Submitting`, `Deleting`) are history only; the log gets the outcomes: created, failed, outcome unknown, deleted, and tracking numbers changed by staff.

## Manager

The order drawer shows the current shipment: state, tracking number with a link to the carrier, carrier, service, pickup point, carrier status and dates, the carrier's details, and an expandable history with every event and every earlier shipment. Staff can set or correct the tracking number; the change is recorded with their name.

The manager endpoints are `GET /ekom/manager/orderShipments/{orderId}` and `POST /ekom/manager/Order/{orderId}/Shipment/Tracking`.

## Upgrading from the Ekom.Shipping framework packages

The shipping framework used to be its own packages and kept the shipment on the order's shipping provider custom data.

- Remove `Ekom.Shipping`, `Ekom.Shipping.Core` and `Ekom.Shipping.U10`/`U17`/`U18`. Their code is part of Ekom. Keep the carrier packages and update them to 1.0.
- Types that were in the `Ekom.Shipping.Ekom` namespace are in `Ekom.Shipping`.
- `ShippingFulfillmentState` is `Ekom.Models.Shipments.ShipmentState`, with the same values.
- `AddEkomShipping()` is no longer called by the site; Ekom registers the framework.
- An order whose shipment is still on its shipping custom data has it copied to a shipment record the first time the order is opened in the manager or its shipment is read. The custom data is left as it is. No activity log entry is added for the copy.
- Code that read `customshippingTrackingNumber`, `customshippingShipmentId` or `customshippingShipmentState` from the order reads `IOrderShipments` instead. New shipments no longer write those keys.
