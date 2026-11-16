using Ekom.Models.Payments;
using Microsoft.Extensions.Logging;

namespace Ekom.Payments;

/// <summary>
/// Gives every log line of a payment the same identifying fields, order first:
/// OrderNumber, OrderUniqueId, PaymentId, PaymentProvider and PaymentAttemptId.
/// </summary>
public static class PaymentLog
{
    /// <summary>The order-first prefix payment log messages share.</summary>
    public const string Template = "OrderNumber: {OrderNumber} OrderId: {OrderUniqueId} PaymentId: {PaymentId}";

    /// <summary>Opens a logging scope for a payment. Dispose it when the request is handled.</summary>
    public static IDisposable? BeginScope(ILogger logger, OrderPayment payment)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(payment);
        return logger.BeginScope(new Dictionary<string, object?>
        {
            ["OrderNumber"] = payment.OrderNumber,
            ["OrderUniqueId"] = payment.OrderUniqueId,
            ["PaymentId"] = payment.PaymentId,
            ["PaymentProvider"] = payment.ProviderType ?? payment.ProviderName,
            ["PaymentAttemptId"] = payment.AttemptId,
        });
    }

    /// <summary>Opens a logging scope for a payment request that has no payment yet.</summary>
    public static IDisposable? BeginScope(ILogger logger, PaymentSettings settings, string providerType)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(settings);
        return logger.BeginScope(new Dictionary<string, object?>
        {
            ["OrderNumber"] = settings.OrderNumber,
            ["OrderUniqueId"] = settings.OrderUniqueId,
            ["PaymentProvider"] = providerType,
        });
    }
}
