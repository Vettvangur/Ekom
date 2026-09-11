using LinqToDB.Mapping;

namespace Ekom.Models;

[Table(Name = "EkomCheckoutPreparation")]
internal sealed class CheckoutPreparationData
{
    [PrimaryKey, NotNull]
    public Guid OrderId { get; set; }
    [Column(Length = 36)]
    public string? Owner { get; set; }
    [Column(Length = int.MaxValue), NotNull]
    public string ProtectedIds { get; set; } = "[]";
}
