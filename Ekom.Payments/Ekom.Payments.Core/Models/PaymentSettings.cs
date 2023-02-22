using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Ekom.Payments;

/// <summary>
/// Base configuration for PaymentProviders. 
/// </summary>
public class PaymentSettings : PaymentSettingsBase<PaymentSettings>
{
    /// <summary>
    /// Chooses a specific payment provider node.
    /// Useful when you have multiple umbraco nodes targetting the same base payment provider.
    /// F.x. Borgun EN and IS with varying currencies and xml configurations.
    /// </summary>
    [PaymentSettingsIgnore]
    public Guid PaymentProviderKey { get; set; }

    /// <summary>
    /// Chooses a specific payment provider node.
    /// Useful when you have multiple umbraco nodes targetting the same base payment provider.
    /// F.x. Borgun EN and IS with varying currencies and xml configurations.
    /// </summary>
    [PaymentSettingsIgnore]
    public string PaymentProviderName { get; set; }

    /// <summary>
    /// Order lines, displayed as a list during payment
    /// </summary>
    [PaymentSettingsIgnore]
    public IEnumerable<OrderItem> Orders { get; set; }

    /// <summary>
    /// Allows to override order name, is otherwise auto-generated from concatenating
    /// OrderItem Names.
    /// </summary>
    [EkomProperty]
    public string OrderName { get; set; }

    /// <summary>
    /// Controls payment portal language.
    /// Default IS
    /// </summary>
    [EkomProperty]
    public string Language { get; set; }
    
    [EkomProperty]
    public string Currency { get; set; }

    /// <summary>
    /// For Ekom properties, controls which key (Store/Language) we read properties from.
    /// Special case during population since properties marked with <see cref="EkomPropertyAttribute"/> depend on this value. <br />
    /// Although this property can itself contain an EkomProperty value on Umbraco payment provider nodes, 
    /// in such cases Ekom handles population of this value.
    /// </summary>
    [PaymentSettingsIgnore]
    //[EkomProperty]
    public Dictionary<PropertyEditorType, string> EkomPropertyKey { get; set; }

    /// <summary>
    /// Optionally store umbraco member id in persisted order
    /// </summary>
    [PaymentSettingsIgnore]
    public int Member { get; set; }

    /// <summary>
    /// Perfect for storing custom data/json in persisted order to be read on callback after payment.
    /// 255 char max length.
    /// </summary>
    [PaymentSettingsIgnore]
    public string OrderCustomString { get; set; }

    /// <summary>
    /// Override umbraco configured success url. Used by Ekom Payments to forward user to f.x. receipt page.
    /// When using umbraco value, netPayment adds reference to queryString to use with OrderRetriever on return.
    /// Commonly overriden in consumers checkout 
    /// to provide an url that also contains a queryString with the orderId to use on receipt page.
    /// </summary>
    [EkomProperty]
    public Uri SuccessUrl { get; set; }

    /// <summary>
    /// Control cancel url when supported
    /// </summary>
    [EkomProperty]
    public Uri CancelUrl { get; set; }

    /// <summary>
    /// Override umbraco configured error url.
    /// </summary>
    [EkomProperty]
    public Uri ErrorUrl { get; set; }

    ///// <summary>
    ///// Supported by: PayPal, Stripe
    ///// </summary>
    //public Currency? Currency { get; set; }

    ///// <summary>
    ///// Email address to send receipts for purchases to
    ///// Supported by: Borgun
    ///// </summary>
    //public string MerchantEmail { get; set; }

    ///// <summary>
    ///// Customer name, mobile number and home address.
    ///// Supported by: Borgun
    ///// Merchantemail parameter must be set since cardholder information is returned through email to merchant.
    ///// </summary>
    //public bool RequireCustomerInformation { get; set; }

    ///// <summary>
    ///// Provide customer information to payment provider.
    ///// Supported by: BorgunLoans, Pei
    ///// Partial support: Netgiro (Phone number)
    ///// </summary>
    //public CustomerInfo CustomerInfo { get; set; }

    ///// <summary>
    ///// BorgunLoans loan type specifier
    ///// </summary>
    //public int LoanType { get; set; }


    //#region Borgun Gateway

    ///// <summary>
    ///// 16 digit payment card number
    ///// Supported by: BorgunGateway
    ///// </summary>
    //public string CardNumber { get; set; }

    //private string _expiry;
    ///// <summary>
    ///// Card expiration date in YYMM format
    ///// Supported by: BorgunGateway, BorgunRpg
    ///// </summary>
    //public string Expiry
    //{
    //    get => _expiry;
    //    set
    //    {
    //        var month = int.Parse(value.Substring(2, 2));
    //        var val = int.Parse(value);

    //        if (value.Length != 4
    //        || val < 0
    //        || month > 12)
    //        {
    //            throw new FormatException("Please supply a valid expiry value in the format YYMM");
    //        }

    //        _expiry = value;
    //    }
    //}

    ///// <summary>
    ///// Card Verification Value. Triple digit number on the back of the card.
    ///// Supported by: BorgunGateway
    ///// </summary>
    //public string CVV { get; set; }

    //#endregion

    [PaymentSettingsIgnore]
    [JsonIgnore]
    public Dictionary<Type, object> CustomSettings { get; } = new Dictionary<Type, object>();
}
