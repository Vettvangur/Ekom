using Ekom.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Ekom;

public class EkomCultureRequestLocalizationOptions : IConfigureOptions<RequestLocalizationOptions>
{
    private readonly IUmbracoService _umbracoService;
    public EkomCultureRequestLocalizationOptions(IUmbracoService umbracoService)
    {
        _umbracoService = umbracoService;
    }

    public void Configure(RequestLocalizationOptions options)
    {
        try
        {
            var cultures = _umbracoService.GetLanguages();
            var defaultCulture = _umbracoService.DefaultLanguage();

            var supportedCultures = cultures.Select(culture => new CultureInfo(culture.IsoCode)).ToList();

            options.DefaultRequestCulture = new RequestCulture(defaultCulture, defaultCulture);
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;

            // Insert EkomCultureProvider at the highest priority
            options.RequestCultureProviders.Insert(0, new EkomCultureProvider(options));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed configuring Ekom localization: " + ex.ToString());
        }
    }

}

public class EkomCultureProvider : RequestCultureProvider
{
    private readonly RequestLocalizationOptions _localizationOptions;

    // ctor with reference to the RequestLocalizationOptions
    public EkomCultureProvider(RequestLocalizationOptions localizationOptions)
        => _localizationOptions = localizationOptions;

    public override Task<ProviderCultureResult> DetermineProviderCultureResult(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/ekom", StringComparison.InvariantCultureIgnoreCase))
        {
            return NullProviderCultureResult;
        }

        var cultureName = context.Request.Query["Culture"].FirstOrDefault()
                            ?? context.Request.Headers["Culture"].FirstOrDefault()
                            ?? context.Request.Headers["Accept-Language"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(cultureName))
        {
            // No culture specified → use default
            return Task.FromResult(
                new ProviderCultureResult(
                    _localizationOptions.DefaultRequestCulture.Culture.Name,
                    _localizationOptions.DefaultRequestCulture.UICulture.Name));
        }

        if (string.IsNullOrEmpty(cultureName) || cultureName == "*")
        {
            return NullProviderCultureResult;
        }

        cultureName = ParseAcceptLanguageHeader(cultureName);

        // Try to match with supported cultures
        var supported = _localizationOptions.SupportedCultures
            ?.FirstOrDefault(c => string.Equals(c.Name, cultureName, StringComparison.OrdinalIgnoreCase));

        if (supported != null)
        {
            // Found in supported list
            return Task.FromResult(new ProviderCultureResult(supported.Name, supported.Name));
        }

        return Task.FromResult(
            new ProviderCultureResult(
                _localizationOptions.DefaultRequestCulture.Culture.Name,
                _localizationOptions.DefaultRequestCulture.UICulture.Name));
    }

    private static string ParseAcceptLanguageHeader(string headerValue)
    {
        string? languages = headerValue.Split(',')
            .Select(l => l.Split(';').First().Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .FirstOrDefault();

        return languages;
    }

}
