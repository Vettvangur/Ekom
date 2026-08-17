using Ekom.Models;
using Ekom.Tests.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ekom.Tests.Tests;

public class StoreCurrencyTests
{
    [Fact]
    public void Currency_UsesRequestCultureWhenCurrencyCookieIsMissing()
    {
        var httpContextAccessor = CreateHttpContextAccessor("en-US");

        using var configurationScope = new ConfigurationScope(addServices: services =>
            services.AddSingleton<IHttpContextAccessor>(httpContextAccessor));
        var store = new TestStore();

        Assert.Equal("en-US", store.Currency.CurrencyValue);
    }

    [Fact]
    public void Currency_UsesCurrencyCookieBeforeRequestCulture()
    {
        var httpContextAccessor = CreateHttpContextAccessor("en-US");
        httpContextAccessor.HttpContext!.Request.Headers.Cookie = "EkomCurrency-Store2=is-IS";

        using var configurationScope = new ConfigurationScope(addServices: services =>
            services.AddSingleton<IHttpContextAccessor>(httpContextAccessor));
        var store = new TestStore();

        Assert.Equal("is-IS", store.Currency.CurrencyValue);
    }

    [Fact]
    public void Currency_UsesDefaultCurrencyForUnsupportedRequestCulture()
    {
        var httpContextAccessor = CreateHttpContextAccessor("da-DK");

        using var configurationScope = new ConfigurationScope(addServices: services =>
            services.AddSingleton<IHttpContextAccessor>(httpContextAccessor));
        var store = new TestStore();

        Assert.Equal("is-IS", store.Currency.CurrencyValue);
    }

    private static HttpContextAccessor CreateHttpContextAccessor(string culture)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.Features.Set<IRequestCultureFeature>(
            new RequestCultureFeature(new RequestCulture(culture), null));

        return httpContextAccessor;
    }

    private sealed class TestStore : Store
    {
        private static readonly List<CurrencyModel> StoreCurrencies =
        [
            new CurrencyModel { CurrencyValue = "is-IS", CurrencyFormat = "C" },
            new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" },
        ];

        public override string Alias => "Store2";
        public override List<CurrencyModel> Currencies => StoreCurrencies;
    }
}
