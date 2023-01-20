using Ekom;
using Ekom.Models;
#if NETCOREAPP
using Microsoft.AspNetCore.Hosting;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;

namespace Ekom.Domain.Repositories
{
    public class CountriesRepository
    {
        private static readonly ConcurrentDictionary<string, List<Country>> _cache = new ConcurrentDictionary<string, List<Country>>();

        private readonly ILogger _logger;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logFac"></param>
        public CountriesRepository(ILogger<CountriesRepository> logger)
        {
            _logger = logger;
        }

        protected virtual string BaseXMLFileName
        {
            get { return "countries"; }
        }

        /// <summary>
        /// Gets all countries.
        /// </summary>
        /// <returns></returns>
        public List<Country> GetAllCountries()
        {
            // todo: multicurrency maybe?
            return _cache.GetOrAdd(BaseXMLFileName, s =>
            {
                // future todo: make file location configurable (web.config or through code)
#if NETCOREAPP
                var env = Configuration.Resolver.GetService<IWebHostEnvironment>();
                //string webRootPath = env.WebRootPath;
                string contentRootPath = env.ContentRootPath;

                var path = Path.Combine(contentRootPath, $"scripts/Ekom/{BaseXMLFileName}.xml");
#else
                var serverUtility = Configuration.Resolver.GetService<HttpContextBase>()?.Server;
                var path = serverUtility.MapPath($"/scripts/Ekom/{BaseXMLFileName}.xml");
#endif

                if (!File.Exists(path))
                {
                    return DotNETFrameworkFallback();
                }

                XDocument doc;
                using (var streamReader = new StreamReader(path, new UTF8Encoding()))
                {
                    doc = XDocument.Load(streamReader);
                }

                return doc.Descendants("country").Select(country => new Country { Name = country.Value, Code = country.Attribute("code").Value }).ToList();
            });
        }

        /// <summary>
        /// Dots the net framework fallback.
        /// </summary>
        /// <returns></returns>
        protected virtual List<Country> DotNETFrameworkFallback()
        {
            var cultureList = new Dictionary<string, string>();

            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                try
                {
                    var region = new RegionInfo(culture.LCID);

                    if (!(cultureList.ContainsKey(region.TwoLetterISORegionName)))
                    {
                        cultureList.Add(region.TwoLetterISORegionName, region.DisplayName);
                    }
                }
                catch
                {
                    // Culture ID 4096 (0x1000) is a neutral culture; a region cannot be created from it.  Error sometimes on some machines
                }

            }

            return cultureList
                .Select(culture => new Country { Name = culture.Value, Code = culture.Key })
                .Where(country => !string.IsNullOrEmpty(country.Name))
                .OrderBy(country => country.Name).ToList();
        }
    }
}
