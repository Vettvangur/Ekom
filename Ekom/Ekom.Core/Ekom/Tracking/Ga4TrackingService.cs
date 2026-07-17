using Ekom.API;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Ekom.Tracking;

public sealed class Ga4TrackingService : IGa4TrackingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<TrackingOptions> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Ga4TrackingService> _logger;

    public Ga4TrackingService(
        IHttpClientFactory httpClientFactory,
        IOptions<TrackingOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<Ga4TrackingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Ga4PurchaseRequest CreatePurchaseRequest(IOrderInfo orderInfo)
    {
        var request = CreateRequest(orderInfo, "purchase");
        request.TransactionId = orderInfo.OrderNumber;
        request.Value = orderInfo.ChargedAmount.Value;
        request.Shipping = orderInfo.ShippingProvider?.Price.Value ?? 0;
        request.Tax = orderInfo.Vat.Value;
        request.PaymentType = orderInfo.PaymentProvider?.Title;
        request.ShippingTier = orderInfo.ShippingProvider?.Title;
        request.Items = orderInfo.OrderLines.Select(orderLine => CreateItem(orderLine, orderInfo.StoreInfo.Alias)).ToList();

        return request;
    }

    public Ga4PurchaseRequest CreateAddedToCartRequest(IOrderInfo orderInfo, IOrderLine orderLine)
    {
        var request = CreateRequest(orderInfo, "add_to_cart");
        request.Value = orderLine.Amount.WithoutVat.Value * orderLine.Quantity;
        request.Items = [CreateItem(orderLine, orderInfo.StoreInfo.Alias)];

        return request;
    }

    public Ga4PurchaseRequest CreateRemovedFromCartRequest(IOrderInfo orderInfo, IOrderLine orderLine)
    {
        var request = CreateRequest(orderInfo, "remove_from_cart");
        request.Value = orderLine.Amount.WithoutVat.Value * orderLine.Quantity;
        request.Items = [CreateItem(orderLine, orderInfo.StoreInfo.Alias)];

        return request;
    }

    public Ga4PurchaseRequest CreateStartedCheckoutRequest(IOrderInfo orderInfo)
    {
        var request = CreateRequest(orderInfo, "begin_checkout");
        request.Value = orderInfo.ChargedAmount.Value;
        request.Items = orderInfo.OrderLines.Select(orderLine => CreateItem(orderLine, orderInfo.StoreInfo.Alias)).ToList();

        return request;
    }

    public Ga4PurchaseRequest CreateAddedShippingInfoRequest(IOrderInfo orderInfo)
    {
        var request = CreateRequest(orderInfo, "add_shipping_info");
        request.Value = orderInfo.ChargedAmount.Value;
        request.ShippingTier = orderInfo.ShippingProvider?.Title;
        request.Items = orderInfo.OrderLines.Select(orderLine => CreateItem(orderLine, orderInfo.StoreInfo.Alias)).ToList();

        return request;
    }

    public Ga4PurchaseRequest CreateAddedPaymentInfoRequest(IOrderInfo orderInfo)
    {
        var request = CreateRequest(orderInfo, "add_payment_info");
        request.Value = orderInfo.ChargedAmount.Value;
        request.PaymentType = orderInfo.PaymentProvider?.Title;
        request.Items = orderInfo.OrderLines.Select(orderLine => CreateItem(orderLine, orderInfo.StoreInfo.Alias)).ToList();

        return request;
    }

    private Ga4PurchaseRequest CreateRequest(IOrderInfo orderInfo, string eventName)
    {
        var tracking = orderInfo.Tracking ?? new OrderTracking();
        var clientId = tracking.Ga4.ClientId;
        var generatedClientId = false;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            clientId = GenerateClientId();
            generatedClientId = true;
        }

        var sessionId = ParseLong(tracking.Ga4.SessionId);
        if (!sessionId.HasValue)
        {
            sessionId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        var request = new Ga4PurchaseRequest
        {
            OrderUniqueId = orderInfo.UniqueId,
            StoreAlias = orderInfo.StoreInfo.Alias,
            ClientId = clientId,
            SessionId = sessionId,
            EventName = eventName,
            Currency = orderInfo.StoreInfo.Currency.ISOCurrencySymbol,
            Source = tracking.Source,
            Medium = tracking.Medium,
            Campaign = tracking.Campaign,
            Term = tracking.Term,
            Content = tracking.Content,
            Gclid = string.Equals(tracking.ClickIdType, "gclid", StringComparison.OrdinalIgnoreCase) ? tracking.ClickId : null,
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        };

        if (generatedClientId)
        {
            _logger.LogWarning("GA4 client_id missing for order {OrderNumber}. Generated fallback client_id for store {StoreAlias}.", orderInfo.OrderNumber, orderInfo.StoreInfo.Alias);
        }

        foreach (var entry in tracking.Ga4.Data)
            request.Parameters[entry.Key] = entry.Value;

        return request;
    }

    private static Ga4PurchaseItem CreateItem(IOrderLine orderLine, string storeAlias)
    {
        var catalogProduct = Catalog.Instance.GetProduct(orderLine.ProductKey, storeAlias);

        return new Ga4PurchaseItem
        {
            ItemId = orderLine.Variant?.SKU ?? orderLine.Product.SKU,
            ItemName = orderLine.Product.Title,
            ItemCategory = catalogProduct?.Categories.FirstOrDefault()?.Title,
            ItemCategory2 = catalogProduct?.CategoryAncestors.LastOrDefault()?.Title,
            Price = orderLine.Amount.WithoutVat.Value,
            Discount = 0,
            Quantity = Convert.ToInt32(orderLine.Quantity),
            ItemVariant = orderLine.Variant?.Title
        };
    }

    public async Task SendPurchaseAsync(Ga4PurchaseRequest request, CancellationToken ct = default)
    {
        ApplyConsent(request);

        var storeOptions = ResolveStore(request.StoreAlias);
        if (string.IsNullOrWhiteSpace(storeOptions?.MeasurementId) || string.IsNullOrWhiteSpace(storeOptions.ApiSecret))
        {
            _logger.LogWarning("GA4 tracking skipped for store {StoreAlias} because MeasurementId or ApiSecret is missing.", request.StoreAlias);
            return;
        }

        var payload = new
        {
            client_id = request.ClientId,
            events = new[]
            {
                new
                {
                    name = request.EventName,
                    @params = BuildParameters(request)
                }
            }
        };

        var endpoint = _options.Value.Ga4.Testing ? "debug/mp/collect" : "mp/collect";
        var url = $"https://www.google-analytics.com/{endpoint}?measurement_id={storeOptions.MeasurementId}&api_secret={storeOptions.ApiSecret}";
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        if (_options.Value.LogPurchaseEventData)
        {
            _logger.LogInformation("GA4 {EventName} event payload for store {StoreAlias}: {Payload}", request.EventName, request.StoreAlias, payloadJson);
        }

        using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        using var response = await _httpClientFactory.CreateClient().PostAsync(url, content, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await WriteActivityLogAsync(request.OrderUniqueId, $"GA4 {request.EventName} event failed ({(int)response.StatusCode})", OrderActivityLogType.Alert).ConfigureAwait(false);
            _logger.LogWarning("GA4 {EventName} tracking failed for store {StoreAlias}. Status: {StatusCode}. Response: {Response}", request.EventName, request.StoreAlias, response.StatusCode, responseBody);
            return;
        }

        if (_options.Value.Ga4.Testing && TryGetValidationError(responseBody, out string? validationError))
        {
            await WriteActivityLogAsync(request.OrderUniqueId, $"GA4 {request.EventName} event validation failed: {validationError}", OrderActivityLogType.Alert).ConfigureAwait(false);
            _logger.LogWarning("GA4 {EventName} debug validation failed for store {StoreAlias}: {ValidationError}. Response: {Response}", request.EventName, request.StoreAlias, validationError, responseBody);
            return;
        }

        await WriteActivityLogAsync(request.OrderUniqueId, $"GA4 {request.EventName} event successfully sent", OrderActivityLogType.Success).ConfigureAwait(false);
    }

    private object BuildParameters(Ga4PurchaseRequest request)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["value"] = request.Value,
            ["currency"] = request.Currency,
            ["source"] = request.Source,
            ["medium"] = request.Medium,
            ["campaign"] = request.Campaign,
            ["term"] = request.Term,
            ["content"] = request.Content,
            ["gclid"] = request.Gclid,
            ["items"] = request.Items.Select(item => new Dictionary<string, object?>
            {
                ["item_id"] = item.ItemId,
                ["item_name"] = item.ItemName,
                ["item_category"] = item.ItemCategory,
                ["item_category2"] = item.ItemCategory2,
                ["price"] = item.Price,
                ["discount"] = item.Discount,
                ["quantity"] = item.Quantity,
                ["item_variant"] = item.ItemVariant
            }).Select(FilterNullValues).ToList()
        };

        if (string.Equals(request.EventName, "purchase", StringComparison.OrdinalIgnoreCase))
        {
            parameters["transaction_id"] = request.TransactionId;
            parameters["shipping"] = request.Shipping;
            parameters["tax"] = request.Tax;
        }

        if (string.Equals(request.EventName, "purchase", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.EventName, "add_payment_info", StringComparison.OrdinalIgnoreCase))
        {
            parameters["payment_type"] = request.PaymentType;
        }

        if (string.Equals(request.EventName, "purchase", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.EventName, "add_shipping_info", StringComparison.OrdinalIgnoreCase))
        {
            parameters["shipping_tier"] = request.ShippingTier;
        }

        if (request.SessionId.HasValue)
            parameters["session_id"] = request.SessionId.Value;

        if (_options.Value.Ga4.Testing)
            parameters["debug_mode"] = true;

        foreach (var item in request.Parameters)
            parameters[item.Key] = item.Value;

        return FilterNullValues(parameters);
    }

    private TrackingStoreOptions? ResolveStore(string storeAlias)
        => _options.Value.Ga4.Stores.FirstOrDefault(x => x.Alias.Equals(storeAlias, StringComparison.OrdinalIgnoreCase));

    private void ApplyConsent(Ga4PurchaseRequest request)
    {
        if (request.HasAnalyticsConsent)
        {
            return;
        }

        request.ClientId = GenerateClientId();
        request.SessionId = null;
        request.Parameters.Clear();
    }

    private static long? ParseLong(string? value)
        => long.TryParse(value, out var parsed) ? parsed : null;

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
            _logger.LogWarning(ex, "Failed to write GA4 activity log for order {OrderUniqueId}.", orderUniqueId);
        }
    }

    private static Dictionary<string, object?> FilterNullValues(Dictionary<string, object?> source)
        => source
            .Where(x => x.Value != null)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    private static bool TryGetValidationError(string? responseBody, out string? validationError)
    {
        validationError = null;
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("validationMessages", out JsonElement validationMessages) || validationMessages.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            JsonElement.ArrayEnumerator enumerator = validationMessages.EnumerateArray();
            if (!enumerator.MoveNext())
            {
                return false;
            }

            JsonElement first = enumerator.Current;
            string? fieldPath = first.TryGetProperty("fieldPath", out JsonElement fieldPathElement) ? fieldPathElement.GetString() : null;
            string? description = first.TryGetProperty("description", out JsonElement descriptionElement) ? descriptionElement.GetString() : null;
            validationError = string.IsNullOrWhiteSpace(fieldPath)
                ? description
                : $"{fieldPath}: {description}";

            return !string.IsNullOrWhiteSpace(validationError);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string GenerateClientId()
        => $"{Random.Shared.Next(100000000, 999999999)}.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
}
