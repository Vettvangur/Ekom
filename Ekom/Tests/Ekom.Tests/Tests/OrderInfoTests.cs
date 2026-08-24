using Ekom.Models;
using Ekom.Tests.Objects;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ekom.Tests.Tests;

public class OrderInfoTests
{
    [Fact]
    public void Culture_UsesRequestCultureAndRetainsItWithoutARequest()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.Features.Set<IRequestCultureFeature>(
            new RequestCultureFeature(new RequestCulture("da-DK"), null));

        using var configurationScope = new ConfigurationScope(addServices: services =>
            services.AddSingleton<IHttpContextAccessor>(httpContextAccessor));
        var store = new Mock<IStore>();
        store.SetupGet(x => x.Culture).Returns(new CultureInfoDto { Name = "en-US" });
        var orderInfo = new OrderInfo(new OrderData(), store.Object);

        Assert.Equal("da-DK", orderInfo.Culture);

        httpContextAccessor.HttpContext = null;

        Assert.Equal("da-DK", orderInfo.Culture);
    }

    [Fact]
    public void Culture_DoesNotOverrideResolvedCultureWithRequestCulture()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.Features.Set<IRequestCultureFeature>(
            new RequestCultureFeature(new RequestCulture("is-IS"), null));

        using var configurationScope = new ConfigurationScope(addServices: services =>
            services.AddSingleton<IHttpContextAccessor>(httpContextAccessor));
        var store = new Mock<IStore>();
        store.SetupGet(x => x.Culture).Returns(new CultureInfoDto { Name = "fi-FI" });
        var orderInfo = new OrderInfo(new OrderData(), store.Object)
        {
            Culture = "fi-FI",
        };

        Assert.Equal("fi-FI", orderInfo.Culture);
    }

    [Fact]
    public void OrderedPaymentProvider_SerializesOnlyActivePrice()
    {
        using var configurationScope = new ConfigurationScope();
        var currency = new CurrencyModel
        {
            CurrencyFormat = "C",
            CurrencyValue = "en-US",
        };
        var storeInfo = new StoreInfo(
            Guid.NewGuid(),
            currency,
            [currency],
            "en-US",
            "Store2",
            vatIncludedInPrice: true,
            vat: 0.11m,
            applyVatOnShipping: true);
        var price = new Price(25m, currency, storeInfo.Vat, storeInfo.VatIncludedInPrice);
        var providerJson = new JObject
        {
            ["Id"] = 1,
            ["Key"] = Guid.NewGuid(),
            ["Title"] = "Test payment",
            ["Price"] = JToken.FromObject(price),
        };

        var provider = new OrderedPaymentProvider(providerJson, storeInfo);
        var serializedProvider = JObject.Parse(JsonConvert.SerializeObject(provider, EkomJsonDotNet.Settings));

        Assert.Null(serializedProvider["Prices"]);
        Assert.Equal("en-US", serializedProvider["Price"]?["Currency"]?["CurrencyValue"]?.Value<string>());
    }

    [Fact]
    public void Constructor_PreservesLegacyTopLevelDiscountObjectAmountValue()
    {
        JObject orderInfoJson = new JObject
        {
            [nameof(OrderInfo.Culture)] = "en-US",
            [nameof(OrderInfo.OrderLines)] = new JArray(),
            [nameof(OrderInfo.Discount)] = CreateDiscountJson(),
        };
        ((JObject)orderInfoJson[nameof(OrderInfo.Discount)]!)[nameof(OrderedDiscount.Amount)] = new JObject
        {
            ["Value"] = 12.34m,
        };
        var orderData = new OrderData
        {
            OrderInfo = orderInfoJson.ToString(),
        };

        var orderInfo = new OrderInfo(orderData);

        Assert.NotNull(orderInfo.Discount);
        Assert.Equal(12.34m, orderInfo.Discount.Amount);
    }

    [Fact]
    public void CreateOrderedDiscountFromJson_PreservesDecimalAmount()
    {
        JObject discountJson = CreateDiscountJson();

        OrderedDiscount? discount = CreateOrderedDiscountFromJson(discountJson);

        Assert.NotNull(discount);
        Assert.Equal(10m, discount.Amount);
    }

    [Fact]
    public void CreateOrderedDiscountFromJson_PreservesLegacyObjectAmountValue()
    {
        JObject discountJson = CreateDiscountJson();
        discountJson[nameof(OrderedDiscount.Amount)] = new JObject
        {
            ["Value"] = 12.34m,
        };

        OrderedDiscount? discount = CreateOrderedDiscountFromJson(discountJson);

        Assert.NotNull(discount);
        Assert.Equal(12.34m, discount.Amount);
    }

    [Fact]
    public void CreateOrderedDiscountFromJson_ReturnsNullForUnreadableObjectAmount()
    {
        JObject discountJson = CreateDiscountJson();
        discountJson[nameof(OrderedDiscount.Amount)] = new JObject
        {
            ["Currency"] = "ISK",
        };

        OrderedDiscount? discount = CreateOrderedDiscountFromJson(discountJson);

        Assert.Null(discount);
    }

    [Fact]
    public void Constructor_DeserializesGiftcards()
    {
        var validUntil = DateTime.UtcNow.AddDays(1);
        var usedDate = DateTime.UtcNow.AddDays(-1);
        var claimDate = DateTime.UtcNow;
        var orderData = new OrderData
        {
            OrderInfo = new JObject
            {
                [nameof(OrderInfo.Giftcards)] = new JArray
                {
                    new JObject
                    {
                        [nameof(Giftcard.Amount)] = 25m,
                        [nameof(Giftcard.Code)] = "giftcard-code",
                        [nameof(Giftcard.UsedDate)] = usedDate,
                        [nameof(Giftcard.ClaimId)] = "claim-id",
                        [nameof(Giftcard.TransactionId)] = "transaction-id",
                        [nameof(Giftcard.ClaimDate)] = claimDate,
                        [nameof(Giftcard.Claimed)] = true,
                        [nameof(Giftcard.ValidUntil)] = validUntil,
                    },
                    new JObject
                    {
                        [nameof(Giftcard.Amount)] = 10m,
                        [nameof(Giftcard.Code)] = "second-giftcard-code",
                    },
                },
                [nameof(OrderInfo.OrderLines)] = new JArray(),
            }.ToString(),
        };

        var orderInfo = new OrderInfo(orderData);

        Assert.Equal(2, orderInfo.Giftcards.Count);
        Giftcard giftcard = orderInfo.Giftcards[0];
        Assert.Equal(25m, giftcard.Amount);
        Assert.Equal("giftcard-code", giftcard.Code);
        Assert.Equal(usedDate, giftcard.UsedDate);
        Assert.Equal("claim-id", giftcard.ClaimId);
        Assert.Equal("transaction-id", giftcard.TransactionId);
        Assert.Equal(claimDate, giftcard.ClaimDate);
        Assert.True(giftcard.Claimed);
        Assert.Equal(validUntil, giftcard.ValidUntil);
    }

    [Fact]
    public void Giftcards_ReducePayableTotalsWhenValid()
    {
        using var configurationScope = new ConfigurationScope();
        var orderInfo = CreateOrderInfoWithPaymentAmount(100m);
        orderInfo.Giftcards =
        [
            new Giftcard { Amount = 25m, Code = "giftcard-code", ValidUntil = DateTime.UtcNow.AddDays(1) },
            new Giftcard { Amount = 10m, Code = "second-giftcard-code" },
        ];

        Assert.Equal(65m, orderInfo.ChargedAmount.Value);
        Assert.Equal(65m, orderInfo.GrandTotal.Value);
        Assert.Equal(65m, orderInfo.GrandTotalWithOutVat.Value);
    }

    [Fact]
    public void UnclaimedGiftcards_DoNotReducePayableTotalsWhenExpired()
    {
        using var configurationScope = new ConfigurationScope();
        var orderInfo = CreateOrderInfoWithPaymentAmount(100m);
        orderInfo.Giftcards =
        [
            new Giftcard { Amount = 25m, Code = "giftcard-code", ValidUntil = DateTime.UtcNow.AddTicks(-1) },
            new Giftcard { Amount = 10m, Code = "second-giftcard-code", ValidUntil = DateTime.UtcNow.AddTicks(-1) },
        ];

        Assert.Equal(100m, orderInfo.ChargedAmount.Value);
        Assert.Equal(100m, orderInfo.GrandTotal.Value);
        Assert.Equal(100m, orderInfo.GrandTotalWithOutVat.Value);
    }

    [Fact]
    public void ClaimedGiftcards_ReducePayableTotalsAfterValidUntil()
    {
        using var configurationScope = new ConfigurationScope();
        var orderInfo = CreateOrderInfoWithPaymentAmount(100m);
        orderInfo.Giftcards =
        [
            new Giftcard { Amount = 25m, Code = "giftcard-code", Claimed = true, ValidUntil = DateTime.UtcNow.AddTicks(-1) },
            new Giftcard { Amount = 10m, Code = "second-giftcard-code", Claimed = true, ValidUntil = DateTime.UtcNow.AddTicks(-1) },
        ];

        Assert.Equal(65m, orderInfo.ChargedAmount.Value);
        Assert.Equal(65m, orderInfo.GrandTotal.Value);
        Assert.Equal(65m, orderInfo.GrandTotalWithOutVat.Value);
    }

    [Fact]
    public void Giftcards_WithoutExpiryCannotReducePayableTotalsBelowZero()
    {
        using var configurationScope = new ConfigurationScope();
        var orderInfo = CreateOrderInfoWithPaymentAmount(100m);
        orderInfo.Giftcards =
        [
            new Giftcard { Amount = 100m, Code = "giftcard-code" },
            new Giftcard { Amount = 50m, Code = "second-giftcard-code" },
        ];

        Assert.Equal(0m, orderInfo.ChargedAmount.Value);
        Assert.Equal(0m, orderInfo.GrandTotal.Value);
        Assert.Equal(0m, orderInfo.GrandTotalWithOutVat.Value);
    }

    private static OrderInfo CreateOrderInfoWithPaymentAmount(decimal amount)
    {
        var currency = new CurrencyModel
        {
            CurrencyFormat = "C",
            CurrencyValue = "en-US",
        };
        var storeInfo = new StoreInfo(
            Guid.NewGuid(),
            currency,
            [currency],
            "en-US",
            "Store",
            vatIncludedInPrice: true,
            vat: 0,
            applyVatOnShipping: false);
        var paymentProvider = new JObject
        {
            ["Id"] = 1,
            ["Key"] = Guid.NewGuid(),
            ["Title"] = "Test payment",
            ["Price"] = JToken.FromObject(new Price(amount, currency, 0, vatIncludedInPrice: true)),
        };
        var orderData = new OrderData
        {
            OrderInfo = new JObject
            {
                [nameof(OrderInfo.StoreInfo)] = JToken.FromObject(storeInfo),
                [nameof(OrderInfo.OrderLines)] = new JArray(),
                [nameof(OrderInfo.PaymentProvider)] = paymentProvider,
            }.ToString(),
        };

        return new OrderInfo(orderData);
    }

    private static OrderedDiscount? CreateOrderedDiscountFromJson(JToken discountJson)
    {
        MethodInfo method = typeof(OrderInfo).GetMethod(
            "CreateOrderedDiscountFromJson",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        return (OrderedDiscount?)method.Invoke(null, new object[] { discountJson });
    }

    private static JObject CreateDiscountJson()
    {
        return new JObject
        {
            [nameof(OrderedDiscount.Key)] = Guid.NewGuid(),
            [nameof(OrderedDiscount.Title)] = "Test discount",
            [nameof(OrderedDiscount.Stackable)] = true,
            [nameof(OrderedDiscount.Amount)] = 10m,
            [nameof(OrderedDiscount.Type)] = DiscountType.Fixed.ToString(),
            [nameof(OrderedDiscount.DiscountItems)] = new JArray(),
            [nameof(OrderedDiscount.ExcludeDiscountItems)] = new JArray(),
            [nameof(OrderedDiscount.Constraints)] = null,
            [nameof(OrderedDiscount.HasMasterStock)] = false,
            [nameof(OrderedDiscount.GlobalDiscount)] = false,
        };
    }
}
