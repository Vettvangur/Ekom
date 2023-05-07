using LinqToDB.Mapping;
using Newtonsoft.Json;
using System;

namespace Ekom.Payments.ValitorPay;

[Table(Name = "EkomValitorPayVirtualCards")]
public class VirtualCard
{
    [PrimaryKey, Identity, NotNull]
    public long Id { get; set; }

    [Column, NotNull]
    public Guid Member { get; set; }

    /// <summary>
    /// ValitorPay virtual credit card Guid
    /// </summary>
    [Column, NotNull]
    public Guid VirtualCardGuid { get; set; }

    [Column]
    public string? MaskedCreditCardNumber { get; set; }

    [Column]
    public string? ValidThrough { get; set; }

    [Column, NotNull]
    public bool Default { get; set; }

    [Column, NotNull]
    public DateTime CreateDate { get; set; }
}
