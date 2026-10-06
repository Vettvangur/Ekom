using Ekom.Analytics;
using Ekom.Controllers;
using Ekom.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class AnalyticsRegistrationTests
{
    [Fact]
    public void AnalyticsControllerRequiresBackofficePolicy()
    {
        var policies = typeof(EkomAnalyticsController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Select(x => x.Policy);

        Assert.Contains(AnalyticsAuthorization.Policy, policies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnalyticsPolicyRejectsAnonymousAndStorefrontUsersEvenWithPermissiveManagerAccess(bool authenticatedMember)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var security = new Mock<ISecurityService>();
        security.Setup(x => x.GetUmbracoUserGroups()).Returns(Array.Empty<string>());
        services.AddSingleton(security.Object);
        services.AddEkomAnalytics(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        var principal = new ClaimsPrincipal(authenticatedMember ? new ClaimsIdentity("StorefrontMember") : new ClaimsIdentity());

        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, http, AnalyticsAuthorization.Policy);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AnalyticsPolicyRequiresResolvedBackofficeUserGroups()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var security = new Mock<ISecurityService>();
        security.Setup(x => x.GetUmbracoUserGroups()).Returns(new[] { "editor" });
        services.AddSingleton(security.Object);
        services.AddEkomAnalytics(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };

        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity("Backoffice")), http, AnalyticsAuthorization.Policy);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("RefreshInterval", "not-a-duration")]
    [InlineData("BatchSize", "not-a-number")]
    [InlineData("BatchSize", "0")]
    [InlineData("RefreshInterval", "00:00:00")]
    public void InvalidAnalyticsConfigurationDisablesProcessingWithoutFailingOptionsResolution(string key, string value)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ekom:Analytics:Enabled"] = "true",
            [$"Ekom:Analytics:{key}"] = value,
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEkomAnalytics(config);
        using var provider = services.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<IOptions<AnalyticsOptions>>().Value.Enabled);
    }

    [Fact]
    public void AnalyticsIsOptInAndCustomerIdentityResolverCanBeReplaced()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        var customResolver = new TestIdentityResolver();
        services.AddSingleton<IAnalyticsCustomerIdentityResolver>(customResolver);
        services.AddEkomAnalytics(config);
        using var provider = services.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<IOptions<AnalyticsOptions>>().Value.Enabled);
        Assert.Same(customResolver, provider.GetRequiredService<IAnalyticsCustomerIdentityResolver>());
    }

    private sealed class TestIdentityResolver : IAnalyticsCustomerIdentityResolver
    {
        public AnalyticsCustomerIdentity? Resolve(Ekom.Models.OrderData order, System.Text.Json.JsonElement snapshot)
            => new("Application", "customer-123");
    }
}
