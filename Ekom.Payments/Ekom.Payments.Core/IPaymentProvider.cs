using System.Threading.Tasks;

namespace Ekom.Payments;

/// <summary>
/// F.x. Valitor/Borgun, handles submission of payment request 
/// and generally returns html string to initiate redirect to portal.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Initiate a payment request.
    /// When calling RequestAsync, always await the result.
    /// </summary>
    /// <param name="paymentSettings">Configuration object for PaymentProviders</param>
    Task<string> RequestAsync(PaymentSettings paymentSettings);
}
