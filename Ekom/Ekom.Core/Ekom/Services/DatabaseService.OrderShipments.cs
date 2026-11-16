using Ekom.Models.Shipments;
using LinqToDB;

namespace Ekom.Services;

internal partial class DatabaseService
{
    /// <summary>Creates the order shipment tables and their indexes. Safe to run on every startup.</summary>
    internal virtual void EnsureOrderShipmentTables()
    {
        using var db = _databaseFactory.GetDatabase();
        db.CreateTable<OrderShipment>(tableOptions: TableOptions.CreateIfNotExists);
        db.CreateTable<OrderShipmentEvent>(tableOptions: TableOptions.CreateIfNotExists);

        EnsurePaymentIndex(db, "EkomOrderShipments", "IX_EkomOrderShipments_ShipmentId", true, nameof(OrderShipment.ShipmentId));
        EnsurePaymentIndex(db, "EkomOrderShipments", "IX_EkomOrderShipments_OrderUniqueId", false, nameof(OrderShipment.OrderUniqueId));
        EnsurePaymentIndex(db, "EkomOrderShipments", "IX_EkomOrderShipments_TrackingNumber", false, nameof(OrderShipment.TrackingNumber));
        EnsurePaymentIndex(db, "EkomOrderShipments", "IX_EkomOrderShipments_CarrierShipmentId", false, nameof(OrderShipment.CarrierShipmentId));
        EnsurePaymentIndex(db, "EkomOrderShipments", "IX_EkomOrderShipments_ShipmentReference", false, nameof(OrderShipment.ShipmentReference));
        EnsurePaymentIndex(db, "EkomOrderShipmentEvents", "IX_EkomOrderShipmentEvents_IdempotencyKey", true, nameof(OrderShipmentEvent.IdempotencyKey));
        EnsurePaymentIndex(db, "EkomOrderShipmentEvents", "IX_EkomOrderShipmentEvents_OrderUniqueId", false, nameof(OrderShipmentEvent.OrderUniqueId));
        EnsurePaymentIndex(db, "EkomOrderShipmentEvents", "IX_EkomOrderShipmentEvents_ShipmentId", false, nameof(OrderShipmentEvent.ShipmentId));
    }
}
