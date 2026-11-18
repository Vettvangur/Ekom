# Upgrade shipping

[Ekom docs](../README.md) / [Upgrading](README.md) / Upgrade shipping

This page is for sites that reference the `Ekom.Shipping` packages. The shipping framework is now part of Ekom, and shipments are saved as records on the order. Before, the framework was a set of separate packages, and the shipment was kept in the custom data of the order's shipping provider.

If the site never referenced an `Ekom.Shipping` package, you do not need this page.

## What changes

| Before | Now |
| --- | --- |
| `Ekom.Shipping`, `Ekom.Shipping.Core`, `Ekom.Shipping.U17` and `Ekom.Shipping.U18` packages | Part of Ekom. No separate package. |
| Types in the `Ekom.Shipping.Ekom` namespace | The same types in `Ekom.Shipping` |
| `ShippingFulfillmentState` | `Ekom.Models.Shipments.ShipmentState`, with the same values |
| The site calls `AddEkomShipping()` | Ekom registers the framework itself |
| Shipment kept in `customshippingTrackingNumber`, `customshippingShipmentId` and `customshippingShipmentState` on the order | Shipment records, read through `IOrderShipments` |

## Steps

1. Remove the `Ekom.Shipping`, `Ekom.Shipping.Core`, `Ekom.Shipping.U17` and `Ekom.Shipping.U18` package references from the site.
2. Keep the carrier packages, for example `Ekom.Shipping.Dropp`. Check each one for a version built for the Ekom version you are moving to.
3. Remove the call to `AddEkomShipping()` from the site's service setup.
4. Change `using Ekom.Shipping.Ekom;` to `using Ekom.Shipping;`.
5. Replace `ShippingFulfillmentState` with `Ekom.Models.Shipments.ShipmentState`.
6. Find code that reads `customshippingTrackingNumber`, `customshippingShipmentId` or `customshippingShipmentState` from an order, and change it to read `IOrderShipments`. See [Read shipments](../guides/shipping.md#read-shipments).

## What happens to existing orders

An older order still has its shipment in the shipping custom data. Ekom copies it to a shipment record the first time the order is opened in the Manager or its shipment is read.

- The custom data is left as it is. Nothing is deleted.
- No activity log entry is added for the copy.
- No shipment event is raised for the copy.
- New shipments no longer write the three `customshipping` keys. Code that still reads them sees old orders only.

## Check that it worked

1. The site builds and starts without the `Ekom.Shipping` packages.
2. Open an order that was shipped before the upgrade in the Manager. Its shipment and tracking number are shown.
3. Complete a new order and record or book a shipment for it. The shipment appears on the order.

## What Ekom does not do for you

- Update the carrier packages.
- Change your code that reads the old `customshipping` keys.
- Remove the old keys from existing orders.

## Related pages

- [Shipping and shipments](../guides/shipping.md): how shipments work now, and how to connect a carrier.
- [Orders in the Manager](../manager/orders.md): where staff see shipments and set tracking numbers.
- [Upgrade payments](payments.md): the matching change for payments.
- [Before and after an upgrade](overview.md): the checks that apply to every upgrade.

[Back to Upgrading](README.md)
