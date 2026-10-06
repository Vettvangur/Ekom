using Ekom.Models;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ekom.Analytics;

/// <summary>Projects frozen JSON without constructing pricing or catalog models.</summary>
public sealed class AnalyticsSnapshotMapper
{
    private readonly IAnalyticsCustomerIdentityResolver _resolver;

    public AnalyticsSnapshotMapper(IAnalyticsCustomerIdentityResolver resolver, IOptions<AnalyticsOptions> options)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        ArgumentNullException.ThrowIfNull(options);
    }

    public AnalyticsProjection Map(OrderData data, DateTime projectedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(data.OrderInfo))
            throw new InvalidOperationException("The order snapshot is empty.");

        try
        {
            using var document = JsonDocument.Parse(data.OrderInfo);
            var snapshot = AnalyticsSnapshotJson.Decode(document.RootElement);
            if (snapshot.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("The order snapshot must be an object.");
            return MapSnapshot(data, snapshot, projectedAtUtc);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The order snapshot is malformed JSON.", exception);
        }
    }

    private AnalyticsProjection MapSnapshot(OrderData data, JsonElement snapshot, DateTime projectedAtUtc)
    {
        var rootDiscount = AnalyticsSnapshotJson.Property(snapshot, "Discount");
        var rootDiscountKey = AnalyticsSnapshotJson.GuidValue(rootDiscount, "Key");
        var rootCoupon = NormalizeCoupon(AnalyticsSnapshotJson.Text(snapshot, "Coupon"));
        var lines = new List<AnalyticsOrderLineData>();
        var promotions = new List<AnalyticsPromotionData>();
        var lineKeys = new HashSet<Guid>();
        AddPromotion(promotions, data.UniqueId, null, rootDiscount, rootCoupon);

        var savedLines = AnalyticsSnapshotJson.Property(snapshot, "OrderLines");
        if (savedLines.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The order snapshot must contain an OrderLines array.");

        foreach (var savedLine in savedLines.EnumerateArray())
        {
            var line = AnalyticsSnapshotJson.Decode(savedLine);
            var key = AnalyticsSnapshotJson.GuidValue(line, "Key")
                ?? throw new InvalidOperationException("An order line has no valid Key.");
            if (!lineKeys.Add(key))
                throw new InvalidOperationException("The order snapshot contains duplicate order line keys.");
            var amount = AnalyticsSnapshotJson.Property(line, "Amount");
            lines.Add(MapLine(data.UniqueId, key, line));

            var discount = AnalyticsSnapshotJson.Property(line, "Discount");
            if (discount.ValueKind != JsonValueKind.Object)
                discount = AnalyticsSnapshotJson.Property(amount, "Discount");
            var coupon = NormalizeCoupon(AnalyticsSnapshotJson.Text(line, "Coupon"));
            var discountKey = AnalyticsSnapshotJson.GuidValue(discount, "Key");
            if (rootDiscountKey.HasValue && discountKey == rootDiscountKey)
            {
                // A root rule repeated on each line is one recorded application, not additional revenue.
                if (!string.IsNullOrWhiteSpace(coupon) && !string.Equals(coupon, rootCoupon, StringComparison.Ordinal))
                    AddPromotion(promotions, data.UniqueId, key, default, coupon);
            }
            else
            {
                AddPromotion(promotions, data.UniqueId, key, discount, coupon);
            }

            // A stackable line discount can sit on top of the selected product or
            // variant price discount. Keep that recorded rule separately without
            // inventing a monetary allocation or counting overridden base rules.
            if (AnalyticsSnapshotJson.Property(discount, "Stackable").ValueKind == JsonValueKind.True)
            {
                var product = AnalyticsSnapshotJson.Property(line, "Product");
                var variant = SelectedVariant(line, product);
                var priceSource = variant.ValueKind == JsonValueKind.Object ? variant : product;
                var baseDiscount = AnalyticsSnapshotJson.Property(AnalyticsSnapshotJson.Property(priceSource, "Price"), "Discount");
                var baseKey = AnalyticsSnapshotJson.GuidValue(baseDiscount, "Key");
                if (baseKey.HasValue && baseKey != discountKey && baseKey != rootDiscountKey)
                    AddPromotion(promotions, data.UniqueId, key, baseDiscount, null);
            }
        }

        return new AnalyticsProjection(MapOrder(data, snapshot, lines, projectedAtUtc), lines, promotions);
    }

    private static AnalyticsOrderLineData MapLine(Guid orderId, Guid key, JsonElement line)
    {
        var product = AnalyticsSnapshotJson.Property(line, "Product");
        var variant = SelectedVariant(line, product);
        var amount = AnalyticsSnapshotJson.Property(line, "Amount");
        var settings = AnalyticsSnapshotJson.Property(line, "Settings");
        return new AnalyticsOrderLineData
        {
            OrderId = orderId,
            OrderLineKey = key,
            ProductKey = AnalyticsSnapshotJson.GuidValue(line, "ProductKey") ?? EntityKey(product),
            VariantKey = AnalyticsSnapshotJson.GuidValue(line, "VariantKey") ?? EntityKey(variant),
            ProductId = EntityId(product),
            VariantId = EntityId(variant),
            ProductTitle = Label(EntityText(product, "Title", "title"), 255),
            VariantTitle = Label(EntityText(variant, "Title", "title"), 255),
            ProductSku = Label(EntityText(product, "SKU", "sku"), 255),
            VariantSku = Label(EntityText(variant, "SKU", "sku"), 255),
            Quantity = AnalyticsSnapshotJson.Number(AnalyticsSnapshotJson.Property(line, "Quantity"))
                ?? throw new InvalidOperationException("An order line has no valid Quantity."),
            CountToTotal = AnalyticsSnapshotJson.Property(settings, "CountToTotal").ValueKind != JsonValueKind.False,
            // Amount is the saved line total, not a unit price. Never multiply it by Quantity again.
            TotalWithVat = GrossPrice(amount),
            TotalWithoutVat = RequiredPrice(amount, "WithoutVat"),
            DiscountAmount = OptionalPrice(amount, "DiscountAmount")
                ?? PriceDifference(amount, "BeforeDiscount", "AfterDiscount") ?? 0,
            DiscountAmountWithoutVat = PriceDifference(amount, "BeforeDiscountWithOutVat", "AfterDiscountWithOutVat") ?? 0,
        };
    }

    private AnalyticsOrderData MapOrder(OrderData data, JsonElement snapshot, IReadOnlyList<AnalyticsOrderLineData> lines, DateTime projectedAtUtc)
    {
        var shipping = AnalyticsSnapshotJson.Property(snapshot, "ShippingProvider");
        var payment = AnalyticsSnapshotJson.Property(snapshot, "PaymentProvider");

        var identity = _resolver.Resolve(data, snapshot);
        var identityType = string.IsNullOrWhiteSpace(identity?.Type) ? null : identity.Type.Trim();
        var identityValue = string.IsNullOrWhiteSpace(identity?.Value) ? null : identity.Value.Trim();
        if (identityType == null || identityValue == null)
            identityType = identityValue = null;
        var customer = AnalyticsSnapshotJson.Property(AnalyticsSnapshotJson.Property(snapshot, "CustomerInformation"), "Customer");
        var sourceId = data.CustomerId > 0 ? data.CustomerId : AnalyticsSnapshotJson.IntValue(customer, "UserId");
        return new AnalyticsOrderData
        {
            OrderId = data.UniqueId,
            ReferenceId = data.ReferenceId,
            OrderNumber = Bounded(data.OrderNumber ?? string.Empty, 100, "OrderNumber"),
            StoreAlias = Bounded(data.StoreAlias ?? string.Empty, 100, "StoreAlias"),
            CurrencyCode = Bounded(CurrencyCode(data, snapshot), 10, "CurrencyCode"),
            OrderStatus = Bounded(data.OrderStatusCol ?? string.Empty, 100, "OrderStatus"),
            CreateDate = data.CreateDate,
            PaidDate = data.PaidDate,
            GrandTotal = RequiredPrice(snapshot, "GrandTotal"),
            GrandTotalWithoutVat = RequiredPrice(snapshot, "GrandTotalWithOutVat"),
            ChargedAmount = RequiredPrice(snapshot, "ChargedAmount"),
            MerchandiseTotalWithVat = OptionalPrice(snapshot, "OrderLineTotal") ?? lines.Sum(line => line.TotalWithVat),
            MerchandiseTotalWithoutVat = OptionalPrice(snapshot, "OrderLineTotalWithOutVat") ?? lines.Sum(line => line.TotalWithoutVat),
            ShippingAmountWithVat = ProviderPrice(shipping, false),
            ShippingAmountWithoutVat = ProviderPrice(shipping, true),
            PaymentFeeWithVat = ProviderPrice(payment, false),
            PaymentFeeWithoutVat = ProviderPrice(payment, true),
            DiscountAmount = OptionalPrice(snapshot, "DiscountAmount") ?? lines.Sum(line => line.DiscountAmount),
            DiscountAmountWithoutVat = OptionalPrice(snapshot, "DiscountAmountWithOutVat") ?? lines.Sum(line => line.DiscountAmountWithoutVat),
            TotalQuantity = lines.Where(line => line.CountToTotal).Sum(line => line.Quantity),
            CustomerIdentityType = identityType == null ? null : Bounded(identityType, 32, "CustomerIdentityType"),
            // StoreAlias scopes queries; the hash itself is the full normalized type + value, never a truncated label.
            CustomerIdentityKey = identityType == null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityType + "\n" + identityValue))),
            CustomerName = Label(!string.IsNullOrWhiteSpace(data.CustomerName) ? data.CustomerName : AnalyticsSnapshotJson.CustomerValue(snapshot, "Name", "customerName"), 255),
            CustomerEmail = Label(!string.IsNullOrWhiteSpace(data.CustomerEmail) ? data.CustomerEmail : AnalyticsSnapshotJson.CustomerValue(snapshot, "Email", "customerEmail"), 320),
            SourceCustomerId = sourceId > 0 ? sourceId.Value.ToString(CultureInfo.InvariantCulture) : null,
            PaymentProviderKey = AnalyticsSnapshotJson.GuidValue(payment, "Key"),
            PaymentProviderTitle = Label(AnalyticsSnapshotJson.Text(payment, "Title"), 255),
            ShippingProviderKey = AnalyticsSnapshotJson.GuidValue(shipping, "Key"),
            ShippingProviderTitle = Label(AnalyticsSnapshotJson.Text(shipping, "Title"), 255),
            ShippingMethod = Label(AnalyticsSnapshotJson.Text(shipping, "Method"), 255),
            ProjectedAtUtc = projectedAtUtc,
        };
    }

    private static void AddPromotion(List<AnalyticsPromotionData> promotions, Guid orderId, Guid? lineKey, JsonElement discount, string? coupon)
    {
        if (discount.ValueKind != JsonValueKind.Object && string.IsNullOrWhiteSpace(coupon))
            return;
        var key = AnalyticsSnapshotJson.GuidValue(discount, "Key");
        var scope = lineKey.HasValue ? "line" : "order";
        var fingerprint = $"{orderId:D}\n{scope}\n{lineKey:D}\n{key:D}\n{coupon}";
        var applicationId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)).AsSpan(0, 16));
        if (promotions.Any(promotion => promotion.ApplicationId == applicationId))
            return;
        var amount = AnalyticsSnapshotJson.Property(discount, "Amount");
        promotions.Add(new AnalyticsPromotionData
        {
            ApplicationId = applicationId,
            OrderId = orderId,
            OrderLineKey = lineKey,
            Scope = scope,
            DiscountKey = key,
            Title = Label(AnalyticsSnapshotJson.Text(discount, "Title"), 255),
            CouponCode = Label(coupon, 255),
            DiscountType = DiscountTypeName(discount),
            RuleAmount = AnalyticsSnapshotJson.Number(amount) ?? AnalyticsSnapshotJson.Number(AnalyticsSnapshotJson.Property(amount, "Value")),
            RealizedAmount = null,
            Attribution = string.IsNullOrWhiteSpace(coupon) ? "RecordedRule" : "RecordedCoupon",
        });
    }

    private static string CurrencyCode(OrderData data, JsonElement snapshot)
    {
        var currency = AnalyticsSnapshotJson.Property(AnalyticsSnapshotJson.Property(snapshot, "StoreInfo"), "Currency");
        var code = AnalyticsSnapshotJson.Text(currency, "ISOCurrencySymbol");
        if (!string.IsNullOrWhiteSpace(code))
            return code.ToUpperInvariant();
        var value = AnalyticsSnapshotJson.Text(currency, "CurrencyValue") ?? data.Currency;
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        if (value.Length == 3 && value.All(char.IsLetter))
            return value.ToUpperInvariant();
        try
        {
            return new RegionInfo(CultureInfo.GetCultureInfo(value).Name).ISOCurrencySymbol;
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("The order snapshot has no recognizable currency.");
        }
    }

    private static string? NormalizeCoupon(string? coupon) => string.IsNullOrWhiteSpace(coupon)
        ? null : coupon.Trim().ToUpperInvariant();

    private static string? DiscountTypeName(JsonElement discount)
    {
        var text = AnalyticsSnapshotJson.Text(discount, "Type");
        return Enum.TryParse<DiscountType>(text, true, out var type) && Enum.IsDefined(type)
            ? type.ToString() : Label(text, 32);
    }

    private static decimal ProviderPrice(JsonElement provider, bool withoutVat)
    {
        if (provider.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return 0;
        var price = AnalyticsSnapshotJson.Property(provider, "Price");
        return withoutVat ? RequiredPrice(price, "WithoutVat") : GrossPrice(price);
    }

    private static decimal GrossPrice(JsonElement price) => OptionalPrice(price, "WithVat")
        ?? AnalyticsSnapshotJson.Number(AnalyticsSnapshotJson.Property(price, "Value"))
        ?? throw new InvalidOperationException("A saved price has no valid WithVat.Value or Value.");

    private static decimal RequiredPrice(JsonElement parent, string name) => OptionalPrice(parent, name)
        ?? throw new InvalidOperationException($"The order snapshot has no valid {name}.Value.");

    private static decimal? OptionalPrice(JsonElement parent, string name) =>
        AnalyticsSnapshotJson.Number(AnalyticsSnapshotJson.Property(AnalyticsSnapshotJson.Property(parent, name), "Value"));

    private static decimal? PriceDifference(JsonElement price, string before, string after)
    {
        var beforeValue = OptionalPrice(price, before);
        var afterValue = OptionalPrice(price, after);
        return beforeValue.HasValue && afterValue.HasValue ? beforeValue.Value - afterValue.Value : null;
    }

    private static Guid? EntityKey(JsonElement entity) => AnalyticsSnapshotJson.GuidValue(entity, "Key")
        ?? AnalyticsSnapshotJson.GuidValue(AnalyticsSnapshotJson.Property(entity, "Properties"), "__Key");

    private static int? EntityId(JsonElement entity) => AnalyticsSnapshotJson.IntValue(entity, "Id")
        ?? AnalyticsSnapshotJson.IntValue(AnalyticsSnapshotJson.Property(entity, "Properties"), "__NodeId")
        ?? AnalyticsSnapshotJson.IntValue(AnalyticsSnapshotJson.Property(entity, "Properties"), "id");

    private static string? EntityText(JsonElement entity, string name, string property) => AnalyticsSnapshotJson.Text(entity, name)
        ?? AnalyticsSnapshotJson.Text(AnalyticsSnapshotJson.Property(entity, "Properties"), property);

    private static JsonElement First(JsonElement array) => array.ValueKind == JsonValueKind.Array && array.GetArrayLength() > 0
        ? AnalyticsSnapshotJson.Decode(array[0]) : default;

    private static JsonElement SelectedVariant(JsonElement line, JsonElement product)
    {
        var variant = AnalyticsSnapshotJson.Property(line, "Variant");
        if (variant.ValueKind == JsonValueKind.Object) return variant;
        var group = First(AnalyticsSnapshotJson.Property(product, "VariantGroups"));
        return First(AnalyticsSnapshotJson.Property(group, "Variants"));
    }

    private static string? Label(string? value, int length) => string.IsNullOrWhiteSpace(value)
        ? null : value.Trim().Length <= length ? value.Trim() : value.Trim()[..length];

    private static string Bounded(string value, int length, string name) => value.Length <= length
        ? value : throw new InvalidOperationException($"{name} exceeds its analytics column length.");
}

internal static class AnalyticsSnapshotJson
{
    internal static JsonElement Decode(JsonElement value)
    {
        // Some historic snapshots stored objects as JSON strings. Limit decoding to avoid unbounded recursion.
        for (var depth = 0; depth < 2 && value.ValueKind == JsonValueKind.String; depth++)
        {
            var text = value.GetString();
            if (string.IsNullOrWhiteSpace(text) || (text.TrimStart()[0] != '{' && text.TrimStart()[0] != '['))
                break;
            using var document = JsonDocument.Parse(text);
            value = document.RootElement.Clone();
        }
        return value;
    }

    internal static JsonElement Property(JsonElement parent, string name, bool decode = true)
    {
        parent = Decode(parent);
        if (parent.ValueKind != JsonValueKind.Object)
            return default;
        if (parent.TryGetProperty(name, out var value))
            return decode ? Decode(value) : value;
        foreach (var property in parent.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return decode ? Decode(property.Value) : property.Value;
        }
        return default;
    }

    internal static string? Text(JsonElement parent, string name)
    {
        var value = Property(parent, name, decode: false);
        return value.ValueKind == JsonValueKind.String ? value.GetString()
            : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : null;
    }

    internal static decimal? Number(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;
        if (value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            return number;
        return null;
    }

    internal static Guid? GuidValue(JsonElement parent, string name) => Guid.TryParse(Text(parent, name), out var value)
        && value != Guid.Empty ? value : null;

    internal static int? IntValue(JsonElement parent, string name) => int.TryParse(Text(parent, name), NumberStyles.Integer,
        CultureInfo.InvariantCulture, out var value) ? value : null;

    internal static string? CustomerValue(JsonElement snapshot, string name, string property)
    {
        var information = Property(snapshot, "CustomerInformation");
        var customer = Property(information, "Customer");
        var directValue = Property(customer, name, decode: false);
        var direct = directValue.ValueKind == JsonValueKind.String ? directValue.GetString() : null;
        if (!string.IsNullOrWhiteSpace(direct))
            return direct;
        var propertyValue = Property(Property(customer, "Properties"), property, decode: false);
        return propertyValue.ValueKind == JsonValueKind.String ? propertyValue.GetString() : null;
    }
}
