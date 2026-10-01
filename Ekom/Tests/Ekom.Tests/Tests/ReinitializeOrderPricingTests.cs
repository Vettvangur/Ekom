using Ekom.API;
using Ekom.Cache;
using Ekom.Events;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Tracking;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class ReinitializeOrderPricingTests
{
    [Fact]
    public async Task RefreshUsesFreshProductAndVariantPricesDespiteZeroStockAndSavesOnce()
    {
        using var fixture = new Fixture();
        var product = fixture.AddProduct(10);
        var variantProduct = fixture.AddProduct(20);
        var variant = fixture.AddVariant(variantProduct, 30);
        var plainLine = fixture.AddLine(product, 2);
        var variantLine = fixture.AddLine(variantProduct, 3, variant);
        await fixture.SaveAsync();

        product.SetupGet(x => x.Stock).Returns(0);
        product.SetupGet(x => x.Prices).Returns(fixture.Prices(15));
        variantProduct.SetupGet(x => x.Stock).Returns(0);
        variantProduct.SetupGet(x => x.Prices).Returns(fixture.Prices(25));
        variant.SetupGet(x => x.Stock).Returns(0);
        variant.SetupGet(x => x.Prices).Returns(fixture.Prices(45));
        var saves = 0;
        var lineEvents = 0;
        EventHandler<OrderUpdatingEventArgs> saving = (_, _) => saves++;
        EventHandler<AddedOrderlineEventArgs> added = (_, _) =>
        {
            Assert.Equal(0, saves);
            lineEvents++;
        };
        EventHandler<UpdatedOrderlineEventArgs> updated = (_, _) =>
        {
            Assert.Equal(0, saves);
            lineEvents++;
        };
        OrderEvents.OrderUpdating += saving;
        OrderEvents.AddedOrderline += added;
        OrderEvents.UpdatedOrderline += updated;
        OrderInfo result;
        try
        {
            result = await fixture.RefreshAsync();
        }
        finally
        {
            OrderEvents.OrderUpdating -= saving;
            OrderEvents.AddedOrderline -= added;
            OrderEvents.UpdatedOrderline -= updated;
        }

        Assert.Equal(1, saves);
        Assert.Equal(4, lineEvents);
        Assert.Equal(15m, Assert.Single(result.OrderLines, x => x.Key == plainLine.Key).Product.Price.Value);
        var refreshedVariant = Assert.Single(result.OrderLines, x => x.Key == variantLine.Key);
        Assert.Equal(25m, refreshedVariant.Product.Price.Value);
        Assert.Equal(45m, refreshedVariant.Variant!.Price.Value);
        Assert.Equal(variant.Object.Key, refreshedVariant.Variant.Key);
        Assert.Equal(165m, (await fixture.ReloadAsync()).ChargedAmount.Value);

        // Ignoring stock during repricing must not enable backorders for normal additions.
        await Assert.ThrowsAsync<Ekom.Exceptions.NotEnoughStockException>(() => fixture.Service.AddOrderLineAsync(
            product.Object.Key, 1, "main", new AddOrderSettings { OrderInfo = result, FireEvents = false }));
    }

    [Fact]
    public async Task RefreshPreservesDuplicateLinesMetadataAndLinkedSettingsVisibleToHandlers()
    {
        using var fixture = new Fixture();
        var product = fixture.AddProduct(10);
        var variant = fixture.AddVariant(product, 12);
        var parent = fixture.AddLine(product, 1, variant);
        var child = fixture.AddLine(product, 2, variant, parent.Key, false);
        parent.OrderLineInfo.Properties["orderlineName"] = "Parent";
        child.OrderLineInfo.Properties["orderlineName"] = "Child";
        child.OrderLineInfo.Properties["externalReference"] = "unprefixed metadata";
        child.OrderLineInfo.Properties["orderlineMarkup"] = "A &amp; B";
        await fixture.SaveAsync();
        variant.SetupGet(x => x.Prices).Returns(fixture.Prices(19));
        var seen = new List<string>();
        EventHandler<AddingOrderlineEventArgs> sync = (_, args) =>
        {
            var name = args.Settings.CustomData["orderlineName"];
            seen.Add(name);
            Assert.NotNull(args.Settings.OrderDynamicRequest);
            Assert.Null(args.Settings.OrderDynamicRequest.Prices);
            Assert.Null(args.Settings.OrderDynamicRequest.VariantPrices);
            Assert.Equal(name == "Parent" ? Guid.Empty : parent.Key, args.Settings.OrderDynamicRequest.OrderLineLink);
            Assert.Equal(name == "Parent", args.Settings.OrderDynamicRequest.CountToTotal);
            if (name == "Child")
            {
                Assert.Equal("unprefixed metadata", args.Settings.CustomData["externalReference"]);
                Assert.Equal("A &amp; B", args.Settings.CustomData["orderlineMarkup"]);
            }
        };
        OrderEvents.AddingOrderline += sync;
        try
        {
            await fixture.RefreshAsync();
        }
        finally
        {
            OrderEvents.AddingOrderline -= sync;
        }

        Assert.Equal(new[] { "Parent", "Child" }, seen);
        var saved = await fixture.ReloadAsync();
        Assert.Equal(2, saved.OrderLines.Count);
        foreach (var original in new[] { parent, child })
        {
            var actual = Assert.Single(saved.OrderLines, x => x.Key == original.Key);
            Assert.Equal(original.ProductKey, actual.ProductKey);
            Assert.Equal(original.Variant!.Key, actual.Variant!.Key);
            Assert.Equal(original.Quantity, actual.Quantity);
            Assert.Equal(original.Settings.Link, actual.Settings.Link);
            Assert.Equal(original.Settings.CountToTotal, actual.Settings.CountToTotal);
            Assert.Equal(original.OrderLineInfo.Properties.OrderBy(x => x.Key), actual.OrderLineInfo.Properties.OrderBy(x => x.Key));
            Assert.Equal(19m, actual.Variant.Price.Value);
        }
        Assert.Equal(57m, saved.ChargedAmount.Value);
        Assert.Equal(1m, saved.TotalQuantity);
    }

    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 25)]
    public async Task RefreshRunsCatalogEventsForCurrentCustomerPricing(bool loggedIn, int currentPrice)
    {
        using var fixture = new Fixture();
        var product = fixture.AddProduct(10);
        fixture.AddLine(product, 1);
        await fixture.SaveAsync();
        var replacement = fixture.CreateProduct(product.Object.Key, loggedIn ? 25 : 40);
        var calls = 0;
        Func<ProductEventArgs, CancellationToken, ValueTask> handler = (args, _) =>
        {
            calls++;
            args.Product = replacement.Object;
            return ValueTask.CompletedTask;
        };
        CatalogEvents.BeforeReturnProductAsync += handler;
        try
        {
            await fixture.RefreshAsync();
        }
        finally
        {
            CatalogEvents.BeforeReturnProductAsync -= handler;
        }

        Assert.True(calls > 0);
        Assert.Equal((decimal)currentPrice, (await fixture.ReloadAsync()).OrderLines.Single().Product.Price.Value);
    }

    [Fact]
    public async Task RefreshUsesDynamicProductAndVariantPricesFromAsyncAddingHandler()
    {
        using var fixture = new Fixture();
        var plainProduct = fixture.AddProduct(10);
        var variantProduct = fixture.AddProduct(20);
        var variant = fixture.AddVariant(variantProduct, 30);
        var plainLine = fixture.AddLine(plainProduct, 2);
        var variantLine = fixture.AddLine(variantProduct, 3, variant);
        await fixture.SaveAsync();
        var calls = 0;
        Func<object, AddingOrderlineEventArgs, CancellationToken, Task> handler = (_, args, _) =>
        {
            calls++;
            args.Settings.OrderDynamicRequest.Prices = fixture.Prices(50);
            args.Settings.OrderDynamicRequest.VariantPrices = fixture.Prices(70);
            return Task.CompletedTask;
        };
        OrderEvents.AddingOrderlineAsync += handler;
        try
        {
            await fixture.RefreshAsync();
        }
        finally
        {
            OrderEvents.AddingOrderlineAsync -= handler;
        }

        Assert.Equal(2, calls);
        var saved = await fixture.ReloadAsync();
        Assert.Equal(50m, Assert.Single(saved.OrderLines, x => x.Key == plainLine.Key).Product.Price.Value);
        var selected = Assert.Single(saved.OrderLines, x => x.Key == variantLine.Key);
        Assert.Equal(50m, selected.Product.Price.Value);
        Assert.Equal(70m, selected.Variant!.Price.Value);
        Assert.Equal(310m, saved.ChargedAmount.Value);
    }

    [Fact]
    public async Task RefreshRetainsMissingProductAndVariantUnchangedWhileRefreshingOtherLines()
    {
        using var fixture = new Fixture();
        var missingProduct = fixture.AddProduct(10);
        var variantProduct = fixture.AddProduct(20);
        var missingVariant = fixture.AddVariant(variantProduct, 30);
        var available = fixture.AddProduct(40);
        var missingProductLine = fixture.AddLine(missingProduct, 2);
        var missingVariantLine = fixture.AddLine(variantProduct, 3, missingVariant);
        var availableLine = fixture.AddLine(available, 1);
        missingProductLine.OrderLineInfo.Properties["externalReference"] = "keep me";
        await fixture.SaveAsync();
        var productBefore = JsonConvert.SerializeObject(missingProductLine);
        var variantBefore = JsonConvert.SerializeObject(missingVariantLine);
        fixture.RemoveProduct(missingProduct.Object.Key);
        fixture.RemoveVariant(missingVariant.Object.Key);
        variantProduct.SetupGet(x => x.Prices).Returns(fixture.Prices(99));
        available.SetupGet(x => x.Prices).Returns(fixture.Prices(55));

        var result = await fixture.RefreshAsync(false);

        Assert.Equal(3, result.OrderLines.Count);
        Assert.Equal(productBefore, JsonConvert.SerializeObject(Assert.Single(result.OrderLines, x => x.Key == missingProductLine.Key)));
        Assert.Equal(variantBefore, JsonConvert.SerializeObject(Assert.Single(result.OrderLines, x => x.Key == missingVariantLine.Key)));
        var saved = await fixture.ReloadAsync();
        Assert.Equal(10m, Assert.Single(saved.OrderLines, x => x.Key == missingProductLine.Key).Product.Price.Value);
        Assert.Equal(30m, Assert.Single(saved.OrderLines, x => x.Key == missingVariantLine.Key).Variant!.Price.Value);
        Assert.Equal(55m, Assert.Single(saved.OrderLines, x => x.Key == availableLine.Key).Product.Price.Value);
    }

    [Fact]
    public async Task SecondLinePricingFailureLeavesOriginalCachedAndPersistedBasketUnchanged()
    {
        using var fixture = new Fixture();
        fixture.AddLine(fixture.AddProduct(10), 1);
        fixture.AddLine(fixture.AddProduct(20), 2);
        await fixture.SaveAsync();
        var cached = (await fixture.Service.GetOrderAsync(fixture.Order.UniqueId))!;
        var originalBefore = JsonConvert.SerializeObject(fixture.Order);
        var cachedBefore = JsonConvert.SerializeObject(cached);
        var persistedBefore = (await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!.OrderInfo;
        var calls = 0;
        var failure = new InvalidOperationException("Pricing service failed on the second line.");
        Func<object, AddingOrderlineEventArgs, CancellationToken, Task> handler = (_, args, _) =>
        {
            args.Settings.CustomData["orderlineChanged"] = "must not leak";
            args.Settings.OrderDynamicRequest.Prices = fixture.Prices(999);
            if (++calls == 2)
                throw failure;
            return Task.CompletedTask;
        };
        OrderEvents.AddingOrderlineAsync += handler;
        try
        {
            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RefreshAsync());
            Assert.Same(failure, thrown);
        }
        finally
        {
            OrderEvents.AddingOrderlineAsync -= handler;
        }

        Assert.Equal(2, calls);
        Assert.Equal(originalBefore, JsonConvert.SerializeObject(fixture.Order));
        Assert.Equal(cachedBefore, JsonConvert.SerializeObject(cached));
        Assert.Equal(cachedBefore, JsonConvert.SerializeObject(await fixture.Service.GetOrderAsync(fixture.Order.UniqueId)));
        Assert.Equal(persistedBefore, (await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!.OrderInfo);
    }

    [Fact]
    public async Task DisabledEventsStillRefreshPricesWithoutRaisingOrderEvents()
    {
        using var fixture = new Fixture();
        var product = fixture.AddProduct(10);
        fixture.AddLine(product, 1);
        await fixture.SaveAsync();
        product.SetupGet(x => x.Prices).Returns(fixture.Prices(25));
        var calls = 0;
        EventHandler<AddingOrderlineEventArgs> adding = (_, _) => calls++;
        Func<object, AddingOrderlineEventArgs, CancellationToken, Task> addingAsync = (_, _, _) =>
        {
            calls++;
            return Task.CompletedTask;
        };
        EventHandler<AddedOrderlineEventArgs> added = (_, _) => calls++;
        EventHandler<UpdatedOrderlineEventArgs> updated = (_, _) => calls++;
        EventHandler<OrderUpdatedEventArgs> saved = (_, _) => calls++;
        OrderEvents.AddingOrderline += adding;
        OrderEvents.AddingOrderlineAsync += addingAsync;
        OrderEvents.AddedOrderline += added;
        OrderEvents.UpdatedOrderline += updated;
        OrderEvents.OrderUpdated += saved;
        try
        {
            await fixture.RefreshAsync(false);
        }
        finally
        {
            OrderEvents.AddingOrderline -= adding;
            OrderEvents.AddingOrderlineAsync -= addingAsync;
            OrderEvents.AddedOrderline -= added;
            OrderEvents.UpdatedOrderline -= updated;
            OrderEvents.OrderUpdated -= saved;
        }

        Assert.Equal(0, calls);
        Assert.Equal(25m, (await fixture.ReloadAsync()).ChargedAmount.Value);
    }

    [Fact]
    public async Task CancellationIsForwardedToCatalogAndAddingHandlersAndDoesNotCommitStagedLines()
    {
        using var fixture = new Fixture();
        fixture.AddLine(fixture.AddProduct(10), 1);
        fixture.AddLine(fixture.AddProduct(20), 2);
        await fixture.SaveAsync();
        var cached = (await fixture.Service.GetOrderAsync(fixture.Order.UniqueId))!;
        var originalBefore = JsonConvert.SerializeObject(fixture.Order);
        var cachedBefore = JsonConvert.SerializeObject(cached);
        var persistedBefore = (await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!.OrderInfo;
        using var cancellation = new CancellationTokenSource();
        var catalogCalls = 0;
        var addingCalls = 0;
        Func<ProductEventArgs, CancellationToken, ValueTask> catalog = (_, ct) =>
        {
            // OrderedProduct.Url also resolves catalog data synchronously during serialization.
            if (ct.CanBeCanceled)
            {
                Assert.Equal(cancellation.Token, ct);
                catalogCalls++;
            }
            return ValueTask.CompletedTask;
        };
        Func<object, AddingOrderlineEventArgs, CancellationToken, Task> adding = (_, args, ct) =>
        {
            Assert.Equal(cancellation.Token, ct);
            args.Settings.OrderDynamicRequest.Prices = fixture.Prices(999);
            if (++addingCalls == 2)
            {
                cancellation.Cancel();
                ct.ThrowIfCancellationRequested();
            }
            return Task.CompletedTask;
        };
        CatalogEvents.BeforeReturnProductAsync += catalog;
        OrderEvents.AddingOrderlineAsync += adding;
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.RefreshAsync(ct: cancellation.Token));
        }
        finally
        {
            CatalogEvents.BeforeReturnProductAsync -= catalog;
            OrderEvents.AddingOrderlineAsync -= adding;
        }

        Assert.Equal(2, catalogCalls);
        Assert.Equal(2, addingCalls);
        Assert.Equal(originalBefore, JsonConvert.SerializeObject(fixture.Order));
        Assert.Equal(cachedBefore, JsonConvert.SerializeObject(await fixture.Service.GetOrderAsync(fixture.Order.UniqueId)));
        Assert.Equal(persistedBefore, (await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!.OrderInfo);
    }

    [Fact]
    public async Task RefreshKeepsCachedIdentityForQuantityMutationWaitingOnTheOrderLock()
    {
        using var fixture = new Fixture();
        var product = fixture.AddProduct(10);
        var line = fixture.AddLine(product, 1);
        await fixture.SaveAsync();
        var cached = (await fixture.Service.GetOrderAsync(fixture.Order.UniqueId))!;
        product.SetupGet(x => x.Prices).Returns(fixture.Prices(25));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<object, AddingOrderlineEventArgs, CancellationToken, Task> adding = async (_, _, _) =>
        {
            entered.TrySetResult();
            await release.Task;
        };
        EventHandler<OrderUpdatedEventArgs> saved = (_, _) =>
        {
            Assert.Equal(25m, Assert.Single(cached.OrderLines).Product.Price.Value);
            Assert.Equal(25m, Assert.Single(fixture.Order.OrderLines).Product.Price.Value);
        };
        OrderEvents.AddingOrderlineAsync += adding;
        OrderEvents.OrderUpdated += saved;
        Task<OrderInfo>? refresh = null;
        Task<OrderInfo>? mutation = null;
        try
        {
            refresh = fixture.RefreshAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(10m, Assert.Single(cached.OrderLines).Product.Price.Value);
            mutation = fixture.Service.AddOrderLineAsync(product.Object.Key, 3, "main", new AddOrderSettings
            {
                OrderInfo = cached,
                OrderAction = OrderAction.Set,
                FireEvents = false,
            });
            Assert.False(mutation.IsCompleted);
            release.TrySetResult();
            Assert.Same(cached, await refresh);
            await mutation;
            Assert.Same(cached, await fixture.Service.GetOrderAsync(fixture.Order.UniqueId));
        }
        finally
        {
            release.TrySetResult();
            try
            {
                if (refresh != null) await refresh;
                if (mutation != null) await mutation;
            }
            finally
            {
                OrderEvents.AddingOrderlineAsync -= adding;
                OrderEvents.OrderUpdated -= saved;
            }
        }

        var persisted = Assert.Single((await fixture.ReloadAsync()).OrderLines);
        Assert.Equal(line.Key, persisted.Key);
        Assert.Equal(3m, persisted.Quantity);
        Assert.Equal(25m, persisted.Product.Price.Value);
        Assert.Equal(75m, (await fixture.ReloadAsync()).ChargedAmount.Value);
    }

    [Fact]
    public async Task RefreshWaitingBehindLinkedAdditionUsesLatestCachedBasketAndSynchronizesInitialReference()
    {
        using var fixture = new Fixture();
        var existing = fixture.AddProduct(10);
        var parent = fixture.AddProduct(20);
        var child = fixture.AddProduct(30);
        fixture.AddLine(existing, 1);
        await fixture.SaveAsync();
        var initialCached = (await fixture.Service.GetOrderAsync(fixture.Order.UniqueId))!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Func<object, AddingOrderlineEventArgs, CancellationToken, Task> adding = async (_, _, _) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.TrySetResult();
                await release.Task;
            }
        };
        OrderEvents.AddingOrderlineAsync += adding;
        Task<OrderInfo>? addition = null;
        Task<OrderInfo>? refresh = null;
        try
        {
            addition = fixture.Service.AddLinkedOrderLinesAsync(parent.Object.Key, 1, "main",
                [new LinkedOrderLineRequest { ProductId = child.Object.Key, Quantity = 2 }],
                new AddOrderSettings { OrderInfo = initialCached });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            refresh = fixture.Service.ReInitializeOrderLinesAsync("main",
                new OrderSettings { OrderInfo = initialCached, FireEvents = false });
            Assert.False(refresh.IsCompleted);
            existing.SetupGet(x => x.Prices).Returns(fixture.Prices(15));
            parent.SetupGet(x => x.Prices).Returns(fixture.Prices(25));
            child.SetupGet(x => x.Prices).Returns(fixture.Prices(35));
            release.TrySetResult();
            var added = await addition;
            Assert.NotSame(initialCached, added);
            var refreshed = await refresh;
            Assert.Same(added, refreshed);
            Assert.Same(added, await fixture.Service.GetOrderAsync(fixture.Order.UniqueId));
            Assert.Equal(3, initialCached.OrderLines.Count);
            Assert.Equal(35m, Assert.Single(initialCached.OrderLines, x => x.ProductKey == child.Object.Key).Product.Price.Value);
        }
        finally
        {
            release.TrySetResult();
            try
            {
                if (addition != null) await addition;
                if (refresh != null) await refresh;
            }
            finally
            {
                OrderEvents.AddingOrderlineAsync -= adding;
            }
        }

        var persisted = await fixture.ReloadAsync();
        Assert.Equal(3, persisted.OrderLines.Count);
        var persistedParent = Assert.Single(persisted.OrderLines, x => x.ProductKey == parent.Object.Key);
        var persistedChild = Assert.Single(persisted.OrderLines, x => x.ProductKey == child.Object.Key);
        Assert.Equal(persistedParent.Key, persistedChild.Settings.Link);
        Assert.Equal(2m, persistedChild.Quantity);
        Assert.Equal(15m, Assert.Single(persisted.OrderLines, x => x.ProductKey == existing.Object.Key).Product.Price.Value);
        Assert.Equal(25m, persistedParent.Product.Price.Value);
        Assert.Equal(35m, persistedChild.Product.Price.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefreshMergesOriginalDynamicMetadataWhenAddingHandlerReplacesRequest(bool overrideMetadata)
    {
        using var fixture = new Fixture();
        var product = fixture.AddProduct(10);
        var variant = fixture.AddVariant(product, 20);
        var parent = fixture.AddLine(fixture.AddProduct(5), 1);
        var line = fixture.AddLine(product, 2, variant, parent.Key, false, new OrderDynamicRequest
        {
            Title = "Original title",
            SKU = "Original SKU",
            Type = "Original type",
            Prices = fixture.Prices(999),
            VariantPrices = fixture.Prices(888),
        });
        await fixture.SaveAsync();
        Func<object, AddingOrderlineEventArgs, CancellationToken, Task> adding = (_, args, _) =>
        {
            Assert.Null(args.Settings.OrderDynamicRequest.Prices);
            Assert.Null(args.Settings.OrderDynamicRequest.VariantPrices);
            if (args.Product.Key == product.Object.Key)
            {
                Assert.Equal("Original title", args.Settings.OrderDynamicRequest.Title);
                Assert.Equal("Original SKU", args.Settings.OrderDynamicRequest.SKU);
                Assert.Equal("Original type", args.Settings.OrderDynamicRequest.Type);
                args.Settings.OrderDynamicRequest = new OrderDynamicRequest
                {
                    Prices = fixture.Prices(40),
                    VariantPrices = fixture.Prices(50),
                };
                if (overrideMetadata)
                {
                    args.Settings.OrderDynamicRequest.Title = "Fresh title";
                    args.Settings.OrderDynamicRequest.SKU = "Fresh SKU";
                    args.Settings.OrderDynamicRequest.Type = "Fresh type";
                }
            }
            return Task.CompletedTask;
        };
        OrderEvents.AddingOrderlineAsync += adding;
        try
        {
            await fixture.RefreshAsync();
        }
        finally
        {
            OrderEvents.AddingOrderlineAsync -= adding;
        }

        var actual = Assert.Single((await fixture.ReloadAsync()).OrderLines, x => x.Key == line.Key);
        Assert.Equal(overrideMetadata ? "Fresh title" : "Original title", actual.Product.Title);
        Assert.Equal(overrideMetadata ? "Fresh SKU" : "Original SKU", actual.Product.SKU);
        Assert.Equal(overrideMetadata ? "Fresh type" : "Original type", actual.Product.Properties["dynamicType"]);
        Assert.Equal(parent.Key, actual.Settings.Link);
        Assert.False(actual.Settings.CountToTotal);
        Assert.Equal(40m, actual.Product.Price.Value);
        Assert.Equal(50m, actual.Variant!.Price.Value);
    }

    [Fact]
    public async Task RefreshPropagatesReplacementOrdersThroughAsyncLineEventsAndPersistence()
    {
        using var fixture = new Fixture();
        var product = fixture.AddProduct(10);
        fixture.AddLine(product, 1);
        fixture.AddLine(fixture.AddProduct(20), 2);
        await fixture.SaveAsync();
        product.SetupGet(x => x.Prices).Returns(fixture.Prices(25));
        var addedCalls = 0;
        var updatedCalls = 0;
        OrderInfo? lastReplacement = null;
        Func<object, AddedOrderlineEventArgs, CancellationToken, Task> added = (_, args, _) =>
        {
            if (lastReplacement != null) Assert.Same(lastReplacement, args.OrderInfo);
            args.OrderInfo = CloneOrder(args.OrderInfo);
            args.OrderInfo.orderLines[0].OrderLineInfo.Properties["orderlineAdded"] = (++addedCalls).ToString();
            lastReplacement = args.OrderInfo;
            return Task.CompletedTask;
        };
        Func<object, UpdatedOrderlineEventArgs, CancellationToken, Task> updated = (_, args, _) =>
        {
            Assert.Same(lastReplacement, args.OrderInfo);
            Assert.Equal(addedCalls.ToString(), args.OrderInfo.orderLines[0].OrderLineInfo.Properties["orderlineAdded"]);
            args.OrderInfo = CloneOrder(args.OrderInfo);
            args.OrderInfo.orderLines[0].OrderLineInfo.Properties["orderlineUpdated"] = (++updatedCalls).ToString();
            lastReplacement = args.OrderInfo;
            return Task.CompletedTask;
        };
        OrderEvents.AddedOrderlineAsync += added;
        OrderEvents.UpdatedOrderlineAsync += updated;
        try
        {
            await fixture.RefreshAsync();
        }
        finally
        {
            OrderEvents.AddedOrderlineAsync -= added;
            OrderEvents.UpdatedOrderlineAsync -= updated;
        }

        Assert.Equal(2, addedCalls);
        Assert.Equal(2, updatedCalls);
        var persisted = await fixture.ReloadAsync();
        var actual = Assert.Single(persisted.OrderLines, x => x.ProductKey == product.Object.Key);
        Assert.Equal("2", actual.OrderLineInfo.Properties["orderlineAdded"]);
        Assert.Equal("2", actual.OrderLineInfo.Properties["orderlineUpdated"]);
        Assert.Equal(25m, actual.Product.Price.Value);
        Assert.Equal(65m, persisted.ChargedAmount.Value);
    }

    [Theory]
    [InlineData(false, "identity")]
    [InlineData(true, "identity")]
    [InlineData(false, "removal")]
    [InlineData(true, "removal")]
    [InlineData(false, "quantity")]
    [InlineData(true, "metadata")]
    public async Task RefreshRejectsInvalidReplacementOrdersWithoutSaving(bool replaceOnUpdated, string invalidChange)
    {
        using var fixture = new Fixture();
        var line = fixture.AddLine(fixture.AddProduct(10), 1);
        line.OrderLineInfo.Properties["externalReference"] = "must survive";
        await fixture.SaveAsync();
        var cached = (await fixture.Service.GetOrderAsync(fixture.Order.UniqueId))!;
        var cachedBefore = JsonConvert.SerializeObject(cached);
        var originalBefore = JsonConvert.SerializeObject(fixture.Order);
        var persistedBefore = (await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!.OrderInfo;
        var saves = 0;
        OrderInfo InvalidReplacement(OrderInfo source)
        {
            var replacement = CloneOrder(source);
            switch (invalidChange)
            {
                case "identity":
                    var data = replacement.OrderDataClone();
                    data.UniqueId = Guid.NewGuid();
                    data.OrderInfo = JsonConvert.SerializeObject(replacement);
                    return new OrderInfo(data);
                case "removal":
                    replacement.orderLines.Clear();
                    break;
                case "quantity":
                    replacement.orderLines[0].Quantity++;
                    break;
                case "metadata":
                    replacement.orderLines[0].OrderLineInfo.Properties.Remove("externalReference");
                    break;
            }
            return replacement;
        }
        Func<object, AddedOrderlineEventArgs, CancellationToken, Task> added = (_, args, _) =>
        {
            if (!replaceOnUpdated) args.OrderInfo = InvalidReplacement(args.OrderInfo);
            return Task.CompletedTask;
        };
        Func<object, UpdatedOrderlineEventArgs, CancellationToken, Task> updated = (_, args, _) =>
        {
            if (replaceOnUpdated) args.OrderInfo = InvalidReplacement(args.OrderInfo);
            return Task.CompletedTask;
        };
        EventHandler<OrderUpdatingEventArgs> saving = (_, _) => saves++;
        OrderEvents.AddedOrderlineAsync += added;
        OrderEvents.UpdatedOrderlineAsync += updated;
        OrderEvents.OrderUpdating += saving;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RefreshAsync());
        }
        finally
        {
            OrderEvents.AddedOrderlineAsync -= added;
            OrderEvents.UpdatedOrderlineAsync -= updated;
            OrderEvents.OrderUpdating -= saving;
        }

        Assert.Equal(0, saves);
        Assert.Equal(originalBefore, JsonConvert.SerializeObject(fixture.Order));
        Assert.Equal(cachedBefore, JsonConvert.SerializeObject(cached));
        Assert.Same(cached, await fixture.Service.GetOrderAsync(fixture.Order.UniqueId));
        Assert.Equal(persistedBefore, (await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!.OrderInfo);
    }

    private static OrderInfo CloneOrder(OrderInfo source)
    {
        var data = source.OrderDataClone();
        data.OrderInfo = JsonConvert.SerializeObject(source);
        return new OrderInfo(data);
    }

    [Fact]
    public async Task RefreshPersistsMetadataSuppliedByOrderUpdatingHandler()
    {
        using var fixture = new Fixture();
        var line = fixture.AddLine(fixture.AddProduct(10), 1);
        await fixture.SaveAsync();
        Func<object, OrderUpdatingEventArgs, CancellationToken, Task> updating = (_, args, _) =>
        {
            var stagedLine = Assert.Single(args.OrderInfo.OrderLines);
            stagedLine.OrderLineInfo.Properties["orderlinePricingContext"] = "member";
            return Task.CompletedTask;
        };
        OrderEvents.OrderUpdatingAsync += updating;
        try
        {
            await fixture.RefreshAsync();
        }
        finally
        {
            OrderEvents.OrderUpdatingAsync -= updating;
        }

        var persisted = Assert.Single((await fixture.ReloadAsync()).OrderLines);
        Assert.Equal(line.Key, persisted.Key);
        Assert.Equal("member", persisted.OrderLineInfo.Properties["orderlinePricingContext"]);
    }

    [Fact]
    public async Task PostSaveNotificationFailureDoesNotUndoPublishedPricesOrFailCommittedRefresh()
    {
        using var fixture = new Fixture();
        var product = fixture.AddProduct(10);
        fixture.AddLine(product, 1);
        await fixture.SaveAsync();
        var cached = (await fixture.Service.GetOrderAsync(fixture.Order.UniqueId))!;
        product.SetupGet(x => x.Prices).Returns(fixture.Prices(25));
        var publishedBeforeNotification = false;
        EventHandler<OrderUpdatedEventArgs> updated = (_, args) =>
        {
            publishedBeforeNotification = ReferenceEquals(cached, args.OrderInfo)
                && cached.OrderLines.Single().Product.Price.Value == 25m
                && fixture.Order.OrderLines.Single().Product.Price.Value == 25m;
            throw new InvalidOperationException("External notification failed after commit.");
        };
        OrderEvents.OrderUpdated += updated;
        try
        {
            Assert.Same(cached, await fixture.RefreshAsync());
        }
        finally
        {
            OrderEvents.OrderUpdated -= updated;
        }

        Assert.True(publishedBeforeNotification);
        Assert.Equal(25m, (await fixture.ReloadAsync()).ChargedAmount.Value);
        Assert.Equal(25m, fixture.Order.ChargedAmount.Value);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ConfigurationScope _scope;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly StockReservationTests.ReservationDatabase _database = new();
        private readonly Mock<IPerStoreIndexedCache<IProduct>> _products = new();
        private readonly ConcurrentDictionary<Guid, IVariant> _variants = new();

        public Fixture()
        {
            var store = new Mock<IStore>();
            store.SetupGet(x => x.Alias).Returns("main");
            store.SetupGet(x => x.Culture).Returns(new CultureInfoDto { Name = "en-US" });
            store.SetupGet(x => x.Cultures).Returns([new CultureInfoDto { Name = "en-US" }]);
            store.SetupGet(x => x.Currencies).Returns([new CurrencyModel { CurrencyValue = "en-US" }]);
            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("main")).Returns(store.Object);
            stores.Setup(x => x.GetStoreFromCache()).Returns(store.Object);
            var variants = new Mock<IPerStoreIndexedCache<IVariant>>();
            variants.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IVariant>>
            {
                ["main"] = _variants,
            });
            var discounts = new Mock<IPerStoreCache<IDiscount>>();
            discounts.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IDiscount>>
            {
                ["main"] = new(),
            });
            _scope = new ConfigurationScope(addServices: services =>
            {
                services.AddSingleton(_database.NewStockApi());
                services.AddSingleton(new Ekom.API.Store(stores.Object, Mock.Of<ICacheRefreshService>()));
                services.AddSingleton(sp => new Providers(sp.GetRequiredService<Configuration>(), NullLogger<Providers>.Instance,
                    Mock.Of<IPerStoreCache<IShippingProvider>>(x => x["main"] == new ConcurrentDictionary<Guid, IShippingProvider>()),
                    Mock.Of<IPerStoreCache<IPaymentProvider>>(x => x["main"] == new ConcurrentDictionary<Guid, IPaymentProvider>()),
                    Mock.Of<IBaseCache<IZone>>(), stores.Object, null!));
                services.AddSingleton(sp => new Discounts(sp.GetRequiredService<Configuration>(), NullLogger<Discounts>.Instance,
                    discounts.Object, stores.Object));
                services.AddSingleton(sp => new Catalog(NullLogger<Catalog>.Instance, sp.GetRequiredService<Configuration>(),
                    sp.GetRequiredService<IServiceScopeFactory>(), _products.Object, Mock.Of<IPerStoreIndexedCache<ICategory>>(),
                    Mock.Of<IPerStoreCache<IProductDiscount>>(), variants.Object, Mock.Of<IPerStoreIndexedCache<IVariantGroup>>(),
                    stores.Object, new Microsoft.AspNetCore.Http.HttpContextAccessor(), Mock.Of<IProductFilterService>()));
            });
            using var db = _database.Factory.GetDatabase();
            db.CreateTable<OrderData>();
            var currency = new CurrencyModel { CurrencyValue = "en-US" };
            var storeInfo = new StoreInfo(Guid.NewGuid(), currency, [currency], "en-US", "main", false, 0, false);
            var data = new OrderData
            {
                UniqueId = Guid.NewGuid(),
                OrderStatusCol = "Incomplete",
                OrderInfo = JsonConvert.SerializeObject(new
                {
                    StoreInfo = storeInfo,
                    OrderLines = Array.Empty<object>(),
                    CustomerInformation = new CustomerInfo(),
                }),
            };
            db.Insert(data);
            Order = new OrderInfo(data);
            Repository = new OrderRepository(NullLogger<OrderRepository>.Instance, _scope.Instance, _database.Factory, _cache);
            Service = new OrderService(_scope.Instance, Repository, null!, Mock.Of<IOrderActivityLogService>(),
                NullLogger<OrderService>.Instance, stores.Object, _cache, Mock.Of<IMemberService>(), null!,
                Mock.Of<IOrderTrackingService>());
        }

        public OrderInfo Order { get; }
        public OrderRepository Repository { get; }
        public OrderService Service { get; }

        public List<IPrice> Prices(decimal amount) => [new Price(amount, Order.StoreInfo.Currency, 0, false)];

        public Mock<IProduct> CreateProduct(Guid key, decimal amount)
        {
            var product = new Mock<IProduct>();
            product.SetupGet(x => x.Key).Returns(key);
            product.SetupGet(x => x.Stock).Returns(100);
            product.SetupGet(x => x.Url).Returns("");
            product.SetupGet(x => x.AllVariants).Returns(Array.Empty<IVariant>());
            product.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>
            {
                ["__Key"] = key.ToString(),
                ["title"] = "Product",
                ["sku"] = key.ToString(),
            });
            product.SetupGet(x => x.Prices).Returns(Prices(amount));
            product.Setup(x => x.ProductDiscountAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDiscount?)null);
            return product;
        }

        public Mock<IProduct> AddProduct(decimal amount)
        {
            var key = Guid.NewGuid();
            var product = CreateProduct(key, amount);
            IProduct? cached = product.Object;
            _products.Setup(x => x.TryGetByKey("main", key, out cached)).Returns(true);
            return product;
        }

        public void RemoveProduct(Guid key)
        {
            IProduct? cached = null;
            _products.Setup(x => x.TryGetByKey("main", key, out cached)).Returns(false);
        }

        public Mock<IVariant> AddVariant(Mock<IProduct> product, decimal amount)
        {
            var key = Guid.NewGuid();
            var variant = new Mock<IVariant>();
            variant.SetupGet(x => x.Key).Returns(key);
            variant.SetupGet(x => x.ProductKey).Returns(product.Object.Key);
            variant.SetupGet(x => x.Stock).Returns(100);
            variant.SetupGet(x => x.Prices).Returns(Prices(amount));
            variant.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>
            {
                ["__Key"] = key.ToString(),
                ["id"] = "123",
                ["title"] = "Variant",
                ["sku"] = key.ToString(),
            });
            var group = new Mock<IVariantGroup>();
            group.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>
            {
                ["__Key"] = Guid.NewGuid().ToString(),
                ["id"] = "456",
            });
            variant.SetupGet(x => x.VariantGroup).Returns(group.Object);
            product.SetupGet(x => x.AllVariants).Returns([variant.Object]);
            _variants[key] = variant.Object;
            return variant;
        }

        public void RemoveVariant(Guid key) => _variants.TryRemove(key, out _);

        public OrderLine AddLine(Mock<IProduct> product, decimal quantity, Mock<IVariant>? variant = null,
            Guid link = default, bool countToTotal = true, OrderDynamicRequest? dynamicRequest = null)
        {
            dynamicRequest ??= new OrderDynamicRequest();
            dynamicRequest.OrderLineLink = link;
            dynamicRequest.CountToTotal = countToTotal;
            var line = new OrderLine(product.Object, quantity, Guid.NewGuid(), Order, [], variant?.Object,
                dynamicRequest);
            Order.orderLines.Add(line);
            return line;
        }

        public async Task SaveAsync()
        {
            var data = (await Repository.GetOrderAsync(Order.UniqueId))!;
            data.OrderInfo = JsonConvert.SerializeObject(Order);
            data.TotalAmount = Order.ChargedAmount.Value;
            await Repository.UpdateOrderAsync(data);
        }

        public Task<OrderInfo> RefreshAsync(bool fireEvents = true, CancellationToken ct = default)
            => Service.ReInitializeOrderLinesAsync("main", new OrderSettings { OrderInfo = Order, FireEvents = fireEvents }, ct);

        public async Task<OrderInfo> ReloadAsync() => new((await Repository.GetOrderAsync(Order.UniqueId))!);

        public void Dispose()
        {
            _scope.Dispose();
            _cache.Dispose();
            _database.Dispose();
        }
    }
}
