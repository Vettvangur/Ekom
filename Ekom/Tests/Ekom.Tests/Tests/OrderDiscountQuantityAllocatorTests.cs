using Ekom.Models;
using Ekom.Services;
using Ekom.Tests.Objects;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class OrderDiscountQuantityAllocatorTests
{
    [Fact]
    public void Repeating_CombinesWholeQuantitiesAndAllocatesCheapestFirst()
    {
        var cheapLine = CreateLine("10", 2.5m, 50m);
        var expensiveLine = CreateLine("20", 4m, 100m);
        var order = CreateOrder(cheapLine.Object, expensiveLine.Object);
        var discount = CreateDiscount(OrderDiscountQuantityMode.Repeating, 3, 1, ["10", "20"], ["10", "20"]);

        var result = OrderDiscountQuantityAllocator.Allocate(order.Object, discount);

        Assert.Equal(2m, result[cheapLine.Object.Key]);
        Assert.False(result.ContainsKey(expensiveLine.Object.Key));
    }

    [Fact]
    public void Threshold_DiscountsAllWholeRewardUnits()
    {
        var firstLine = CreateLine("10", 1.5m, 50m);
        var secondLine = CreateLine("20", 2.75m, 100m);
        var order = CreateOrder(firstLine.Object, secondLine.Object);
        var discount = CreateDiscount(OrderDiscountQuantityMode.Threshold, 3, 0, ["10", "20"], ["10", "20"]);

        var result = OrderDiscountQuantityAllocator.Allocate(order.Object, discount);

        Assert.Equal(1m, result[firstLine.Object.Key]);
        Assert.Equal(2m, result[secondLine.Object.Key]);
    }

    [Fact]
    public void QualifyingSelectors_DoNotDoubleCountALine()
    {
        var line = CreateLine("1,10", 3m, 50m);
        var order = CreateOrder(line.Object);
        var discount = CreateDiscount(OrderDiscountQuantityMode.Repeating, 4, 1, ["1", "10"], ["10"]);

        var result = OrderDiscountQuantityAllocator.Allocate(order.Object, discount);

        Assert.Empty(result);
    }

    [Fact]
    public void Repeating_RequiresPositiveRewardQuantity()
    {
        var discount = CreateDiscount(OrderDiscountQuantityMode.Repeating, 3, 0, ["10"], ["10"]);

        Assert.False(OrderDiscountQuantityAllocator.IsValid(discount));
    }

    [Fact]
    public void Repeating_UsesProvidedEffectiveUnitPricesForAllocation()
    {
        var firstLine = CreateLine("10", 1m, 10m);
        var secondLine = CreateLine("20", 1m, 20m);
        var order = CreateOrder(firstLine.Object, secondLine.Object);
        var discount = CreateDiscount(OrderDiscountQuantityMode.Repeating, 2, 1, ["10", "20"], ["10", "20"]);
        var effectiveUnitPrices = new Dictionary<Guid, decimal>
        {
            [firstLine.Object.Key] = 30m,
            [secondLine.Object.Key] = 5m,
        };

        var result = OrderDiscountQuantityAllocator.Allocate(
            order.Object,
            discount,
            effectiveUnitPrices: effectiveUnitPrices);

        Assert.False(result.ContainsKey(firstLine.Object.Key));
        Assert.Equal(1m, result[secondLine.Object.Key]);
    }

    [Fact]
    public void Repeating_ReassignsRewardWhenProductDiscountIsStronger()
    {
        using var configurationScope = new ConfigurationScope();
        var productDiscount = CreateDiscount(OrderDiscountQuantityMode.None, 0, 0, [], []);
        productDiscount.Amount = 0.3m;
        var cheapLine = CreateLine("10", 1m, 100m, productDiscount: productDiscount);
        var fallbackLine = CreateLine("20", 1m, 200m);
        var order = CreateOrder(cheapLine.Object, fallbackLine.Object);
        var discount = CreateDiscount(OrderDiscountQuantityMode.Repeating, 2, 1, ["10", "20"], ["10", "20"]);

        var result = OrderDiscountQuantityAllocator.Allocate(order.Object, discount);

        Assert.False(result.ContainsKey(cheapLine.Object.Key));
        Assert.Equal(1m, result[fallbackLine.Object.Key]);
    }

    [Fact]
    public void Repeating_ReassignsRewardWhenProductDiscountIsEqual()
    {
        using var configurationScope = new ConfigurationScope();
        var productDiscount = CreateDiscount(OrderDiscountQuantityMode.None, 0, 0, [], []);
        var cheapLine = CreateLine("10", 1m, 100m, productDiscount: productDiscount);
        var fallbackLine = CreateLine("20", 1m, 200m);
        var order = CreateOrder(cheapLine.Object, fallbackLine.Object);
        var discount = CreateDiscount(OrderDiscountQuantityMode.Repeating, 2, 1, ["10", "20"], ["10", "20"]);

        var result = OrderDiscountQuantityAllocator.Allocate(order.Object, discount);

        Assert.False(result.ContainsKey(cheapLine.Object.Key));
        Assert.Equal(1m, result[fallbackLine.Object.Key]);
    }

    [Fact]
    public void Threshold_DoesNotAllocateWhenRequiredQuantityIsNotMet()
    {
        var line = CreateLine("10", 2.9m, 50m);
        var order = CreateOrder(line.Object);
        var discount = CreateDiscount(OrderDiscountQuantityMode.Threshold, 3, 0, ["10"], ["10"]);

        var result = OrderDiscountQuantityAllocator.Allocate(order.Object, discount);

        Assert.Empty(result);
    }

    [Fact]
    public void Repeating_ExcludesRewardItemsWithoutRemovingQualifiers()
    {
        var excludedLine = CreateLine("10", 3m, 25m);
        var rewardLine = CreateLine("20", 2m, 50m);
        var order = CreateOrder(excludedLine.Object, rewardLine.Object);
        var discount = CreateDiscount(
            OrderDiscountQuantityMode.Repeating,
            3,
            1,
            ["10"],
            ["10", "20"],
            ["10"]);

        var result = OrderDiscountQuantityAllocator.Allocate(order.Object, discount);

        Assert.False(result.ContainsKey(excludedLine.Object.Key));
        Assert.Equal(1m, result[rewardLine.Object.Key]);
    }

    [Fact]
    public void Repeating_DisabledProductDoesNotQualifyOrReceiveReward()
    {
        var disabledQualifier = CreateLine("10", 3m, 25m, disableDiscounts: true);
        var rewardLine = CreateLine("20", 1m, 50m);
        var order = CreateOrder(disabledQualifier.Object, rewardLine.Object);
        var discount = CreateDiscount(
            OrderDiscountQuantityMode.Repeating,
            3,
            1,
            ["10"],
            ["10", "20"]);

        var result = OrderDiscountQuantityAllocator.Allocate(order.Object, discount);

        Assert.Empty(result);
    }

    [Fact]
    public void None_DisabledProductDoesNotReceiveDiscount()
    {
        var disabledLine = CreateLine("10", 1m, 25m, disableDiscounts: true);
        var eligibleLine = CreateLine("20", 1m, 50m);
        var order = CreateOrder(disabledLine.Object, eligibleLine.Object);
        var discount = CreateDiscount(OrderDiscountQuantityMode.None, 0, 0, [], ["10", "20"]);

        var result = OrderDiscountQuantityAllocator.Allocate(order.Object, discount);

        Assert.False(result.ContainsKey(disabledLine.Object.Key));
        Assert.Equal(1m, result[eligibleLine.Object.Key]);
    }

    [Fact]
    public void OrderConstraints_ExcludeDisabledProductsFromMinimumSpend()
    {
        var disabledLine = CreateLine("10", 1m, 100m, disableDiscounts: true);
        var eligibleLine = CreateLine("20", 1m, 25m);
        var order = CreateOrder(disabledLine.Object, eligibleLine.Object);
        var discount = CreateDiscount(OrderDiscountQuantityMode.None, 0, 0, [], ["20"]);
        var constraints = new Mock<IConstraints>();
        constraints.Setup(x => x.IsValid("en-US", It.IsAny<decimal>()))
            .Returns((string _, decimal amount) => amount >= 50m);
        discount.Constraints = constraints.Object;

        var result = DiscountApplicability.AreOrderConstraintsMet(order.Object, discount);

        Assert.False(result);
        constraints.Verify(x => x.IsValid("en-US", 25m), Times.Once);
    }

    [Fact]
    public void Applicability_DisabledProductNeverMatchesDiscount()
    {
        var disabledLine = CreateLine("10", 1m, 25m, disableDiscounts: true);
        var order = CreateOrder(disabledLine.Object);
        var discount = CreateDiscount(OrderDiscountQuantityMode.None, 0, 0, [], ["10"]);

        var result = DiscountApplicability.IsDiscountApplicable(
            order.Object,
            disabledLine.Object,
            discount);

        Assert.False(result);
    }

    [Fact]
    public void OrderedProduct_DisabledProductRemovesPersistedPriceDiscount()
    {
        var productDiscount = CreateDiscount(OrderDiscountQuantityMode.None, 0, 0, [], []);

        var line = CreateLine(
            "10",
            1m,
            25m,
            productDiscount: productDiscount,
            disableDiscounts: true);

        Assert.False(line.Object.Product.Price.HasDiscount);
        Assert.Null(line.Object.Product.ProductDiscount);
    }

    [Fact]
    public void OrderedProduct_DisabledProductDoesNotMutateDynamicPrices()
    {
        var storeInfo = CreateStoreInfo();
        var discount = CreateDiscount(OrderDiscountQuantityMode.None, 0, 0, [], []);
        var dynamicPrice = new Price(25m, storeInfo.Currency, 0, true, discount);
        var dynamicPrices = new List<IPrice> { dynamicPrice };
        var product = new Mock<IProduct>();
        product.SetupGet(x => x.DisableDiscounts).Returns(true);
        product.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>
        {
            ["__Key"] = Guid.NewGuid().ToString(),
            ["__Path"] = "10",
        });
        product.SetupGet(x => x.Prices).Returns(dynamicPrices);
        var request = new OrderDynamicRequest { Prices = dynamicPrices };

        var orderedProduct = new OrderedProduct(product.Object, null, storeInfo, request);

        Assert.True(dynamicPrice.HasDiscount);
        Assert.Same(dynamicPrice, Assert.Single(dynamicPrices));
        Assert.False(orderedProduct.Price.HasDiscount);
    }

    [Fact]
    public void PricingScope_ReusesAllocationForNestedCalculations()
    {
        var orderLineReads = 0;
        var order = new Mock<IOrderInfo>();
        order.SetupGet(x => x.OrderLines).Returns(() =>
        {
            orderLineReads++;
            return Array.Empty<IOrderLine>();
        });
        var discount = CreateDiscount(OrderDiscountQuantityMode.None, 0, 0, [], []);
        order.SetupGet(x => x.Discount).Returns(discount);

        using (var scope = OrderPricingCalculationScope.Enter(order.Object))
        {
            _ = scope.Allocations;
            _ = scope.Allocations;

            using (var nestedScope = OrderPricingCalculationScope.Enter(order.Object))
            {
                _ = nestedScope.Allocations;
            }
        }

        Assert.Equal(1, orderLineReads);
    }

    [Fact]
    public void PricingScope_RestoresOuterAllocationAfterDifferentNestedCalculation()
    {
        var orderLineReads = 0;
        var order = new Mock<IOrderInfo>();
        order.SetupGet(x => x.OrderLines).Returns(() =>
        {
            orderLineReads++;
            return Array.Empty<IOrderLine>();
        });
        var outerDiscount = CreateDiscount(OrderDiscountQuantityMode.None, 0, 0, [], []);
        var innerDiscount = CreateDiscount(OrderDiscountQuantityMode.None, 0, 0, [], []);
        OrderedDiscount? currentDiscount = outerDiscount;
        order.SetupGet(x => x.Discount).Returns(() => currentDiscount);

        using (var outerScope = OrderPricingCalculationScope.Enter(order.Object))
        {
            _ = outerScope.Allocations;
            currentDiscount = innerDiscount;

            using (var innerScope = OrderPricingCalculationScope.Enter(order.Object))
            {
                _ = innerScope.Allocations;
            }

            currentDiscount = outerDiscount;
            using var restoredScope = OrderPricingCalculationScope.Enter(order.Object);
            _ = restoredScope.Allocations;
        }

        Assert.Equal(2, orderLineReads);
    }

    [Fact]
    public void InvalidQuantityRule_DoesNotReadOrderLines()
    {
        var order = new Mock<IOrderInfo>();
        var discount = CreateDiscount(OrderDiscountQuantityMode.Repeating, 3, 0, ["10"], ["10"]);

        var result = OrderDiscountQuantityAllocator.Allocate(order.Object, discount);

        Assert.Empty(result);
        order.VerifyGet(x => x.OrderLines, Times.Never);
    }

    [Fact]
    public void CategoryTargets_AreResolvedOncePerDistinctCategory()
    {
        var firstLine = CreateLine("10", 1m, 10m, "100");
        var secondLine = CreateLine("20", 1m, 20m, "100");
        var order = CreateOrder(firstLine.Object, secondLine.Object);
        var discount = CreateDiscount(OrderDiscountQuantityMode.Repeating, 2, 1, ["100"], ["100"]);
        var nodeService = new Mock<INodeService>();
        nodeService.Setup(x => x.NodeById("100", false)).Returns(new UmbracoContent { Path = "1,100" });

        var result = OrderDiscountQuantityAllocator.Allocate(order.Object, discount, nodeService.Object);

        Assert.Single(result);
        nodeService.Verify(x => x.NodeById("100", false), Times.Once);
    }

    private static Mock<IOrderInfo> CreateOrder(params IOrderLine[] lines)
    {
        var order = new Mock<IOrderInfo>();
        order.SetupGet(x => x.OrderLines).Returns(lines);
        order.SetupGet(x => x.StoreInfo).Returns(CreateStoreInfo());
        return order;
    }

    private static Mock<IOrderLine> CreateLine(
        string path,
        decimal quantity,
        decimal unitPrice,
        string? categories = null,
        OrderedDiscount? productDiscount = null,
        bool disableDiscounts = false)
    {
        var storeInfo = CreateStoreInfo();
        var currency = storeInfo.Currency;
        var product = new Mock<IProduct>();
        var properties = new Dictionary<string, string>
        {
            ["__Key"] = Guid.NewGuid().ToString(),
            ["__Path"] = path,
        };
        if (categories != null)
        {
            properties["categories"] = categories;
        }

        product.SetupGet(x => x.Properties).Returns(properties);
        product.SetupGet(x => x.DisableDiscounts).Returns(disableDiscounts);
        product.SetupGet(x => x.Prices).Returns([new Price(unitPrice, currency, 0, true, productDiscount)]);
        var orderedProduct = new OrderedProduct(product.Object, null, storeInfo);
        var amount = new Mock<IPrice>();
        amount.SetupGet(x => x.Value).Returns(unitPrice * quantity);
        var line = new Mock<IOrderLine>();
        line.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        line.SetupGet(x => x.Product).Returns(orderedProduct);
        line.SetupGet(x => x.Quantity).Returns(quantity);
        line.SetupGet(x => x.Amount).Returns(amount.Object);
        return line;
    }

    private static StoreInfo CreateStoreInfo()
    {
        var currency = new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" };
        return new StoreInfo(Guid.NewGuid(), currency, [currency], "en-US", "main", true, 0, false);
    }

    private static OrderedDiscount CreateDiscount(
        OrderDiscountQuantityMode mode,
        int requiredQuantity,
        int rewardQuantity,
        List<string> qualifyingItems,
        List<string> discountItems,
        List<string>? excludeDiscountItems = null)
        => new(
            Guid.NewGuid(),
            "Quantity discount",
            false,
            0.2m,
            DiscountType.Percentage,
            discountItems,
            excludeDiscountItems ?? [],
            new Constraints(),
            false,
            false,
            mode,
            qualifyingItems,
            requiredQuantity,
            rewardQuantity);
}
