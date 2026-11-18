using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Xunit;

namespace Ekom.Tests.Tests;

public class BasketStockPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void BackorderOnTheProductOrOnTheVariantAllowsSellingBeyondStock(bool onProduct, bool onVariant, bool expected)
    {
        var product = Mock.Of<IProduct>(x => x.Backorder == onProduct);
        var variant = Mock.Of<IVariant>(x => x.Backorder == onVariant);

        Assert.Equal(expected, StockBufferHelper.AllowsBackorder(product, variant));
        Assert.Equal(onProduct, StockBufferHelper.AllowsBackorder(product));
    }

    [Theory]
    [InlineData(true, 5, 6, true)]   // the policy needs stock and there is too little
    [InlineData(true, 5, 5, false)]  // the policy needs stock and there is enough
    [InlineData(false, 5, 6, false)] // the policy exempts the line
    public void AddingToTheBasketAsksTheSameStockPolicyAsCheckout(bool requiresStock, decimal inStock, decimal quantity, bool rejected)
    {
        var order = Mock.Of<IOrderInfo>();
        var line = Mock.Of<IOrderLine>();
        var policy = new Mock<ICheckoutStockPolicy>();
        policy.Setup(x => x.RequiresStock(order, line)).Returns(requiresStock);
        var product = new Mock<IProduct>();
        product.SetupGet(x => x.CategoryAncestors).Returns(new List<ICategory>());

        var failure = Record.Exception(() => VerifyStock(policy.Object, quantity, inStock, product.Object, order, line));

        if (rejected) Assert.IsType<NotEnoughStockException>(failure);
        else Assert.Null(failure);
        policy.Verify(x => x.RequiresStock(order, line), Times.Once);
    }

    [Fact]
    public void SynchronousProviderListAlsoAppliesRulesRegisteredOnTheAsyncEvent()
    {
        var hidden = Mock.Of<IShippingProvider>();
        var shown = Mock.Of<IShippingProvider>();
        Task HideOne(object sender, ProviderEvents.ShippingProvidersEventArgs args, CancellationToken ct)
        {
            args.Providers = args.Providers.Where(x => !ReferenceEquals(x, hidden)).ToList();
            return Task.CompletedTask;
        }
        ProviderEvents.BeforeReturnShippingProvidersAsync += HideOne;
        try
        {
            var result = ProviderEvents.RaiseOnBeforeReturnShippingProviders([hidden, shown], "Store");

            Assert.Equal([shown], result);
        }
        finally
        {
            ProviderEvents.BeforeReturnShippingProvidersAsync -= HideOne;
        }
    }

    [Fact]
    public void SynchronousPaymentProviderListAlsoAppliesRulesRegisteredOnTheAsyncEvent()
    {
        var hidden = Mock.Of<IPaymentProvider>();
        var shown = Mock.Of<IPaymentProvider>();
        Task HideOne(object sender, ProviderEvents.PaymentProvidersEventArgs args, CancellationToken ct)
        {
            args.Providers = args.Providers.Where(x => !ReferenceEquals(x, hidden)).ToList();
            return Task.CompletedTask;
        }
        ProviderEvents.BeforeReturnPaymentProvidersAsync += HideOne;
        try
        {
            var result = ProviderEvents.RaiseOnBeforeReturnPaymentProviders([hidden, shown], "Store");

            Assert.Equal([shown], result);
        }
        finally
        {
            ProviderEvents.BeforeReturnPaymentProvidersAsync -= HideOne;
        }
    }

    // VerifyStock is the private check every basket change goes through.
    private static void VerifyStock(ICheckoutStockPolicy policy, decimal quantity, decimal inStock, IProduct product, IOrderInfo order, IOrderLine line)
    {
        var service = (OrderService)RuntimeHelpers.GetUninitializedObject(typeof(OrderService));
        SetField(service, "_config", new Configuration(new ConfigurationBuilder().Build()));
        SetField(service, "_stockPolicy", policy);
        try
        {
            typeof(OrderService).GetMethod("VerifyStock", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(service, [quantity, inStock, product, null, order, line]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }

    private static void SetField(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
