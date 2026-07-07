using Ekom.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Ekom;

public class EkomCultureRequestLocalizationOptions : IConfigureOptions<RequestLocalizationOptions>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<EkomCultureRequestLocalizationOptions> _logger;

    public EkomCultureRequestLocalizationOptions(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<EkomCultureRequestLocalizationOptions> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public void Configure(RequestLocalizationOptions options)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();

            if (!IsUmbracoRuntimeReady(scope.ServiceProvider))
            {
                return;
            }

            var umbracoService = scope.ServiceProvider.GetRequiredService<IUmbracoService>();

            var cultures = umbracoService.GetLanguages();
            var defaultCulture = umbracoService.DefaultLanguage();

            var supportedCultures = cultures.Select(culture => new CultureInfo(culture.IsoCode)).ToList();

            options.DefaultRequestCulture = new RequestCulture(defaultCulture, defaultCulture);
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;

            // Insert EkomCultureProvider at the highest priority
            options.RequestCultureProviders.Insert(0, new EkomCultureProvider(options));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed configuring Ekom localization.");
        }
    }

    private static bool IsUmbracoRuntimeReady(IServiceProvider serviceProvider)
    {
        var runtimeStateType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType("Umbraco.Cms.Core.Runtime.IRuntimeState"))
            .FirstOrDefault(type => type != null);

        if (runtimeStateType == null)
        {
            return false;
        }

        var runtimeState = serviceProvider.GetService(runtimeStateType);

        if (runtimeState == null)
        {
            return false;
        }

        var level = runtimeStateType.GetProperty("Level")?.GetValue(runtimeState)?.ToString();

        return string.Equals(level, "Run", StringComparison.Ordinal);
    }

}

public class EkomCultureProvider : RequestCultureProvider
{
    private readonly RequestLocalizationOptions _localizationOptions;
    private readonly Dictionary<string, string> _supportedCulturesByName;

    // ctor with reference to the RequestLocalizationOptions
    public EkomCultureProvider(RequestLocalizationOptions localizationOptions)
    {
        _localizationOptions = localizationOptions;
        _supportedCulturesByName = localizationOptions.SupportedCultures
            ?.DistinctBy(culture => culture.Name)
            .ToDictionary(culture => culture.Name, culture => culture.Name, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

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

        if (_supportedCulturesByName.TryGetValue(cultureName, out string? supportedCulture))
        {
            // Found in supported list
            return Task.FromResult(new ProviderCultureResult(supportedCulture, supportedCulture));
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
