using Ekom.Models.Umbraco;
using Ekom.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Globalization;
using Xunit;

namespace Ekom.Tests.Tests;

public class EkomCultureProviderTests
{
    [Fact]
    public async Task DetermineProviderCultureResult_UsesCultureHeaderAfterUmbracoStarts()
    {
        var umbracoService = new Mock<IUmbracoService>();
        umbracoService.Setup(x => x.DefaultLanguage()).Returns("en-US");
        umbracoService.Setup(x => x.GetLanguages()).Returns(
        [
            new UmbracoLanguage { IsoCode = "en-US" },
            new UmbracoLanguage { IsoCode = "is-IS" },
        ]);
        var options = new RequestLocalizationOptions();
        var configurator = new EkomCultureRequestLocalizationOptions();
        configurator.Configure(options);
        EkomCultureRequestLocalizationOptions.ConfigureCultures(options, umbracoService.Object);
        var provider = Assert.IsType<EkomCultureProvider>(options.RequestCultureProviders[0]);
        var context = new DefaultHttpContext();
        context.Request.Path = "/ekom/order/add";
        context.Request.Headers["Culture"] = "is-IS";

        ProviderCultureResult? result = await provider.DetermineProviderCultureResult(context);

        Assert.NotNull(result);
        Assert.Equal("is-IS", result.Cultures[0].Value);

        await provider.DetermineProviderCultureResult(context);

        umbracoService.Verify(x => x.GetLanguages(), Times.Once);
    }

    [Fact]
    public void Configure_RegistersProviderBeforeUmbracoStarts()
    {
        var options = new RequestLocalizationOptions();
        var configurator = new EkomCultureRequestLocalizationOptions();

        configurator.Configure(options);

        Assert.IsType<EkomCultureProvider>(options.RequestCultureProviders[0]);
    }

    [Fact]
    public async Task RequestLocalizationMiddleware_SetsCultureFromEkomHeader()
    {
        var umbracoService = new Mock<IUmbracoService>();
        umbracoService.Setup(x => x.DefaultLanguage()).Returns("en-US");
        umbracoService.Setup(x => x.GetLanguages()).Returns(
        [
            new UmbracoLanguage { IsoCode = "en-US" },
            new UmbracoLanguage { IsoCode = "is-IS" },
        ]);
        var options = new RequestLocalizationOptions();
        new EkomCultureRequestLocalizationOptions().Configure(options);
        EkomCultureRequestLocalizationOptions.ConfigureCultures(options, umbracoService.Object);
        var context = new DefaultHttpContext();
        context.Request.Path = "/ekom/order/add";
        context.Request.Headers["Culture"] = "is-IS";
        var middleware = new RequestLocalizationMiddleware(
            _ =>
            {
                Assert.Equal("is-IS", CultureInfo.CurrentCulture.Name);
                return Task.CompletedTask;
            },
            Options.Create(options),
            NullLoggerFactory.Instance);

        await middleware.Invoke(context);
    }
}
