using Ekom.Models.Payments;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ekom.Payments;

/// <summary>Helpers for payment providers working with <see cref="IOrderPayments"/>.</summary>
public static class OrderPaymentsExtensions
{
    // Card details are only used while the payment request is made; they are never stored.
    private static readonly string[] _cardDataProperties =
    {
        nameof(PaymentSettings.CardNumber),
        nameof(PaymentSettings.CardCVV),
        nameof(PaymentSettings.CardExpirationMonth),
        nameof(PaymentSettings.CardExpirationYear),
    };

    /// <summary>
    /// Starts a payment for the order the settings belong to.
    /// </summary>
    /// <param name="payments">The order payments service.</param>
    /// <param name="settings">The payment settings Ekom handed to the provider.</param>
    /// <param name="providerType">The base payment provider name, e.g. "borgun". Callbacks use it to refuse payments of other providers.</param>
    /// <param name="amount">The amount requested from the gateway.</param>
    /// <param name="providerSettings">The provider's own settings, stored for the callback.</param>
    /// <param name="providerReference">A lookup key for gateways that do not echo the payment id.</param>
    /// <param name="httpContext">The checkout request, for IP address and user agent.</param>
    /// <param name="paymentId">A specific payment id. Leave empty to have one generated.</param>
    /// <param name="createdUtc">When the payment was requested, if not now. For payments copied from elsewhere.</param>
    /// <param name="ct">Cancellation token.</param>
    public static Task<OrderPayment> CreateAsync(this IOrderPayments payments, PaymentSettings settings, string providerType, decimal amount,
        object? providerSettings = null, string? providerReference = null, HttpContext? httpContext = null,
        Guid paymentId = default, DateTime? createdUtc = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(settings);

        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
        return payments.CreateAsync(new NewOrderPayment
        {
            PaymentId = paymentId,
            CreatedUtc = createdUtc,
            OrderUniqueId = settings.OrderUniqueId,
            OrderNumber = settings.OrderNumber,
            AttemptId = settings.OrderCustomData.TryGetValue("ekomPaymentAttemptId", out var attempt)
                && Guid.TryParse(attempt, out var attemptId) ? attemptId : null,
            StoreAlias = settings.EkomPropertyKeys.TryGetValue(PropertyEditorType.Store, out var store) ? store : null,
            ProviderKey = settings.PaymentProviderKey == Guid.Empty ? null : settings.PaymentProviderKey,
            ProviderName = settings.PaymentProviderName,
            ProviderType = providerType,
            Amount = amount,
            Currency = settings.Currency,
            ProviderReference = providerReference,
            Settings = SerializeWithoutCardData(settings),
            ProviderData = providerSettings == null ? null : JsonConvert.SerializeObject(providerSettings),
            Member = settings.Member,
            IPAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = userAgent is { Length: > 4000 } ? userAgent[..4000] : userAgent,
        }, ct);
    }

    /// <summary>The payment settings the payment was requested with. Card details are never stored.</summary>
    public static PaymentSettings? GetPaymentSettings(this OrderPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return string.IsNullOrWhiteSpace(payment.Settings) ? null : JsonConvert.DeserializeObject<PaymentSettings>(payment.Settings);
    }

    /// <summary>True when the payment was started by the given base payment provider.</summary>
    public static bool BelongsTo(this OrderPayment payment, string providerType)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return string.Equals(payment.ProviderType, providerType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The provider's own settings stored with the payment.</summary>
    public static T? GetProviderData<T>(this OrderPayment payment) where T : class
    {
        ArgumentNullException.ThrowIfNull(payment);
        return string.IsNullOrWhiteSpace(payment.ProviderData) ? null : JsonConvert.DeserializeObject<T>(payment.ProviderData);
    }

    private static string SerializeWithoutCardData(PaymentSettings settings)
    {
        var json = JObject.FromObject(settings);
        foreach (var property in json.Properties().ToList())
        {
            if (_cardDataProperties.Contains(property.Name, StringComparer.OrdinalIgnoreCase)) property.Remove();
        }
        return json.ToString(Formatting.None);
    }
}
