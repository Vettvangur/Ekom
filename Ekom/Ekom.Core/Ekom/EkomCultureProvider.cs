using Ekom.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Ekom;

public class EkomCultureRequestLocalizationOptions : IConfigureOptions<RequestLocalizationOptions>
{
    public void Configure(RequestLocalizationOptions options)
    {
        if (!options.RequestCultureProviders.OfType<EkomCultureProvider>().Any())
        {
            options.RequestCultureProviders.Insert(0, new EkomCultureProvider());
        }
    }

    public static void ConfigureCultures(RequestLocalizationOptions options, IUmbracoService umbracoService)
    {
        var defaultCulture = umbracoService.DefaultLanguage();
        var supportedCultures = umbracoService.GetLanguages()
            .Select(culture => culture.IsoCode)
            .Append(defaultCulture)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(culture => culture, culture => culture, StringComparer.OrdinalIgnoreCase);
        var cultures = supportedCultures.Values.Select(culture => new CultureInfo(culture)).ToList();

        options.DefaultRequestCulture = new RequestCulture(defaultCulture, defaultCulture);
        options.SupportedCultures = cultures;
        options.SupportedUICultures = cultures;

        options.RequestCultureProviders.OfType<EkomCultureProvider>()
            .FirstOrDefault()
            ?.SetCultures(defaultCulture, supportedCultures);
    }
}

public class EkomCultureProvider : RequestCultureProvider
{
    private CultureSettings? _cultureSettings;

    public override Task<ProviderCultureResult> DetermineProviderCultureResult(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/ekom", StringComparison.InvariantCultureIgnoreCase))
        {
            return NullProviderCultureResult;
        }

        var cultureSettings = Volatile.Read(ref _cultureSettings);
        if (cultureSettings == null)
        {
            return NullProviderCultureResult;
        }

        var cultureName = context.Request.Query["Culture"].FirstOrDefault()
            ?? context.Request.Headers["Culture"].FirstOrDefault()
            ?? context.Request.Headers["Accept-Language"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return Task.FromResult(new ProviderCultureResult(cultureSettings.DefaultCulture, cultureSettings.DefaultCulture));
        }

        if (string.IsNullOrEmpty(cultureName) || cultureName == "*")
        {
            return NullProviderCultureResult;
        }

        cultureName = ParseAcceptLanguageHeader(cultureName);

        if (cultureSettings.SupportedCultures.TryGetValue(cultureName, out var supportedCulture))
        {
            return Task.FromResult(new ProviderCultureResult(supportedCulture, supportedCulture));
        }

        return Task.FromResult(new ProviderCultureResult(cultureSettings.DefaultCulture, cultureSettings.DefaultCulture));
    }

    internal void SetCultures(string defaultCulture, IReadOnlyDictionary<string, string> supportedCultures)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCulture);
        Volatile.Write(ref _cultureSettings, new CultureSettings(defaultCulture, supportedCultures));
    }

    private sealed record CultureSettings(
        string DefaultCulture,
        IReadOnlyDictionary<string, string> SupportedCultures);

    private static string ParseAcceptLanguageHeader(string headerValue)
    {
        string? languages = headerValue.Split(',')
            .Select(l => l.Split(';').First().Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .FirstOrDefault();

        return languages;
    }

}
