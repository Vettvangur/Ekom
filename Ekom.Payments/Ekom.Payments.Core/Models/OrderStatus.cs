using LinqToDB.Mapping;
using Newtonsoft.Json;
using System;

namespace Ekom.Payments;

/// <summary>
/// Generalized object storing basic information on orders and their status
/// </summary>
[Table(Name = "EkomPaymentOrders")]
public class OrderStatus
{
    /// <summary>
    /// Friendly order name: f.x. IS0001
    /// </summary>
    [Column(Length = 50)]
    public string? OrderName { get; set; }

    /// <summary>
    /// Order SQL unique Id
    /// </summary>
    [Column, NotNull]
    public Guid UniqueId { get; set; }

    /// <summary>
    /// Used by borgun gateway for the rrn field.
    /// Is trimmed to 12 characters, this gives us a maximum order count of 10^12
    /// </summary>
    [PrimaryKey, Identity, NotNull]
    public long ReferenceId { get; set; }

    /// <summary>
    /// Umbraco member id
    /// </summary>
    [Column]
    public Guid? Member { get; set; }

    /// <summary>
    /// Total amount
    /// </summary>
    [Column, NotNull]
    public decimal Amount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Column, NotNull]
    public DateTime Date { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Column(Length = 45), NotNull]
    public string IPAddress { get; set; }

    /// <summary>
    /// Browser User agent
    /// </summary>
    [Column(Length = 4000)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Column, NotNull]
    public bool Paid { get; set; }

    /// <summary>
    /// For netpayment internal usage
    /// </summary>
    [Column, NotNull]
    public string EkomPaymentSettingsData { get; set; }

    public PaymentSettings EkomPaymentSettings 
    { 
        get
        {
            return JsonConvert.DeserializeObject<PaymentSettings>(EkomPaymentSettingsData);
        }
        set
        {
            EkomPaymentSettingsData = JsonConvert.SerializeObject(value);
        }
    }

    /// <summary>
    /// For netpayment internal usage <br />
    /// Contains the payment provider specific settings, f.x. PayPalSettings
    /// </summary>
    [Column]
    public string? EkomPaymentProviderData { get; set; }

    /// <summary>
    /// Contains other custom json data not configured via Settings <br />
    /// F.x. values received via Api calls to payment providers that are needed later in the payment flow
    /// </summary>
    [Column]
    public string? CustomData { get; set; }

    [Column(SkipOnInsert = true, SkipOnUpdate = true)]
    public string PaymentProviderName { get; set; }

    /// <summary>
    /// Guid key of payment provider <see cref="IPaymentProvider"/> umbraco node
    /// Helps to resolve overloaded payment providers, f.x. Borgun USD and Borgun ISK
    /// </summary>
    [Column(SkipOnInsert = true, SkipOnUpdate = true)]
    public string PaymentProviderKey { get; set; }
}
