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
    [EkomProperty(PropertyEditorType.Language)]
    public string OrderName { get; set; }
    
    [EkomProperty(PropertyEditorType.Store)]
    public string Currency { get; set; }

    /// <summary>
    /// For Ekom properties, controls which key (Store/Language) we read properties from.
    /// Special case during population since properties marked with <see cref="EkomPropertyAttribute"/> depend on this value. <br />
    /// Although this property can itself contain an EkomProperty value on Umbraco payment provider nodes, 
    /// in such cases Ekom handles population of this value.
    /// </summary>
    [PaymentSettingsIgnore]
    //[EkomProperty]
    public Dictionary<PropertyEditorType, string> EkomPropertyKeys { get; }
        = new Dictionary<PropertyEditorType, string>();

    [PaymentSettingsIgnore]
    public string Store
    {
        get => EkomPropertyKeys[PropertyEditorType.Store];
        set => EkomPropertyKeys[PropertyEditorType.Store] = value;
    }
    [PaymentSettingsIgnore]
    public string Language
    {
        get => EkomPropertyKeys[PropertyEditorType.Language];
        set => EkomPropertyKeys[PropertyEditorType.Language] = value;
    }

    /// <summary>
    /// Optionally store umbraco member id in persisted order
    /// </summary>
    [PaymentSettingsIgnore]
    public Guid? Member { get; set; }

    /// <summary>
    /// This data is serialized into OrderCustomString and persisted through the payment process <br />
    /// Ekom Core f.x. stores it's order unique id in this object
    /// </summary>
    [PaymentSettingsIgnore]
    public Dictionary<string, string> OrderCustomData { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Perfect for storing custom data/json in persisted order to be read on callback after payment.
    /// 255 char max length.
    /// </summary>
    [PaymentSettingsIgnore]
    public string OrderCustomString 
    { 
        get => JsonConvert.SerializeObject(OrderCustomData);
    }

    /// <summary>
    /// Override umbraco configured success url. Used by Ekom Payments to forward user to f.x. receipt page.
    /// When using umbraco value, netPayment adds reference to queryString to use with OrderRetriever on return.
    /// Commonly overriden in consumers checkout 
    /// to provide an url that also contains a queryString with the orderId to use on receipt page.
    /// </summary>
    [EkomProperty(PropertyEditorType.Language)]
    public Uri SuccessUrl { get; set; }

    /// <summary>
    /// Control cancel url when supported
    /// </summary>
    [EkomProperty(PropertyEditorType.Language)]
    public Uri CancelUrl { get; set; }

    /// <summary>
    /// Override umbraco configured error url.
    /// </summary>
    [EkomProperty(PropertyEditorType.Language)]
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

    /// <summary>
    /// Provide customer information to payment provider.
    /// </summary>
    public CustomerInfo CustomerInfo { get; set; }

    ///// <summary>
    ///// BorgunLoans loan type specifier
    ///// </summary>
    //public int LoanType { get; set; }


    #region Direct Credit Card Payments

    /// <summary>
    /// 16 digit payment card number
    /// </summary>
    [PaymentSettingsIgnore]
    public string CardNumber { get; set; }

    [PaymentSettingsIgnore]
    public int CardExpirationMonth { get; set; }

    [PaymentSettingsIgnore]
    public int CardExpirationYear { get; set; }

    /// <summary>
    /// Card Verification Value. Triple digit number on the back of the card.
    /// </summary>
    [PaymentSettingsIgnore]
    public string CardCVV { get; set; }

    [PaymentSettingsIgnore]
    public string? VirtualCardNumber { get; set; }

    #endregion

    [PaymentSettingsIgnore]
    [JsonIgnore]
    public Dictionary<Type, object> CustomSettings { get; } = new Dictionary<Type, object>();
}
