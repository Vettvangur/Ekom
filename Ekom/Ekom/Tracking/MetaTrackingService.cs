using Ekom.Models;
using Microsoft.AspNetCore.WebUtilities;
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
    private readonly ILogger<MetaTrackingService> _logger;

    public MetaTrackingService(HttpClient httpClient, IOptions<TrackingOptions> options, ILogger<MetaTrackingService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public MetaPurchaseRequest CreatePurchaseRequest(IOrderInfo orderInfo)
    {
        var tracking = orderInfo.Tracking ?? new OrderTracking();
        var request = new MetaPurchaseRequest
        {
            StoreAlias = orderInfo.StoreInfo.Alias,
            EventTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            EventId = orderInfo.OrderNumber,
            EventSourceUrl = BuildEventSourceUrl(orderInfo, tracking),
            Email = orderInfo.CustomerInformation.Customer.Email,
            Phone = orderInfo.CustomerInformation.Customer.Phone,
            FirstName = orderInfo.CustomerInformation.Customer.FirstName,
            LastName = orderInfo.CustomerInformation.Customer.LastName,
            Fbp = tracking.Meta.Fbp,
            Fbc = tracking.Meta.Fbc,
            Value = orderInfo.ChargedAmount.Value,
            Currency = orderInfo.StoreInfo.Currency.ISOCurrencySymbol,
            Source = tracking.Source,
            Medium = tracking.Medium,
            Campaign = tracking.Campaign,
            Term = tracking.Term,
            Content = tracking.Content,
            Gclid = string.Equals(tracking.ClickIdType, "gclid", StringComparison.OrdinalIgnoreCase) ? tracking.ClickId : null,
            UserData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            CustomData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            Contents = orderInfo.OrderLines.Select(x => new MetaPurchaseContent
            {
                Id = x.Variant?.SKU ?? x.Product.SKU,
                Quantity = x.Quantity,
                ItemPrice = Math.Round(x.Amount.WithVat.Value, 2, MidpointRounding.AwayFromZero)
            }).ToList()
        };

        foreach (var entry in tracking.Meta.Data)
            request.CustomData[entry.Key] = entry.Value;

        return request;
    }

    public async Task SendPurchaseAsync(MetaPurchaseRequest request, CancellationToken ct = default)
    {
        ApplyConsent(request);

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
        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogWarning("Meta tracking failed for store {StoreAlias}. Status: {StatusCode}. Response: {Response}", request.StoreAlias, response.StatusCode, responseBody);
        }
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
        var baseUrl = tracking.LandingUrl ?? _options.Value.SiteBaseUrl;
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

    private TrackingStoreOptions? ResolveStore(string storeAlias)
        => _options.Value.Meta.Stores.FirstOrDefault(x => x.Alias.Equals(storeAlias, StringComparison.OrdinalIgnoreCase));

    private static void ApplyConsent(MetaPurchaseRequest request)
    {
        if (request.HasMarketingConsent)
        {
            return;
        }

        request.EventSourceUrl = null;
        request.Email = null;
        request.Phone = null;
        request.FirstName = null;
        request.LastName = null;
        request.Fbp = null;
        request.Fbc = null;
        request.Source = null;
        request.Medium = null;
        request.Campaign = null;
        request.Term = null;
        request.Content = null;
        request.Gclid = null;
        request.UserData.Clear();
        request.CustomData.Clear();
    }

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
}
