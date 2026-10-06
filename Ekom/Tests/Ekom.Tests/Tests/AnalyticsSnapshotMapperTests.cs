using Ekom.Analytics;
using Ekom.Models;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Ekom.Tests.Tests;

public class AnalyticsSnapshotMapperTests
{
    private static readonly Guid OrderId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid LineKey = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid DiscountKey = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly DateTime ProjectedAtUtc = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CurrentSnapshotPreservesSavedTotalsAndScalarMetadata()
    {
        var data = Data();
        var projection = Mapper().Map(data, ProjectedAtUtc);

        Assert.Equal(OrderId, projection.Order.OrderId);
        Assert.Equal(data.ReferenceId, projection.Order.ReferenceId);
        Assert.Equal(data.OrderNumber, projection.Order.OrderNumber);
        Assert.Equal(data.StoreAlias, projection.Order.StoreAlias);
        Assert.Equal("USD", projection.Order.CurrencyCode);
        Assert.Equal(data.OrderStatusCol, projection.Order.OrderStatus);
        Assert.Equal(data.CreateDate, projection.Order.CreateDate);
        Assert.Equal(data.PaidDate, projection.Order.PaidDate);
        Assert.Equal(130m, projection.Order.GrandTotal);
        Assert.Equal(104m, projection.Order.GrandTotalWithoutVat);
        Assert.Equal(110m, projection.Order.ChargedAmount);
        Assert.Equal(120m, projection.Order.MerchandiseTotalWithVat);
        Assert.Equal(96m, projection.Order.MerchandiseTotalWithoutVat);
        Assert.Equal(8m, projection.Order.ShippingAmountWithVat);
        Assert.Equal(6.4m, projection.Order.ShippingAmountWithoutVat);
        Assert.Equal(2m, projection.Order.PaymentFeeWithVat);
        Assert.Equal(1.6m, projection.Order.PaymentFeeWithoutVat);
        Assert.Equal(30m, projection.Order.DiscountAmount);
        Assert.Equal(24m, projection.Order.DiscountAmountWithoutVat);
        Assert.Equal("Delivery", projection.Order.ShippingProviderTitle);
        Assert.Equal("Shipping", projection.Order.ShippingMethod);
        Assert.Equal("Card", projection.Order.PaymentProviderTitle);
        Assert.Equal(ProjectedAtUtc, projection.Order.ProjectedAtUtc);
        Assert.Equal(1, projection.Order.ProjectionVersion);
    }

    [Fact]
    public void VariantAndDecimalQuantityUseFrozenLineAmountsWithoutMultiplyingAgain()
    {
        var projection = Mapper().Map(Data(), ProjectedAtUtc);
        var line = Assert.Single(projection.Lines);

        Assert.Equal(LineKey, line.OrderLineKey);
        Assert.Equal(OrderId, line.OrderId);
        Assert.Equal(Guid.Parse("40000000-0000-0000-0000-000000000004"), line.ProductKey);
        Assert.Equal(Guid.Parse("50000000-0000-0000-0000-000000000005"), line.VariantKey);
        Assert.Equal(42, line.ProductId);
        Assert.Equal(43, line.VariantId);
        Assert.Equal("Product", line.ProductTitle);
        Assert.Equal("Large", line.VariantTitle);
        Assert.Equal("P-1", line.ProductSku);
        Assert.Equal("V-1", line.VariantSku);
        Assert.Equal(2.5m, line.Quantity);
        Assert.Equal(2.5m, projection.Order.TotalQuantity);
        Assert.Equal(120m, line.TotalWithVat);
        Assert.Equal(96m, line.TotalWithoutVat);
        Assert.Equal(30m, line.DiscountAmount);
        Assert.Equal(24m, line.DiscountAmountWithoutVat);
        Assert.True(line.CountToTotal);
    }

    [Fact]
    public void MissingSettingsDefaultsCountToTotalToTrueAndFalseExcludesOnlyQuantity()
    {
        var snapshot = Snapshot();
        var line = snapshot["OrderLines"]![0]!;
        Assert.True(Mapper().Map(Data(snapshot), ProjectedAtUtc).Lines[0].CountToTotal);
        line["Settings"] = new JsonObject { ["CountToTotal"] = false };
        var projection = Mapper().Map(Data(snapshot), ProjectedAtUtc);
        Assert.False(projection.Lines[0].CountToTotal);
        Assert.Equal(0m, projection.Order.TotalQuantity);
        Assert.Equal(120m, projection.Order.MerchandiseTotalWithVat);
    }

    [Theory]
    [InlineData("GrandTotal")]
    [InlineData("GrandTotalWithOutVat")]
    [InlineData("ChargedAmount")]
    public void MissingRequiredTotalNeverFallsBackToOldGrandTotalOrScalarTotal(string name)
    {
        var snapshot = Snapshot();
        snapshot.Remove(name);
        snapshot["OldGrandTotal"] = new JsonObject { ["Value"] = 999m };
        var data = Data(snapshot);
        data.TotalAmount = 999m;
        Assert.Throws<InvalidOperationException>(() => Mapper().Map(data, ProjectedAtUtc));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("{\"Value\":false}")]
    [InlineData("{\"Value\":{}}")]
    [InlineData("{\"Value\":\"not-a-number\"}")]
    public void MalformedRequiredTotalIsRejectedWithKindGuards(string total)
    {
        var snapshot = Snapshot();
        snapshot["GrandTotal"] = JsonNode.Parse(total);
        Assert.Throws<InvalidOperationException>(() => Mapper().Map(Data(snapshot), ProjectedAtUtc));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("null")]
    public void MalformedSnapshotIsRejected(string json)
    {
        var data = Data();
        data.OrderInfo = json;
        Assert.Throws<InvalidOperationException>(() => Mapper().Map(data, ProjectedAtUtc));
    }

    [Fact]
    public void MalformedOrDuplicateLineKeysAreRejected()
    {
        var snapshot = Snapshot();
        snapshot["OrderLines"]!.AsArray().Add(snapshot["OrderLines"]![0]!.DeepClone());
        Assert.Throws<InvalidOperationException>(() => Mapper().Map(Data(snapshot), ProjectedAtUtc));
        snapshot["OrderLines"]!.AsArray().RemoveAt(1);
        snapshot["OrderLines"]![0]!["Key"] = new JsonObject();
        Assert.Throws<InvalidOperationException>(() => Mapper().Map(Data(snapshot), ProjectedAtUtc));
    }

    [Fact]
    public void RootDiscountRepeatedOnLineIsOneRuleAndNeverInventsRealizedAttribution()
    {
        var snapshot = Snapshot();
        snapshot["OrderLines"]![0]!["Discount"] = snapshot["Discount"]!.DeepClone();
        var first = Mapper().Map(Data(snapshot), ProjectedAtUtc);
        var second = Mapper().Map(Data(snapshot), ProjectedAtUtc);
        var promotion = Assert.Single(first.Promotions);

        Assert.Equal(DiscountKey, promotion.DiscountKey);
        Assert.Equal("order", promotion.Scope);
        Assert.Null(promotion.OrderLineKey);
        Assert.Equal(0.2m, promotion.RuleAmount);
        Assert.Equal("Percentage", promotion.DiscountType);
        Assert.Equal("RecordedRule", promotion.Attribution);
        Assert.Null(promotion.RealizedAmount);
        Assert.Equal(promotion.ApplicationId, Assert.Single(second.Promotions).ApplicationId);
        Assert.Equal(30m, first.Order.DiscountAmount);
    }

    [Fact]
    public void RecordedCouponsAreDistinctFromRuleAmounts()
    {
        var snapshot = Snapshot();
        snapshot["Coupon"] = "SAVE20";
        snapshot["OrderLines"]![0]!["Discount"] = snapshot["Discount"]!.DeepClone();
        snapshot["OrderLines"]![0]!["Coupon"] = "SAVE20";
        var projection = Mapper().Map(Data(snapshot), ProjectedAtUtc);
        var root = Assert.Single(projection.Promotions);
        Assert.Equal("SAVE20", root.CouponCode);
        Assert.Equal("RecordedCoupon", root.Attribution);
        Assert.Null(root.RealizedAmount);

        snapshot["OrderLines"]![0]!["Coupon"] = "LINE-ONLY";
        projection = Mapper().Map(Data(snapshot), ProjectedAtUtc);
        Assert.Equal(2, projection.Promotions.Count);
        var coupon = Assert.Single(projection.Promotions, promotion => promotion.Scope == "line");
        Assert.Equal("LINE-ONLY", coupon.CouponCode);
        Assert.Null(coupon.RuleAmount);
        Assert.Null(coupon.DiscountKey);
        Assert.Null(coupon.RealizedAmount);
    }

    [Theory]
    [InlineData(0, "Fixed")]
    [InlineData(1, "Percentage")]
    public void NumericDiscountTypesAndCaseInsensitiveCouponsAreNormalized(int type, string expectedType)
    {
        var snapshot = Snapshot();
        snapshot["Discount"]!["Type"] = type;
        snapshot["Coupon"] = " Save20 ";
        snapshot["OrderLines"]![0]!["Discount"] = snapshot["Discount"]!.DeepClone();
        snapshot["OrderLines"]![0]!["Coupon"] = "save20";

        var promotion = Assert.Single(Mapper().Map(Data(snapshot), ProjectedAtUtc).Promotions);

        Assert.Equal(expectedType, promotion.DiscountType);
        Assert.Equal("SAVE20", promotion.CouponCode);
    }

    [Fact]
    public void DifferentLineDiscountKeepsItsOwnFrozenRuleWithoutSummingWithOrderRule()
    {
        var snapshot = Snapshot();
        snapshot["OrderLines"]![0]!["Discount"] = new JsonObject
        {
            ["Key"] = "60000000-0000-0000-0000-000000000006",
            ["Title"] = "Line offer",
            ["Type"] = "Fixed",
            ["Amount"] = new JsonObject { ["Value"] = 4m },
        };
        var projection = Mapper().Map(Data(snapshot), ProjectedAtUtc);
        Assert.Equal(2, projection.Promotions.Count);
        Assert.Equal(4m, Assert.Single(projection.Promotions, promotion => promotion.Scope == "line").RuleAmount);
        Assert.All(projection.Promotions, promotion => Assert.Null(promotion.RealizedAmount));
        Assert.Equal(30m, projection.Order.DiscountAmount);
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void StackedVariantPriceRuleIsRecordedButOverriddenRuleIsNot(bool stackable, int expectedRules)
    {
        var snapshot = Snapshot();
        snapshot["Discount"]!["Stackable"] = stackable;
        snapshot["OrderLines"]![0]!["Discount"] = snapshot["Discount"]!.DeepClone();
        snapshot["OrderLines"]![0]!["Product"]!["VariantGroups"]![0]!["Variants"]![0]!["Price"]!["Discount"] = new JsonObject
        {
            ["Key"] = "60000000-0000-0000-0000-000000000006",
            ["Title"] = "Variant price discount",
            ["Type"] = 0,
            ["Amount"] = 5,
        };

        var projection = Mapper().Map(Data(snapshot), ProjectedAtUtc);

        Assert.Equal(expectedRules, projection.Promotions.Count);
        Assert.All(projection.Promotions, x => Assert.Null(x.RealizedAmount));
        Assert.Equal(30m, projection.Order.DiscountAmount);
    }

    [Fact]
    public void EmailIdentityPrefersScalarAndNormalizesWithoutRemovingPlusAddressing()
    {
        var data = Data();
        data.CustomerEmail = "  Alice+Shop@Example.com  ";
        var projection = Mapper().Map(data, ProjectedAtUtc);
        Assert.Equal("email", projection.Order.CustomerIdentityType);
        Assert.Equal(Hash("email", "ALICE+SHOP@EXAMPLE.COM"), projection.Order.CustomerIdentityKey);
        Assert.Equal("Alice+Shop@Example.com", projection.Order.CustomerEmail);
        Assert.Equal("Alice", projection.Order.CustomerName);
        Assert.Equal("17", projection.Order.SourceCustomerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmailIdentityReadsCurrentCustomerEmailAndPropertyFallback(bool propertyOnly)
    {
        var snapshot = Snapshot();
        if (propertyOnly)
            snapshot["CustomerInformation"]!["Customer"]!.AsObject().Remove("Email");
        var data = Data(snapshot);
        data.CustomerEmail = " ";
        var projection = Mapper().Map(data, ProjectedAtUtc);
        Assert.Equal(Hash("email", "SNAPSHOT+TAG@EXAMPLE.COM"), projection.Order.CustomerIdentityKey);
    }

    [Fact]
    public void UnknownCustomerRemainsUnknownEvenWithSourceCustomerId()
    {
        var snapshot = Snapshot();
        snapshot["CustomerInformation"] = new JsonObject { ["Customer"] = false };
        var data = Data(snapshot);
        data.CustomerEmail = "";
        var projection = Mapper().Map(data, ProjectedAtUtc);
        Assert.Null(projection.Order.CustomerIdentityType);
        Assert.Null(projection.Order.CustomerIdentityKey);
        Assert.Null(projection.Order.CustomerEmail);
        Assert.Equal("17", projection.Order.SourceCustomerId);
    }

    [Fact]
    public void CurrentSerializedCustomerFieldsAreReadAndNonStringEmailsAreUnknown()
    {
        var snapshot = Snapshot();
        var customer = new CustomerInfo();
        customer.Customer.Properties["customerEmail"] = "Saved+Customer@example.com";
        customer.Customer.Properties["customerName"] = "Saved Customer";
        snapshot["CustomerInformation"] = JsonSerializer.SerializeToNode(customer, new JsonSerializerOptions { IncludeFields = true });
        var data = Data(snapshot);
        data.CustomerEmail = "";
        data.CustomerName = null;
        var projection = Mapper().Map(data, ProjectedAtUtc);
        Assert.Equal(Hash("email", "SAVED+CUSTOMER@EXAMPLE.COM"), projection.Order.CustomerIdentityKey);
        Assert.Equal("Saved Customer", projection.Order.CustomerName);

        snapshot["CustomerInformation"]!["Customer"]!["Email"] = 42;
        snapshot["CustomerInformation"]!["Customer"]!["Properties"]!["customerEmail"] = false;
        data = Data(snapshot);
        data.CustomerEmail = "";
        Assert.Null(Mapper().Map(data, ProjectedAtUtc).Order.CustomerIdentityKey);
    }

    [Fact]
    public void CustomResolverAndPolicyVersionAreUsedWithoutChangingItsNormalizedValue()
    {
        var resolver = new Mock<IAnalyticsCustomerIdentityResolver>();
        resolver.Setup(value => value.Resolve(It.IsAny<OrderData>(), It.IsAny<JsonElement>()))
            .Callback<OrderData, JsonElement>((_, snapshot) => Assert.Equal(JsonValueKind.Object, snapshot.ValueKind))
            .Returns(new AnalyticsCustomerIdentity("account", "CaseSensitiveId"));
        var mapper = Mapper(resolver.Object, new AnalyticsOptions { CustomerIdentityPolicyVersion = 7 });
        var data = Data();
        var projection = mapper.Map(data, ProjectedAtUtc);
        Assert.Equal("account", projection.Order.CustomerIdentityType);
        Assert.Equal(Hash("account", "CaseSensitiveId"), projection.Order.CustomerIdentityKey);
        Assert.Equal(7, projection.Order.CustomerIdentityPolicyVersion);
        resolver.Verify(value => value.Resolve(data, It.IsAny<JsonElement>()), Times.Once);
    }

    [Fact]
    public void IdentityHashUsesFullEmailBeforeDisplayColumnTruncation()
    {
        var data = Data();
        data.CustomerEmail = new string('a', 330) + "+tag@example.com";
        var projection = Mapper().Map(data, ProjectedAtUtc);
        Assert.Equal(320, projection.Order.CustomerEmail!.Length);
        Assert.Equal(Hash("email", data.CustomerEmail.ToUpperInvariant()), projection.Order.CustomerIdentityKey);
    }

    [Fact]
    public void GroupingKeysAndIdentityTypesAreNotSilentlyTruncated()
    {
        var data = Data();
        data.StoreAlias = new string('s', 101);
        Assert.Throws<InvalidOperationException>(() => Mapper().Map(data, ProjectedAtUtc));
        var resolver = new Mock<IAnalyticsCustomerIdentityResolver>();
        resolver.Setup(value => value.Resolve(It.IsAny<OrderData>(), It.IsAny<JsonElement>()))
            .Returns(new AnalyticsCustomerIdentity(new string('t', 33), "id"));
        Assert.Throws<InvalidOperationException>(() => Mapper(resolver.Object).Map(Data(), ProjectedAtUtc));
    }

    [Fact]
    public void NestedJsonStringSnapshotsAreReadWithoutModelHydration()
    {
        var snapshot = Snapshot();
        foreach (var name in new[] { "StoreInfo", "CustomerInformation", "ShippingProvider", "PaymentProvider" })
            snapshot[name] = JsonValue.Create(snapshot[name]!.ToJsonString());
        var line = snapshot["OrderLines"]![0]!;
        line["Product"] = JsonValue.Create(line["Product"]!.ToJsonString());
        var data = Data(snapshot);
        data.CustomerEmail = "";
        var projection = Mapper().Map(data, ProjectedAtUtc);
        Assert.Equal("USD", projection.Order.CurrencyCode);
        Assert.Equal("Large", Assert.Single(projection.Lines).VariantTitle);
        Assert.Equal(Hash("email", "SNAPSHOT+TAG@EXAMPLE.COM"), projection.Order.CustomerIdentityKey);
        Assert.Equal(8m, projection.Order.ShippingAmountWithVat);
    }

    [Fact]
    public void AnalyticsOptionsAreSafeOptInAndValidationDoesNotThrow()
    {
        var options = new AnalyticsOptions();
        Assert.False(options.Enabled);
        Assert.True(options.ScheduledRefreshEnabled);
        Assert.Equal(TimeSpan.FromMinutes(30), options.RefreshInterval);
        Assert.Equal(TimeSpan.FromDays(7), options.LookbackWindow);
        Assert.Equal(250, options.BatchSize);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.DelayBetweenBatches);
        Assert.Equal(30, options.DatabaseCommandTimeoutSeconds);
        Assert.Equal(TimeSpan.FromMinutes(5), options.LeaseDuration);
        Assert.Equal(1, options.CustomerIdentityPolicyVersion);
        Assert.True(options.IsValid(out var error));
        Assert.Null(error);
        options.BatchSize = 0;
        Assert.False(options.IsValid(out error));
        Assert.Contains("BatchSize", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("RefreshInterval")]
    [InlineData("LookbackWindow")]
    [InlineData("DelayBetweenBatches")]
    [InlineData("DatabaseCommandTimeoutSeconds")]
    [InlineData("LeaseDuration")]
    [InlineData("CustomerIdentityPolicyVersion")]
    public void InvalidOptionsReturnAnError(string name)
    {
        var options = new AnalyticsOptions();
        var property = typeof(AnalyticsOptions).GetProperty(name)!;
        property.SetValue(options, property.PropertyType == typeof(TimeSpan) ? (object)TimeSpan.FromSeconds(-1) : 0);
        Assert.False(options.IsValid(out var error));
        Assert.Contains(name, error, StringComparison.Ordinal);
    }

    private static AnalyticsSnapshotMapper Mapper(IAnalyticsCustomerIdentityResolver? resolver = null, AnalyticsOptions? options = null) =>
        new(resolver ?? new EmailAnalyticsCustomerIdentityResolver(), Options.Create(options ?? new AnalyticsOptions()));

    private static string Hash(string type, string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(type + "\n" + value)));

    private static OrderData Data(JsonObject? snapshot = null) => new()
    {
        UniqueId = OrderId,
        ReferenceId = 101,
        OrderNumber = "ORDER-101",
        OrderInfo = (snapshot ?? Snapshot()).ToJsonString(),
        StoreAlias = "store",
        Currency = "en-US",
        OrderStatusCol = "ReadyForDispatch",
        CustomerEmail = "alice@example.com",
        CustomerName = "Alice",
        CustomerId = 17,
        CreateDate = new DateTime(2026, 10, 1, 9, 0, 0),
        PaidDate = new DateTime(2026, 10, 1, 9, 1, 0),
    };

    private static JsonObject Snapshot() => JsonNode.Parse("""
        {
          "GrandTotal": { "Value": 130 },
          "GrandTotalWithOutVat": { "Value": 104 },
          "ChargedAmount": { "Value": 110 },
          "OrderLineTotal": { "Value": 120 },
          "OrderLineTotalWithOutVat": { "Value": 96 },
          "DiscountAmount": { "Value": 30 },
          "DiscountAmountWithOutVat": { "Value": 24 },
          "StoreInfo": { "Alias": "store", "Currency": { "CurrencyValue": "en-US", "ISOCurrencySymbol": "USD" } },
          "CustomerInformation": { "Customer": { "Email": "snapshot+tag@example.com", "Name": "Snapshot", "UserId": 18, "Properties": { "customerEmail": "snapshot+tag@example.com" } } },
          "ShippingProvider": { "Key": "70000000-0000-0000-0000-000000000007", "Title": "Delivery", "Method": "Shipping", "Price": { "Value": 8, "WithVat": { "Value": 8 }, "WithoutVat": { "Value": 6.4 } } },
          "PaymentProvider": { "Key": "80000000-0000-0000-0000-000000000008", "Title": "Card", "Price": { "Value": 2, "WithVat": { "Value": 2 }, "WithoutVat": { "Value": 1.6 } } },
          "Discount": { "Key": "30000000-0000-0000-0000-000000000003", "Title": "20 percent", "Type": "Percentage", "Amount": 0.2 },
          "OrderLines": [
            {
              "Key": "20000000-0000-0000-0000-000000000002",
              "Quantity": 2.5,
              "Amount": { "Value": 120, "WithVat": { "Value": 120 }, "WithoutVat": { "Value": 96 }, "DiscountAmount": { "Value": 30 }, "BeforeDiscount": { "Value": 150 }, "AfterDiscount": { "Value": 120 }, "BeforeDiscountWithOutVat": { "Value": 120 }, "AfterDiscountWithOutVat": { "Value": 96 } },
              "Product": {
                "Key": "40000000-0000-0000-0000-000000000004", "Id": 42, "Title": "Product", "SKU": "P-1", "Price": { "Value": 9000 },
                "VariantGroups": [ { "Variants": [ { "Key": "50000000-0000-0000-0000-000000000005", "Id": 43, "Title": "Large", "SKU": "V-1", "Price": { "Value": 8000 } } ] } ]
              }
            }
          ]
        }
        """)!.AsObject();
}
