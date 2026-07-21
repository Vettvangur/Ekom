using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ekom.Tracking;

public sealed class MetaTrackingService : IMetaTrackingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly IOptions<TrackingOptions> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MetaTrackingService> _logger;

    public MetaTrackingService(HttpClient httpClient, IOptions<TrackingOptions> options, IServiceScopeFactory scopeFactory, ILogger<MetaTrackingService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public MetaPurchaseRequest CreatePurchaseRequest(IOrderInfo orderInfo)
    {
        var request = CreateRequest(orderInfo, "Purchase", orderInfo.OrderNumber);
        request.Value = orderInfo.ChargedAmount.Value;
        request.Contents = orderInfo.OrderLines.Select(CreateContent).ToList();

        return request;
    }

    public MetaPurchaseRequest CreateAddedToCartRequest(IOrderInfo orderInfo, IOrderLine orderLine)
    {
        var request = CreateRequest(orderInfo, "AddToCart", $"{orderInfo.OrderNumber}:{orderLine.Key}");
        request.Value = orderLine.Amount.WithVat.Value * orderLine.Quantity;
        request.Contents = [CreateContent(orderLine)];

        return request;
    }

    public MetaPurchaseRequest CreateStartedCheckoutRequest(IOrderInfo orderInfo)
    {
        var request = CreateRequest(orderInfo, "InitiateCheckout", $"{orderInfo.OrderNumber}:initiate_checkout");
        request.Value = orderInfo.ChargedAmount.Value;
        request.Contents = orderInfo.OrderLines.Select(CreateContent).ToList();

        return request;
    }

    private MetaPurchaseRequest CreateRequest(IOrderInfo orderInfo, string eventName, string eventId)
    {
        var tracking = orderInfo.Tracking ?? new OrderTracking();
        var request = new MetaPurchaseRequest
        {
            OrderUniqueId = orderInfo.UniqueId,
            StoreAlias = orderInfo.StoreInfo.Alias,
            EventTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            EventName = eventName,
            EventId = eventId,
            EventSourceUrl = BuildEventSourceUrl(orderInfo, tracking),
            Email = orderInfo.CustomerInformation.Customer.Email,
            Phone = orderInfo.CustomerInformation.Customer.Phone,
            FirstName = orderInfo.CustomerInformation.Customer.FirstName,
            LastName = orderInfo.CustomerInformation.Customer.LastName,
            Fbp = tracking.Meta.Fbp,
            Fbc = tracking.Meta.Fbc,
            Currency = orderInfo.StoreInfo.Currency.ISOCurrencySymbol,
            Source = tracking.Source,
            Medium = tracking.Medium,
            Campaign = tracking.Campaign,
            Term = tracking.Term,
            Content = tracking.Content,
            Gclid = string.Equals(tracking.ClickIdType, "gclid", StringComparison.OrdinalIgnoreCase) ? tracking.ClickId : null,
            UserData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            CustomData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var entry in tracking.Meta.Data)
            request.CustomData[entry.Key] = entry.Value;

        return request;
    }

    private static MetaPurchaseContent CreateContent(IOrderLine orderLine)
        => new()
        {
            Id = orderLine.Variant?.SKU ?? orderLine.Product.SKU,
            Quantity = orderLine.Quantity,
            ItemPrice = Math.Round(orderLine.Amount.WithVat.Value, 2, MidpointRounding.AwayFromZero)
        };

    public async Task SendPurchaseAsync(MetaPurchaseRequest request, CancellationToken ct = default)
    {
        if (!request.HasMarketingConsent)
        {
            return;
        }

        if (!HasMatchableUserData(request))
        {
            await WriteActivityLogAsync(request.OrderUniqueId, $"Meta {request.EventName} event skipped: insufficient customer information for matching", OrderActivityLogType.Alert).ConfigureAwait(false);
            _logger.LogWarning("Meta {EventName} tracking skipped for store {StoreAlias} because no matchable customer information is available.", request.EventName, request.StoreAlias);
            return;
        }

        var storeOptions = ResolveStore(request.StoreAlias);
        if (string.IsNullOrWhiteSpace(storeOptions?.PixelId) || string.IsNullOrWhiteSpace(storeOptions.AccessToken))
        {
            _logger.LogWarning("Meta tracking skipped for store {StoreAlias} because PixelId or AccessToken is missing.", request.StoreAlias);
            return;
        }

        var payload = new
        {
            data = new[] { BuildEvent(request) },
            test_event_code = ResolveTestEventCode(storeOptions, request.StoreAlias)
        };

        var url = $"{storeOptions.PixelId}/events?access_token={storeOptions.AccessToken}";
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        if (_options.Value.ShouldLogEventData(request.EventName))
        {
            _logger.LogInformation("Meta {EventName} event payload for store {StoreAlias}: {Payload}", request.EventName, request.StoreAlias, payloadJson);
        }

        using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var errorMessage = TryGetErrorMessage(responseBody, request.EventName, out string? parsedError)
                ? parsedError
                : $"Meta {request.EventName} event failed ({(int)response.StatusCode})";

            await WriteActivityLogAsync(request.OrderUniqueId, errorMessage, OrderActivityLogType.Alert).ConfigureAwait(false);
            _logger.LogWarning("Meta {EventName} tracking failed for store {StoreAlias}. Status: {StatusCode}. Response: {Response}", request.EventName, request.StoreAlias, response.StatusCode, responseBody);
            return;
        }

        await WriteActivityLogAsync(request.OrderUniqueId, $"Meta {request.EventName} event sent", OrderActivityLogType.Success).ConfigureAwait(false);
    }

    private object BuildEvent(MetaPurchaseRequest request)
    {
        var userData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["em"] = HashOrNull(NormalizeEmail(request.Email)),
            ["ph"] = HashOrNull(NormalizePhone(request.Phone)),
            ["fn"] = HashOrNull(NormalizeName(request.FirstName)),
            ["ln"] = HashOrNull(NormalizeName(request.LastName)),
            ["fbp"] = NormalizeBrowserId(request.Fbp),
            ["fbc"] = NormalizeBrowserId(request.Fbc)
        };

        foreach (var item in request.UserData)
            userData[item.Key] = item.Value;

        var customData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["value"] = Math.Round(request.Value, 2, MidpointRounding.AwayFromZero),
            ["currency"] = request.Currency,
            ["utm_source"] = NullIfWhiteSpace(request.Source),
            ["utm_medium"] = NullIfWhiteSpace(request.Medium),
            ["utm_campaign"] = NullIfWhiteSpace(request.Campaign),
            ["utm_term"] = NullIfWhiteSpace(request.Term),
            ["utm_content"] = NullIfWhiteSpace(request.Content),
            ["gclid"] = NullIfWhiteSpace(request.Gclid),
            ["contents"] = request.Contents.Select(x => new
            {
                id = x.Id,
                quantity = x.Quantity,
                item_price = x.ItemPrice
            }).ToList(),
            ["content_type"] = "product"
        };

        foreach (var item in request.CustomData)
            customData[item.Key] = item.Value;

        return new
        {
            event_name = request.EventName,
            event_time = request.EventTimeUnix ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            event_id = request.EventId,
            event_source_url = request.EventSourceUrl,
            action_source = request.ActionSource,
            user_data = userData,
            custom_data = customData
        };
    }

    private string? BuildEventSourceUrl(IOrderInfo orderInfo, OrderTracking tracking)
    {
        var baseUrl = tracking.LandingUrl ?? ResolveSiteBaseUrl(orderInfo.StoreInfo.Alias);
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        var query = new Dictionary<string, string?>
        {
            ["utm_source"] = tracking.Source,
            ["utm_medium"] = tracking.Medium,
            ["utm_campaign"] = tracking.Campaign,
            ["utm_term"] = tracking.Term,
            ["utm_content"] = tracking.Content,
            ["gclid"] = string.Equals(tracking.ClickIdType, "gclid", StringComparison.OrdinalIgnoreCase) ? tracking.ClickId : null,
            ["fbclid"] = ExtractFbclid(tracking)
        };

        return QueryHelpers.AddQueryString(
            baseUrl,
            query.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key, x => x.Value!));
    }

    private string? ResolveSiteBaseUrl(string storeAlias)
    {
        var storeBaseUrl = _options.Value.Stores
            .FirstOrDefault(x => string.Equals(x.Alias, storeAlias, StringComparison.OrdinalIgnoreCase))?
            .SiteBaseUrl;

        return string.IsNullOrWhiteSpace(storeBaseUrl) ? _options.Value.SiteBaseUrl : storeBaseUrl;
    }

    private TrackingStoreOptions? ResolveStore(string storeAlias)
        => _options.Value.Meta.Stores.FirstOrDefault(x => x.Alias.Equals(storeAlias, StringComparison.OrdinalIgnoreCase));

    private static bool HasMatchableUserData(MetaPurchaseRequest request)
        => HasValue(request.Email)
            || HasValue(request.Phone)
            || HasValue(request.FirstName)
            || HasValue(request.LastName)
            || HasValue(request.Fbp)
            || HasValue(request.Fbc)
            || request.UserData.Any(x => HasValue(x.Value));

    private string? ResolveTestEventCode(TrackingStoreOptions? storeOptions, string storeAlias)
    {
        if (!_options.Value.Meta.Testing)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(storeOptions?.TestEventCode))
        {
            _logger.LogWarning("Meta testing is enabled for store {StoreAlias}, but no TestEventCode is configured. Sending event normally.", storeAlias);
            return null;
        }

        return storeOptions.TestEventCode;
    }

    private static string? ExtractFbclid(OrderTracking tracking)
    {
        if (string.Equals(tracking.ClickIdType, "fbclid", StringComparison.OrdinalIgnoreCase))
            return tracking.ClickId;

        if (string.IsNullOrWhiteSpace(tracking.Meta.Fbc))
            return null;

        var parts = tracking.Meta.Fbc.Split('.');
        return parts.Length >= 4 ? parts[3] : tracking.Meta.Fbc;
    }

    private static string? NormalizeEmail(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static string? NormalizeName(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NormalizeBrowserId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? HashOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private async Task WriteActivityLogAsync(Guid orderUniqueId, string message, OrderActivityLogType logType)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var activityLogRepository = scope.ServiceProvider.GetRequiredService<ActivityLogRepository>();
            await activityLogRepository.InsertAsync(
                    new[]
                    {
                        new OrderActivityLogWrite(orderUniqueId, message, "System", DateTime.Now, logType),
                    })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write Meta activity log for order {OrderUniqueId}.", orderUniqueId);
        }
    }

    private static bool TryGetErrorMessage(string? responseBody, string eventName, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("error", out JsonElement error) || error.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            string? message = error.TryGetProperty("message", out JsonElement messageElement) ? messageElement.GetString() : null;
            string? userMessage = error.TryGetProperty("error_user_msg", out JsonElement userMessageElement) ? userMessageElement.GetString() : null;

            errorMessage = string.IsNullOrWhiteSpace(userMessage)
                ? message
                : $"{message} - {userMessage}";

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                errorMessage = $"Meta {eventName} event failed: {errorMessage}";
            }

            return !string.IsNullOrWhiteSpace(errorMessage);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasValue(object? value)
        => value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            JsonElement element => element.ValueKind != JsonValueKind.Null && element.ValueKind != JsonValueKind.Undefined,
            _ => true,
        };
}
