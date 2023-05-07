using Microsoft.Extensions.Configuration;
using System;
using System.Configuration;
using System.Globalization;
using Ekom.Payments.Helpers;

namespace Ekom.Payments;

/// <summary>
/// Various settings for the Ekom.Payments package.
/// Settings are bound from appSettings.
/// </summary>
public class PaymentsConfiguration
{
    /// <summary>
    /// Payment provider document type alias prefix.
    /// Ensures pp specific doc types are created with the same prefix, f.x. ekmPaymentPayPal
    /// </summary>
    public const string ProviderDocTypeAliasPrefix = "ekmPayment";
    
    /// <summary>
    /// Payment providers container document type alias.
    /// </summary>
    public const string ContainerDocumentTypeAlias
        = "ekmPaymentProviders";

    ///// <summary>
    ///// Default number format
    ///// Used by: Borgun, Netgiro
    ///// </summary>
    //public static readonly NumberFormatInfo Nfi = new CultureInfo("is-IS", false).NumberFormat;

    /// <summary>
    /// Ekom:Payments:SendEmailAlerts
    /// This property controls whether we attempt to send emails when exceptions occur in certain places.
    /// Used in response controllers.
    /// Defaults to true.
    /// </summary>
    public virtual bool SendEmailAlerts { get; set; } = true;
}
