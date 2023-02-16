namespace Ekom.Payments.Valitor;

public class ValitorSettings : PaymentSettingsBase<ValitorSettings>
{
    public string ReportUrl { get; set; }
    
    [EkomProperty]
    public string MerchantId { get; set; }

    [EkomProperty]
    public string VerificationCode { get; set; }

    /// <summary>
    /// Required by Valitor loans
    /// </summary>
    [EkomProperty]
    public string MerchantName { get; set; }

    /// <summary>
    /// Required by Valitor loans
    /// </summary>
    public LoanType LoanType { get; set; }

    /// <summary>
    /// Controls how long the user has to complete checkout on payment portal page.
    /// Must be configured in tandem with a TimeoutRedirectURL property on umbraco payment provider.
    /// </summary>
    public int CheckoutTimeoutMinutes { get; set; }

    public bool SkipReceipt { get; set; }
    
    [EkomProperty]
    public string PaymentSuccessfulURLText { get; set; }
    
    /// <summary>
    /// Dev https://paymentweb.uat.valitor.is/
    /// Prod https://greidslusida.valitor.is
    /// </summary>
    public Uri PaymentPageUrl { get; set; }

    [EkomProperty]
    public Uri TimeoutRedirectURL { get; set; }
}
