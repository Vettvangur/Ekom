using LinqToDB.Mapping;

namespace Ekom.Models;

// Durable capability, not a time-based lease. Recovery of an uncertain owner is an
// explicit administrative operation after inspecting the provider and database.
[Table(Name = "EkomCheckoutPaymentOperation")]
internal sealed class CheckoutPaymentOperationData
{
    [PrimaryKey, NotNull]
    public Guid OrderId { get; set; }
    [Column(Length = 36)]
    public string? Owner { get; set; }
    [Column]
    public Guid? ActiveAttemptId { get; set; }
    // Tokenless verified callbacks cannot be safely attributed to one historical
    // attempt. Block further changes until an operator reconciles that payment.
    [Column, NotNull]
    public bool ReconciliationRequired { get; set; }
}
