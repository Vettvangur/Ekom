using Ekom.Models;
using Ekom.Services;
using Ekom.Tests.Objects;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Ekom.Tests.Tests;

public class DiscountCalculationTests
{
    [Theory]
    [InlineData(DiscountType.Fixed, 2000, 2000)]
    [InlineData(DiscountType.Percentage, 20, 0.2)]
    [InlineData(DiscountType.Fixed, 0, 0)]
    [InlineData(DiscountType.Percentage, -20, 0)]
    public void ProductDiscount_UsesTypeSpecificAmountNormalization(
        DiscountType type,
        decimal configuredAmount,
        decimal expectedAmount)
    {
        using var configurationScope = new ConfigurationScope();
        var discount = new TestProductDiscount(Stores.Store_US_0Vat_VatIncluded, type, configuredAmount);

        Assert.Equal(expectedAmount, discount.Amount);
    }

    [Fact]
    public void SelectBestDiscount_ChoosesPercentageWhenItSavesMoreThanFixed()
    {
        var fixedDiscount = CreateProductDiscount(DiscountType.Fixed, 10m);
        var percentageDiscount = CreateProductDiscount(DiscountType.Percentage, 0.2m);

        var result = ProductDiscountService.SelectBestDiscount(
            [fixedDiscount.Object, percentageDiscount.Object],
            100m);

        Assert.Same(percentageDiscount.Object, result);
    }

    [Fact]
    public void SelectBestDiscount_ChoosesFixedWhenItSavesMoreThanPercentage()
    {
        var fixedDiscount = CreateProductDiscount(DiscountType.Fixed, 30m);
        var percentageDiscount = CreateProductDiscount(DiscountType.Percentage, 0.2m);

        var result = ProductDiscountService.SelectBestDiscount(
            [fixedDiscount.Object, percentageDiscount.Object],
            100m);

        Assert.Same(fixedDiscount.Object, result);
    }

    [Fact]
    public void SelectBestDiscount_IgnoresNonPositiveDiscounts()
    {
        var zeroDiscount = CreateProductDiscount(DiscountType.Fixed, 0m);
        var negativeDiscount = CreateProductDiscount(DiscountType.Percentage, -0.2m);

        var result = ProductDiscountService.SelectBestDiscount(
            [zeroDiscount.Object, negativeDiscount.Object],
            100m);

        Assert.Null(result);
    }

    [Fact]
    public void SelectDiscount_UsesMonetaryValueForMixedDiscountTypes()
    {
        using var configurationScope = new ConfigurationScope();
        var currency = new CurrencyModel
        {
            CurrencyValue = "en-US",
            CurrencyFormat = "C",
        };
        var productDiscount = CreateOrderedDiscount(DiscountType.Fixed, 10m);
        var orderDiscount = CreateOrderedDiscount(DiscountType.Percentage, 0.2m);
        var variantPrice = new Price(100m, currency, 0m, true, productDiscount);

        var result = OrderLine.SelectDiscount(variantPrice, orderDiscount, 0m, true, 1m);

        Assert.Same(orderDiscount, result);
    }

    [Fact]
    public void SelectDiscount_RetainsProductDiscountWhenItSavesMore()
    {
        using var configurationScope = new ConfigurationScope();
        var currency = new CurrencyModel
        {
            CurrencyValue = "en-US",
            CurrencyFormat = "C",
        };
        var productDiscount = CreateOrderedDiscount(DiscountType.Percentage, 0.2m);
        var orderDiscount = CreateOrderedDiscount(DiscountType.Fixed, 10m);
        var variantPrice = new Price(100m, currency, 0m, true, productDiscount);

        var result = OrderLine.SelectDiscount(variantPrice, orderDiscount, 0m, true, 1m);

        Assert.Same(productDiscount, result);
    }

    [Fact]
    public void IsBetterLineDiscount_AppliesStackableDiscountToDiscountedPrice()
    {
        using var configurationScope = new ConfigurationScope();
        var currency = new CurrencyModel
        {
            CurrencyValue = "en-US",
            CurrencyFormat = "C",
        };
        var productDiscount = CreateOrderedDiscount(DiscountType.Fixed, 20m);
        var stackableDiscount = CreateOrderedDiscount(DiscountType.Fixed, 5m, true);
        var price = new Price(100m, currency, 0m, true, productDiscount);

        bool result = DiscountValueCalculator.IsBetterLineDiscount(
            price,
            stackableDiscount,
            productDiscount,
            0m,
            true,
            1m);

        Assert.True(result);
    }

    private static Mock<IProductDiscount> CreateProductDiscount(DiscountType type, decimal amount)
    {
        var discount = new Mock<IProductDiscount>();
        discount.SetupGet(item => item.Type).Returns(type);
        discount.SetupGet(item => item.Amount).Returns(amount);
        discount.SetupGet(item => item.StartOfRange).Returns(0m);
        discount.SetupGet(item => item.EndOfRange).Returns(0m);
        return discount;
    }

    private static OrderedDiscount CreateOrderedDiscount(DiscountType type, decimal amount, bool stackable = false)
        => new(
            Guid.NewGuid(),
            "Test discount",
            stackable,
            amount,
            type,
            [],
            [],
            null!,
            false,
            false);

    private sealed class TestProductDiscount : ProductDiscount
    {
        private readonly DiscountType _type;

        internal TestProductDiscount(IStore store, DiscountType type, decimal configuredAmount)
            : base(store)
        {
            _type = type;
            _properties["discount"] = JsonConvert.SerializeObject(new[]
            {
                new CurrencyValue(configuredAmount, store.Currencies[0].CurrencyValue),
            });
        }

        public override DiscountType Type => _type;
    }
}
