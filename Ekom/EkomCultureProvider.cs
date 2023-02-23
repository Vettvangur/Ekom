using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Ekom
{

    public class EkomRequestLocalizationOptions : IConfigureOptions<RequestLocalizationOptions>
    {
        public void Configure(RequestLocalizationOptions options)
        {
            // Add the custom provider,
            // in many cases you'll want this to execute before the defaults
            options.RequestCultureProviders.Insert(0, new EkomCultureProvider(options));
        }
    }

    public class EkomCultureProvider : RequestCultureProvider
    {
        private readonly RequestLocalizationOptions _localizationOptions;
        private readonly object _locker = new object();

        // ctor with reference to the RequestLocalizationOptions
        public EkomCultureProvider(RequestLocalizationOptions localizationOptions)
            => _localizationOptions = localizationOptions;

        public override Task<ProviderCultureResult> DetermineProviderCultureResult(HttpContext context)
        {
            var cultureName = context.Request.Headers["Culture"].FirstOrDefault() ?? context.Request.Query["Culture"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(cultureName))
            {
                return NullProviderCultureResult;
            }

            var culture = new CultureInfo(cultureName);

            if (culture is null)
            {
                return NullProviderCultureResult;
            }

            lock (_locker)
            {
                // check if this culture is already supported
                var cultureExists = _localizationOptions.SupportedCultures.Contains(culture);

                if (!cultureExists)
                {
                    // If not, add this as a supporting culture
                    _localizationOptions.SupportedCultures.Add(culture);
                    _localizationOptions.SupportedUICultures.Add(culture);
                }
            }

            return Task.FromResult(new ProviderCultureResult(culture.Name));
        }
    }
}
