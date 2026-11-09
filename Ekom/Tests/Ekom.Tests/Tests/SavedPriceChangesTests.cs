using Ekom.Utilities;
using Ekom.Interfaces;
using Ekom.Services;
using Ekom.Tests.Objects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection(NativeDiscountPriceCollection.Name)]
public sealed class SavedPriceChangesTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ContentSavingUsesScopedServicesAndLogsFailures(bool allowed, bool serviceFails)
    {
        using var fixture = new DatabasePriceTests.Fixture();
        var key = Guid.NewGuid();
        var raw = "{\"Web\":[{\"Currency\":\"en-US\",\"Price\":125,\"EditId\":\"edit\"}]}";
        var property = new Mock<IProperty>();
        property.SetupGet(x => x.Alias).Returns("price");
        property.SetupGet(x => x.PropertyType).Returns(Mock.Of<IPropertyType>(x => x.PropertyEditorAlias == "Ekom.Price"));
        property.Setup(x => x.GetValue(null, null, false)).Returns(() => raw);
        var node = new Mock<IContent>();
        node.SetupGet(x => x.Key).Returns(key);
        node.SetupGet(x => x.Properties).Returns(new PropertyCollection([property.Object]));
        node.Setup(x => x.SetValue("price", It.IsAny<object>(), null, null))
            .Callback((string alias, object value, string? culture, string? segment) => raw = value.ToString()!);
        var security = new Mock<ISecurityService>();
        security.Setup(x => x.IsAuthenticated()).Returns(true);
        if (serviceFails) security.Setup(x => x.IsAuthenticated()).Throws(new InvalidOperationException("Security service failed."));
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessStore("Web")).Returns(allowed);
        var stores = new Mock<IStoreService>();
        stores.Setup(x => x.GetAllStores()).Returns([fixture.Store]);
        using var config = new ConfigurationScope(overrides: new Dictionary<string, string?> { ["Ekom:UseDatabasePrices"] = "true" },
            addServices: services =>
            {
                services.AddSingleton(fixture.Prices);
                services.AddSingleton(new Ekom.API.Store(stores.Object, Mock.Of<ICacheRefreshService>()));
            });
        using var scopedProvider = new ServiceCollection()
            .AddScoped<ISecurityService>(_ => security.Object)
            .AddScoped<IManagerAccessService>(_ => access.Object)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.Throws<InvalidOperationException>(() => scopedProvider.GetRequiredService<ISecurityService>());
        using var scope = scopedProvider.CreateScope();
        var scopedSecurity = scope.ServiceProvider.GetRequiredService<ISecurityService>();
        var scopedAccess = scope.ServiceProvider.GetRequiredService<IManagerAccessService>();
        var logger = new Mock<ILogger>();
#if NET10_0_OR_GREATER
        var assembly = Assembly.Load("Ekom.U17");
#else
        var assembly = Assembly.Load("Ekom.U10");
#endif
        var apply = assembly.GetType("Ekom.Umb.PriceSavingNotifications")!.GetMethod("ApplyAsync", BindingFlags.Public | BindingFlags.Static)!;
        var notification = new ContentSavingNotification([node.Object], new EventMessages());
        await (Task)apply.Invoke(null, [notification, scopedSecurity, scopedAccess, logger.Object, CancellationToken.None])!;
        var succeeds = allowed && !serviceFails;
        Assert.Equal(!succeeds, notification.Cancel);
        logger.Verify(x => x.Log(
            LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            succeeds ? Times.Never() : Times.Once());
        if (!succeeds)
        {
            Assert.Empty(fixture.Prices.Get(key));
            Assert.Contains("EditId", raw, StringComparison.Ordinal);
            return;
        }
        Assert.Equal(125, Assert.Single(fixture.Prices.Get(key)).Price);
        Assert.DoesNotContain("EditId", raw, StringComparison.Ordinal);
        await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 150);
        await (Task)apply.Invoke(null, [new ContentSavingNotification([node.Object], new EventMessages()), scopedSecurity, scopedAccess, logger.Object, CancellationToken.None])!;
        Assert.Equal(150, Assert.Single(fixture.Prices.Get(key)).Price);
        node.Verify(x => x.SetValue("price", It.IsAny<object>(), null, null), Times.Once);
    }

    [Fact]
    public async Task SaveAppliesOnlyExplicitEditsAndSubsequentSavesPreserveErpPrices()
    {
        using var fixture = new DatabasePriceTests.Fixture();
        var key = Guid.NewGuid();
        var value = "{\"Web\":[{\"Currency\":\"en-US\",\"Price\":125,\"EditId\":\"edit\"},{\"Currency\":\"en-GB\",\"Price\":80}]}";
        await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 110);
        await fixture.Prices.SetPriceAsync(key, "Web", "en-GB", 95);
        var changes = SavedPriceChanges.Create(key, value, [fixture.Store]);
        fixture.Prices.Validate(changes);
        Assert.Single(changes);
        Assert.Equal(110, fixture.Prices.Get(key).Single(row => row.Currency == "EN-US").Price);
        await fixture.Prices.ApplyAsync(changes);
        var savedValue = SavedPriceChanges.Clear(value);
        Assert.Equal(125, fixture.Prices.Get(key).Single(row => row.Currency == "EN-US").Price);
        Assert.Equal(95, fixture.Prices.Get(key).Single(row => row.Currency == "EN-GB").Price);
        Assert.DoesNotContain("EditId", savedValue, StringComparison.Ordinal);
        await fixture.Prices.SetPriceAsync(key, "Web", "en-US", 150);
        Assert.Empty(SavedPriceChanges.Create(key, savedValue, [fixture.Store]));
        Assert.Equal(150, fixture.Prices.Get(key).Single(row => row.Currency == "EN-US").Price);
    }

    [Fact]
    public void UneditedLegacyValuesDoNotOverwriteTablePrices()
    {
        using var fixture = new DatabasePriceTests.Fixture();
        var key = Guid.NewGuid();
        Assert.Empty(SavedPriceChanges.Create(key, "100", [fixture.Store]));
        Assert.Empty(SavedPriceChanges.Create(key, "{\"Web\":[{\"Currency\":\"en-US\",\"Price\":100}]}", [fixture.Store]));
        var change = Assert.Single(SavedPriceChanges.Create(key,
            "{\"Web\":[{\"Currency\":\"en-US\",\"Price\":100,\"EditId\":\"edit\"}]}", [fixture.Store], "ekmDiscountPrice"));
        Assert.Equal("ekmDiscountPrice", change.PropertyAlias);
        Assert.Equal(100, change.Price);
    }

    [Fact]
    public void InvalidEditsFailValidationBeforeAnyPriceChanges()
    {
        using var fixture = new DatabasePriceTests.Fixture();
        var key = Guid.NewGuid();
        var value = "{\"Web\":[{\"Currency\":\"en-US\",\"Price\":-1,\"EditId\":\"edit\"}]}";
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Prices.Validate(SavedPriceChanges.Create(key, value, [fixture.Store])));
        Assert.Throws<ArgumentException>(() => SavedPriceChanges.Create(key, value.Replace("en-US", "unknown", StringComparison.Ordinal), [fixture.Store]));
        Assert.Empty(fixture.Prices.Get(key));
    }
}
