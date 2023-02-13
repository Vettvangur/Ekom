using LinqToDB.Mapping;
using System;

namespace Ekom.Payments;

/// <summary>
/// Saves payment information for transactions
/// </summary>
[Table(Name = "NetPayments")]
public class PaymentData
{
    /// <summary>
    /// SQL entry Id
    /// </summary>
    [PrimaryKey, Identity, NotNull]
    public long Id { get; set; }

    /// <summary>
    /// Masked credit card number
    /// </summary>
    [Column(Length = 30)]
    public string CardNumber { get; set; }

    /// <summary>
    /// Mastercard/Visa/etc...
    /// </summary>
    [Column(Length = 100)]
    public string PaymentMethod { get; set; }

    /// <summary>
    /// Json blob of payment data
    /// </summary>
    [Column]
    public string CustomData { get; set; }

    /// <summary>
    /// Total
    /// </summary>
    [Column, NotNull]
    public decimal Amount { get; set; }

    /// <summary>
    /// SQL entry creation date
    /// </summary>
    [Column, NotNull]
    public DateTime Date { get; set; }
}
