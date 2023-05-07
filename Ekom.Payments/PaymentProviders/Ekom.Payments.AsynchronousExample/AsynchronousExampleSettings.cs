namespace Ekom.Payments.AsynchronousExample;

public class AsynchronousExampleSettings : PaymentSettingsBase<AsynchronousExampleSettings>
{
    public string ReportUrl { get; set; }
    
    [EkomProperty(PropertyEditorType.Store)]
    public string MerchantId { get; set; }
    
    [EkomProperty(PropertyEditorType.Language)]
    public string PaymentSuccessfulURLText { get; set; }
    
    /// <summary>
    /// Dev https://uat.AsynchronousExample.is/
    /// Prod https://AsynchronousExample.is
    /// </summary>
    public Uri PaymentPageUrl { get; set; }

    /// <summary>
    /// Controls how long the user has to complete checkout on payment portal page.
    /// Must be configured in tandem with a TimeoutRedirectURL property on umbraco payment provider.
    /// </summary>
    public int SessionExpiredTimeoutInSeconds { get; set; }

    [EkomProperty(PropertyEditorType.Language)]
    public Uri SessionExpiredRedirectURL { get; set; }
}
