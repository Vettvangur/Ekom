using Ekom.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Vettvangur.Core
{

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

                // Add the custom provider,
                // in many cases you'll want this to execute before the defaults
                options.RequestCultureProviders.Insert(0, new CultureProvider(options));
            }
            catch
            {

            }


        }
    }

    public class CultureProvider : RequestCultureProvider
    {
        private readonly RequestLocalizationOptions _localizationOptions;

        // ctor with reference to the RequestLocalizationOptions
        public CultureProvider(RequestLocalizationOptions localizationOptions)
            => _localizationOptions = localizationOptions;

        public override Task<ProviderCultureResult> DetermineProviderCultureResult(HttpContext context)
        {

            var cultureName = context.Request.Query["Accept-Language"].FirstOrDefault()
                            ?? context.Request.Query["Culture"].FirstOrDefault()
                            ?? context.Request.Headers["Accept-Language"].FirstOrDefault()
                            ?? context.Request.Headers["Culture"].FirstOrDefault();

            if (string.IsNullOrEmpty(cultureName))
            {
                return NullProviderCultureResult;
            }

            CultureInfo culture;
            try
            {
                culture = new CultureInfo(cultureName);
            }
            catch (CultureNotFoundException)
            {
                // Culture is not recognized, return null result
                return NullProviderCultureResult;
            }

            return Task.FromResult(new ProviderCultureResult(culture.Name));
        }

    }
}
